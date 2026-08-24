using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using RaidFlow.Data;
using RaidFlow.Models;
using RaidFlow.Services;

namespace RaidFlow.Windows;

public sealed class OverlayWindow : Window
{
    private readonly Configuration configuration;
    private readonly PullTimerService pullTimer;
    private readonly ActionIconService actionIconService;

    public OverlayWindow(Configuration configuration, PullTimerService pullTimer, ActionIconService actionIconService)
        : base(VersionInfo.WindowTitle("RaidFlow オーバーレイ", "RaidFlowOverlay"), ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse, true)
    {
        this.configuration = configuration;
        this.pullTimer = pullTimer;
        this.actionIconService = actionIconService;

        this.IsOpen = false;
        this.Size = new Vector2(360, 220);
        this.SizeCondition = ImGuiCond.FirstUseEver;
        this.AllowClickthrough = true;
        this.RespectCloseHotkey = false;
    }

    public override void PreDraw()
    {
        var settings = this.configuration.Overlay;
        var flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoFocusOnAppearing;

        if (settings.LockOverlay)
        {
            flags |= ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings;
        }

        this.Flags = flags;
        this.BgAlpha = Math.Clamp(settings.BackgroundAlpha, 0.2f, 1f);
        this.ShowCloseButton = !settings.LockOverlay;
        this.IsClickthrough = settings.LockOverlay && settings.ClickThroughWhenLocked;
    }

    public override void OnOpen()
    {
        this.configuration.Overlay.IsOpen = true;
        this.configuration.Save();
    }

    public override void OnClose()
    {
        this.configuration.Overlay.IsOpen = false;
        this.configuration.Save();
    }

    public override void Draw()
    {
        this.configuration.Plan.Normalize();

        var currentTime = this.pullTimer.CurrentTimeSeconds;
        var settings = this.configuration.Overlay;
        var events = this.configuration.Plan.Events
            .OrderBy(timelineEvent => timelineEvent.TimeSeconds)
            .ToList();

        this.DrawTimerHeader(currentTime);
        ImGui.Separator();

        if (events.Count == 0)
        {
            ImGui.TextUnformatted("タイムラインイベントがありません。");
            return;
        }

        var upcoming = events
            .Where(timelineEvent =>
                timelineEvent.TimeSeconds >= currentTime - settings.RecentSeconds &&
                timelineEvent.TimeSeconds <= currentTime + settings.LookAheadSeconds)
            .Take(Math.Clamp(settings.EventCount, 1, 10))
            .ToList();

        if (upcoming.Count == 0)
        {
            var nextEvent = events.FirstOrDefault(timelineEvent => timelineEvent.TimeSeconds >= currentTime);
            if (nextEvent is null)
            {
                ImGui.TextUnformatted("タイムライン終了。");
                return;
            }

            upcoming.Add(nextEvent);
        }

        var primary = upcoming.FirstOrDefault(timelineEvent => timelineEvent.TimeSeconds >= currentTime) ?? upcoming[0];
        this.DrawPrimaryEvent(primary, currentTime);

        ImGui.Spacing();
        foreach (var timelineEvent in upcoming.Where(timelineEvent => timelineEvent.Id != primary.Id))
        {
            this.DrawCompactEvent(timelineEvent, currentTime);
        }
    }

    private void DrawTimerHeader(float currentTime)
    {
        var runningText = this.pullTimer.IsRunning ? "再生中" : "停止中";
        ImGui.TextUnformatted($"{PlannerValidator.FormatTimestamp(currentTime)}  {runningText}");

        if (!this.configuration.Overlay.LockOverlay)
        {
            ImGui.SameLine();
            if (this.pullTimer.IsRunning)
            {
                if (ImGui.SmallButton("停止"))
                {
                    this.pullTimer.Pause();
                }
            }
            else if (ImGui.SmallButton("開始"))
            {
                this.pullTimer.StartFrom(currentTime);
            }

            ImGui.SameLine();
            if (ImGui.SmallButton("リセット"))
            {
                this.pullTimer.Reset();
            }
        }
    }

    private void DrawPrimaryEvent(TimelineEvent timelineEvent, float currentTime)
    {
        var delta = timelineEvent.TimeSeconds - currentTime;
        var color = delta switch
        {
            <= 0 => new Vector4(0.45f, 0.9f, 0.65f, 1f),
            <= 5 => new Vector4(1f, 0.45f, 0.35f, 1f),
            <= 15 => new Vector4(1f, 0.78f, 0.35f, 1f),
            _ => new Vector4(0.65f, 0.82f, 1f, 1f),
        };

        ImGui.TextColored(color, $"{FormatDelta(delta)}  {timelineEvent.Name}");
        ImGui.TextUnformatted($"{PlannerValidator.FormatTimestamp(timelineEvent.TimeSeconds)}  {DisplayText.TimelineEventTypeName(timelineEvent.Type)}");

        if (this.configuration.Overlay.ShowNotes && !string.IsNullOrWhiteSpace(timelineEvent.Notes))
        {
            ImGui.PushTextWrapPos(0);
            ImGui.TextUnformatted(timelineEvent.Notes);
            ImGui.PopTextWrapPos();
        }

        this.DrawAssignments(timelineEvent);
    }

    private void DrawCompactEvent(TimelineEvent timelineEvent, float currentTime)
    {
        var delta = timelineEvent.TimeSeconds - currentTime;
        ImGui.Separator();
        ImGui.TextUnformatted($"{FormatDelta(delta)}  {timelineEvent.Name}");

        if (this.configuration.Overlay.ShowNotes && !string.IsNullOrWhiteSpace(timelineEvent.Notes))
        {
            ImGui.PushTextWrapPos(0);
            ImGui.TextUnformatted(timelineEvent.Notes);
            ImGui.PopTextWrapPos();
        }

        this.DrawAssignments(timelineEvent);
    }

    private void DrawAssignments(TimelineEvent timelineEvent)
    {
        var settings = this.configuration.Overlay;
        var selectedSlot = this.configuration.SelectedSlot;
        var assignments = timelineEvent.Assignments
            .Select(assignment => new
            {
                Assignment = assignment,
                Action = MitigationCatalog.FindAction(assignment.ActionId, this.configuration.Plan.ContentLevel),
            })
            .Where(item =>
                !settings.ShowOnlySelectedSlot ||
                item.Assignment.Slot == selectedSlot ||
                (settings.AlwaysShowRaidMitigation && item.Action?.IsRaidMitigation == true))
            .OrderBy(item => item.Assignment.Slot == selectedSlot ? 0 : 1)
            .ThenBy(item => item.Assignment.Slot)
            .ToList();

        if (assignments.Count == 0)
        {
            return;
        }

        foreach (var item in assignments)
        {
            var assignment = item.Assignment;
            var action = item.Action;
            var useTime = timelineEvent.TimeSeconds + assignment.UseOffsetSeconds;
            this.DrawAssignmentIconBlock(assignment, action, useTime, assignment.Slot == selectedSlot);
        }
    }

    private void DrawAssignmentIconBlock(
        MitigationAssignment assignment,
        MitigationActionDefinition? action,
        float useTime,
        bool isSelectedSlot)
    {
        const float iconSize = 34f;
        const float blockWidth = 74f;
        const float blockHeight = 74f;
        const float gap = 6f;

        var start = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var bgColor = ImGui.GetColorU32(isSelectedSlot
            ? new Vector4(0.10f, 0.45f, 0.60f, 0.48f)
            : new Vector4(0.08f, 0.08f, 0.08f, 0.34f));
        var borderColor = ImGui.GetColorU32(isSelectedSlot
            ? new Vector4(0.45f, 0.9f, 1f, 0.95f)
            : new Vector4(0.45f, 0.45f, 0.45f, 0.35f));

        drawList.AddRectFilled(start, start + new Vector2(blockWidth, blockHeight), bgColor, 4f);
        drawList.AddRect(start, start + new Vector2(blockWidth, blockHeight), borderColor, 4f);

        var blockCursorX = ImGui.GetCursorPosX();
        ImGui.BeginGroup();
        var actionLabel = action?.ShortName ?? assignment.ActionId.ToString();
        DrawCenteredText(actionLabel, blockCursorX, blockWidth);

        var iconStartX = blockCursorX + ((blockWidth - iconSize) * 0.5f);
        ImGui.SetCursorPosX(iconStartX);
        if (action is not null && this.actionIconService.TryGetIcon(action, out var texture))
        {
            var wrap = texture.GetWrapOrEmpty();
            ImGui.Image(wrap.Handle, new Vector2(iconSize, iconSize));
        }
        else
        {
            var pos = ImGui.GetCursorScreenPos();
            drawList.AddRectFilled(pos, pos + new Vector2(iconSize, iconSize), ImGui.GetColorU32(new Vector4(0.18f, 0.18f, 0.18f, 0.85f)), 3f);
            drawList.AddRect(pos, pos + new Vector2(iconSize, iconSize), ImGui.GetColorU32(new Vector4(0.75f, 0.75f, 0.75f, 0.45f)), 3f);
            ImGui.Dummy(new Vector2(iconSize, iconSize));
        }

        DrawCenteredText(assignment.Slot.ToString(), blockCursorX, blockWidth);
        ImGui.EndGroup();

        if (ImGui.IsItemHovered())
        {
            var tooltip = $"{actionLabel}\n担当: {assignment.Slot}\n使用: {PlannerValidator.FormatTimestamp(useTime)}";
            if (!string.IsNullOrWhiteSpace(assignment.Note))
            {
                tooltip += $"\n{assignment.Note}";
            }

            ImGui.SetTooltip(tooltip);
        }

        var windowRight = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X;
        if (start.X + blockWidth + gap + blockWidth < windowRight)
        {
            ImGui.SameLine(0, gap);
        }
    }

    private static void DrawCenteredText(string text, float baseX, float width)
    {
        var clipped = FitText(text, width - 8f);
        var textWidth = ImGui.CalcTextSize(clipped).X;
        ImGui.SetCursorPosX(baseX + Math.Max(0f, (width - textWidth) * 0.5f));
        ImGui.TextUnformatted(clipped);
    }

    private static string FitText(string text, float maxWidth)
    {
        if (ImGui.CalcTextSize(text).X <= maxWidth)
        {
            return text;
        }

        var clipped = text;
        while (clipped.Length > 1 && ImGui.CalcTextSize($"{clipped}.").X > maxWidth)
        {
            clipped = clipped[..^1];
        }

        return $"{clipped}.";
    }

    private static string FormatDelta(float seconds)
    {
        return seconds < 0
            ? $"+{PlannerValidator.FormatTimestamp(Math.Abs(seconds))}"
            : PlannerValidator.FormatTimestamp(seconds);
    }
}

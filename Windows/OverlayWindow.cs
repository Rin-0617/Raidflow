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

    public OverlayWindow(Configuration configuration, PullTimerService pullTimer)
        : base("RaidFlow オーバーレイ###RaidFlowOverlay", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse, true)
    {
        this.configuration = configuration;
        this.pullTimer = pullTimer;

        this.IsOpen = configuration.Overlay.IsOpen;
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
            var label = $"{assignment.Slot}:{action?.ShortName ?? assignment.ActionId.ToString()} @{PlannerValidator.FormatTimestamp(useTime)}";
            var line = string.IsNullOrWhiteSpace(assignment.Note)
                ? $"[{label}]"
                : $"[{label}] {assignment.Note}";

            ImGui.PushTextWrapPos(0);
            if (assignment.Slot == selectedSlot)
            {
                ImGui.TextColored(new Vector4(0.45f, 0.9f, 1f, 1f), line);
            }
            else if (settings.ShowOnlySelectedSlot && action?.IsRaidMitigation == true)
            {
                ImGui.TextColored(new Vector4(0.8f, 0.82f, 0.86f, 1f), line);
            }
            else
            {
                ImGui.TextUnformatted(line);
            }

            ImGui.PopTextWrapPos();
        }
    }

    private static string FormatDelta(float seconds)
    {
        var sign = seconds < 0 ? "T+" : "T-";
        return $"{sign}{PlannerValidator.FormatTimestamp(Math.Abs(seconds))}";
    }
}

using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using RaidFlow.Data;
using RaidFlow.Models;
using RaidFlow.Services;

namespace RaidFlow.Windows;

public sealed class MainWindow : Window
{
    private readonly Configuration configuration;
    private readonly PullTimerService pullTimer;
    private readonly OverlayWindow overlayWindow;
    private readonly CombatSyncService combatSyncService;
    private readonly ActionIconService actionIconService;
    private readonly FileDialogManager fileDialogManager = new();
    private Task<FFLogsImportResult>? fflogsImportTask;
    private bool showFFLogsAccessToken;
    private string fflogsImportStatus = string.Empty;
    private string importStatus = string.Empty;
    private int selectedEventIndex;
    private float timerSetSeconds;

    public MainWindow(
        Configuration configuration,
        PullTimerService pullTimer,
        OverlayWindow overlayWindow,
        CombatSyncService combatSyncService,
        ActionIconService actionIconService)
        : base(VersionInfo.WindowTitle("RaidFlow", "RaidFlowMain"))
    {
        this.configuration = configuration;
        this.pullTimer = pullTimer;
        this.overlayWindow = overlayWindow;
        this.combatSyncService = combatSyncService;
        this.actionIconService = actionIconService;
        this.Size = new Vector2(1120, 720);
        this.SizeCondition = ImGuiCond.FirstUseEver;
        this.IsOpen = true;
    }

    public override void Draw()
    {
        this.configuration.Plan.Normalize();
        this.UpdateFFLogsImportTask();

        this.DrawHeader();

        if (ImGui.BeginTabBar("RaidFlowTabs"))
        {
            if (ImGui.BeginTabItem("タイムライン"))
            {
                this.DrawTimelineTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("PT設定"))
            {
                this.DrawPartyTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("警告"))
            {
                this.DrawWarningsTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("オーバーレイ"))
            {
                this.DrawOverlayTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("インポート/エクスポート"))
            {
                this.DrawImportExportTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("FFLogs読込"))
            {
                this.DrawFFLogsTab();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        this.fileDialogManager.Draw();
    }

    private void DrawHeader()
    {
        var plan = this.configuration.Plan;
        var contentName = plan.ContentName;
        var revision = plan.Revision;

        ImGui.SetNextItemWidth(260);
        if (ImGui.InputText("コンテンツ", ref contentName, 128))
        {
            plan.ContentName = contentName;
            this.configuration.Save();
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(90);
        if (ImGui.InputText("版", ref revision, 32))
        {
            plan.Revision = revision;
            this.configuration.Save();
        }

        ImGui.SameLine();
        var contentLevel = plan.ContentLevel;
        ImGui.SetNextItemWidth(80);
        if (ImGui.InputInt("Lv", ref contentLevel))
        {
            plan.ContentLevel = Math.Clamp(contentLevel, 1, 120);
            this.configuration.Save();
        }

        ImGui.SameLine();
        ImGui.TextUnformatted($"TL {TimelineHasher.Compute(plan)}");

        ImGui.Separator();

        this.DrawSelectedSlotControls();
    }

    private void DrawSelectedSlotControls()
    {
        var selectedSlot = this.configuration.SelectedSlot;
        ImGui.SetNextItemWidth(95);
        if (DrawEnumCombo("編集スロット", ref selectedSlot))
        {
            this.configuration.SelectedSlot = selectedSlot;
            this.configuration.Save();
        }

        var profile = this.configuration.Plan.Party.First(member => member.Slot == this.configuration.SelectedSlot);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(120);
        var job = profile.Job;
        if (DrawJobCombo("ジョブ", ref job, MitigationCatalog.JobsForSlot(profile.Slot)))
        {
            profile.Job = job;
            this.configuration.Save();
        }

        ImGui.SameLine();
        ImGui.TextUnformatted("選択スロット/ジョブに応じてスキル一覧が変わります。");
    }

    private void DrawOverlayTab()
    {
        var settings = this.configuration.Overlay;

        var isOpen = this.overlayWindow.IsOpen;
        if (ImGui.Checkbox("オーバーレイを表示", ref isOpen))
        {
            this.overlayWindow.IsOpen = isOpen;
            settings.IsOpen = isOpen;
            this.configuration.Save();
        }

        var lockOverlay = settings.LockOverlay;
        if (ImGui.Checkbox("オーバーレイを固定", ref lockOverlay))
        {
            settings.LockOverlay = lockOverlay;
            this.configuration.Save();
        }

        var clickThrough = settings.ClickThroughWhenLocked;
        if (ImGui.Checkbox("固定中はクリック透過", ref clickThrough))
        {
            settings.ClickThroughWhenLocked = clickThrough;
            this.configuration.Save();
        }

        var selectedOnly = settings.ShowOnlySelectedSlot;
        if (ImGui.Checkbox("選択スロットの担当だけ表示", ref selectedOnly))
        {
            settings.ShowOnlySelectedSlot = selectedOnly;
            this.configuration.Save();
        }

        var alwaysShowRaidMitigation = settings.AlwaysShowRaidMitigation;
        if (ImGui.Checkbox("PT軽減は常に表示", ref alwaysShowRaidMitigation))
        {
            settings.AlwaysShowRaidMitigation = alwaysShowRaidMitigation;
            this.configuration.Save();
        }

        var showNotes = settings.ShowNotes;
        if (ImGui.Checkbox("ギミックメモを表示", ref showNotes))
        {
            settings.ShowNotes = showNotes;
            this.configuration.Save();
        }

        ImGui.Separator();

        var autoStart = settings.AutoStartOnCombat;
        if (ImGui.Checkbox("戦闘開始で自動スタート", ref autoStart))
        {
            settings.AutoStartOnCombat = autoStart;
            this.configuration.Save();
        }

        var openOnAutoStart = settings.AutoOpenOverlayOnCombatStart;
        if (ImGui.Checkbox("自動スタート時にオーバーレイを開く", ref openOnAutoStart))
        {
            settings.AutoOpenOverlayOnCombatStart = openOnAutoStart;
            this.configuration.Save();
        }

        var startOffset = settings.AutoStartOffsetSeconds;
        ImGui.SetNextItemWidth(180);
        if (ImGui.SliderFloat("自動スタート時刻", ref startOffset, 0, 10, "%.1f"))
        {
            settings.AutoStartOffsetSeconds = startOffset;
            this.configuration.Save();
        }

        var resetOnDutyWipe = settings.AutoResetOnDutyWipe;
        if (ImGui.Checkbox("ワイプ検知で自動リセット", ref resetOnDutyWipe))
        {
            settings.AutoResetOnDutyWipe = resetOnDutyWipe;
            this.configuration.Save();
        }

        var resetAfterCombatEnd = settings.AutoResetAfterCombatEnd;
        if (ImGui.Checkbox("戦闘終了後リセットを保険で使う", ref resetAfterCombatEnd))
        {
            settings.AutoResetAfterCombatEnd = resetAfterCombatEnd;
            this.configuration.Save();
        }

        var delayDuringCutscene = settings.DelayCombatEndResetDuringCutscene;
        if (ImGui.Checkbox("カットシーン/エリア移動中は保険リセットを延期", ref delayDuringCutscene))
        {
            settings.DelayCombatEndResetDuringCutscene = delayDuringCutscene;
            this.configuration.Save();
        }

        var resetDelay = settings.CombatEndResetDelaySeconds;
        ImGui.SetNextItemWidth(180);
        if (ImGui.SliderFloat("戦闘終了後リセット猶予", ref resetDelay, 0, 60, "%.0f"))
        {
            settings.CombatEndResetDelaySeconds = resetDelay;
            this.configuration.Save();
        }

        ImGui.TextUnformatted($"同期状態: {this.combatSyncService.LastStatus}");
        if (this.combatSyncService.HasPendingCombatEndReset)
        {
            ImGui.TextUnformatted($"リセット待機: {this.combatSyncService.PendingCombatEndResetSeconds:0.0}秒");
        }

        ImGui.Separator();

        var eventCount = settings.EventCount;
        ImGui.SetNextItemWidth(180);
        if (ImGui.SliderInt("表示イベント数", ref eventCount, 1, 10))
        {
            settings.EventCount = eventCount;
            this.configuration.Save();
        }

        var lookAhead = settings.LookAheadSeconds;
        ImGui.SetNextItemWidth(180);
        if (ImGui.SliderFloat("先読み秒数", ref lookAhead, 15, 600, "%.0f"))
        {
            settings.LookAheadSeconds = lookAhead;
            this.configuration.Save();
        }

        var recent = settings.RecentSeconds;
        ImGui.SetNextItemWidth(180);
        if (ImGui.SliderFloat("直近イベント保持秒数", ref recent, 0, 30, "%.0f"))
        {
            settings.RecentSeconds = recent;
            this.configuration.Save();
        }

        var backgroundAlpha = settings.BackgroundAlpha;
        ImGui.SetNextItemWidth(180);
        if (ImGui.SliderFloat("背景透明度", ref backgroundAlpha, 0.2f, 1f, "%.2f"))
        {
            settings.BackgroundAlpha = backgroundAlpha;
            this.configuration.Save();
        }

        ImGui.Separator();
        this.DrawTimerControls();
    }

    private void DrawTimerControls()
    {
        ImGui.TextUnformatted($"プルタイマー: {PlannerValidator.FormatTimestamp(this.pullTimer.CurrentTimeSeconds)}");

        if (ImGui.Button("0秒から開始"))
        {
            this.pullTimer.StartFrom();
            this.overlayWindow.IsOpen = true;
            this.configuration.Overlay.IsOpen = true;
            this.configuration.Save();
        }

        ImGui.SameLine();
        if (this.pullTimer.IsRunning)
        {
            if (ImGui.Button("一時停止"))
            {
                this.pullTimer.Pause();
            }
        }
        else if (ImGui.Button("再開"))
        {
            this.pullTimer.Resume();
        }

        ImGui.SameLine();
        if (ImGui.Button("リセット"))
        {
            this.pullTimer.Reset();
        }

        if (ImGui.Button("-5s"))
        {
            this.pullTimer.Nudge(-5);
        }

        ImGui.SameLine();
        if (ImGui.Button("+5s"))
        {
            this.pullTimer.Nudge(5);
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(100);
        ImGui.InputFloat("秒を指定", ref this.timerSetSeconds, 1, 10, "%.1f");

        ImGui.SameLine();
        if (ImGui.Button("適用"))
        {
            this.pullTimer.SetCurrentTime(this.timerSetSeconds);
        }
    }

    private void DrawTimelineTab()
    {
        if (ImGui.Button("イベント追加"))
        {
            this.AddEvent();
        }

        ImGui.SameLine();
        if (ImGui.Button("時刻順に並べる"))
        {
            this.configuration.Plan.Events = this.configuration.Plan.Events.OrderBy(item => item.TimeSeconds).ToList();
            this.configuration.Save();
        }

        ImGui.SameLine();
        if (ImGui.Button("タイムライン全削除") && this.configuration.Plan.Events.Count > 0)
        {
            ImGui.OpenPopup("ClearTimelineConfirm");
        }

        if (ImGui.BeginPopup("ClearTimelineConfirm"))
        {
            ImGui.TextUnformatted($"タイムラインのイベント {this.configuration.Plan.Events.Count} 件と担当軽減をすべて削除します。");
            ImGui.TextUnformatted("この操作は元に戻せません。");

            if (ImGui.Button("削除する"))
            {
                this.ClearTimeline();
                ImGui.CloseCurrentPopup();
            }

            ImGui.SameLine();
            if (ImGui.Button("キャンセル"))
            {
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }

        ImGui.Columns(2, "TimelineColumns", true);
        this.DrawEventList();
        ImGui.NextColumn();
        this.DrawEventEditor();
        ImGui.Columns(1);
    }

    private void DrawEventList()
    {
        var events = this.configuration.Plan.Events;
        this.selectedEventIndex = Math.Clamp(this.selectedEventIndex, 0, Math.Max(0, events.Count - 1));

        if (ImGui.BeginTable("TimelineTable", 5, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable))
        {
            ImGui.TableSetupColumn("時刻", ImGuiTableColumnFlags.WidthFixed, 70);
            ImGui.TableSetupColumn("イベント");
            ImGui.TableSetupColumn("種別", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("担当");
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 54);
            ImGui.TableHeadersRow();

            for (var index = 0; index < events.Count; index++)
            {
                var timelineEvent = events[index];
                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                ImGui.TextUnformatted(PlannerValidator.FormatTimestamp(timelineEvent.TimeSeconds));

                ImGui.TableSetColumnIndex(1);
                if (ImGui.Selectable($"{timelineEvent.Name}##event_{timelineEvent.Id}", this.selectedEventIndex == index))
                {
                    this.selectedEventIndex = index;
                }

                ImGui.TableSetColumnIndex(2);
                ImGui.TextUnformatted(DisplayText.TimelineEventTypeName(timelineEvent.Type));

                ImGui.TableSetColumnIndex(3);
                this.DrawAssignmentChips(timelineEvent);

                ImGui.TableSetColumnIndex(4);
                if (ImGui.SmallButton($"削除##delete_{timelineEvent.Id}"))
                {
                    events.RemoveAt(index);
                    this.selectedEventIndex = Math.Clamp(this.selectedEventIndex, 0, Math.Max(0, events.Count - 1));
                    this.configuration.Save();
                    break;
                }
            }

            ImGui.EndTable();
        }
    }

    private void DrawEventEditor()
    {
        var events = this.configuration.Plan.Events;
        if (events.Count == 0)
        {
            ImGui.TextUnformatted("イベントを追加してください。");
            return;
        }

        var timelineEvent = events[this.selectedEventIndex];
        ImGui.TextUnformatted("選択中イベント");
        ImGui.Separator();

        var time = timelineEvent.TimeSeconds;
        ImGui.SetNextItemWidth(110);
        if (ImGui.InputFloat("時刻", ref time, 1, 10, "%.1f"))
        {
            timelineEvent.TimeSeconds = Math.Max(0, time);
            this.configuration.Save();
        }

        var name = timelineEvent.Name;
        ImGui.SetNextItemWidth(320);
        if (ImGui.InputText("名前", ref name, 128))
        {
            timelineEvent.Name = name;
            this.configuration.Save();
        }

        var type = timelineEvent.Type;
        if (DrawEnumCombo("種別", ref type))
        {
            timelineEvent.Type = type;
            this.configuration.Save();
        }

        var notes = timelineEvent.Notes;
        ImGui.TextUnformatted("メモ");
        if (ImGui.InputTextMultiline("##event_notes", ref notes, 500, new Vector2(-1, 78)))
        {
            timelineEvent.Notes = notes;
            this.configuration.Save();
        }

        ImGui.Spacing();
        this.DrawSkillPalette(timelineEvent);

        ImGui.Spacing();
        this.DrawAssignmentEditor(timelineEvent);
    }

    private void DrawSkillPalette(TimelineEvent timelineEvent)
    {
        var profile = this.configuration.Plan.Party.First(member => member.Slot == this.configuration.SelectedSlot);
        var actions = MitigationCatalog.ActionsForJob(profile.Job, this.configuration.Plan.ContentLevel);

        ImGui.TextUnformatted($"{profile.Slot} / {profile.Job} Lv{this.configuration.Plan.ContentLevel} スキル");
        if (actions.Count == 0)
        {
            ImGui.TextUnformatted("このジョブのスキルはまだ登録されていません。");
            return;
        }

        var availableWidth = ImGui.GetContentRegionAvail().X;
        var buttonWidth = 148f;
        var columns = Math.Max(1, (int)(availableWidth / (buttonWidth + 8)));
        var index = 0;

        foreach (var action in actions)
        {
            if (this.DrawActionPickerButton(action, $"add_{timelineEvent.Id}_{action.ActionId}", new Vector2(buttonWidth, 34)))
            {
                timelineEvent.Assignments.Add(new MitigationAssignment
                {
                    Slot = profile.Slot,
                    Job = profile.Job,
                    ActionId = action.ActionId,
                    UseOffsetSeconds = action.DefaultUseOffsetSeconds,
                });
                this.configuration.Save();
            }

            index++;
            if (index % columns != 0)
            {
                ImGui.SameLine();
            }
        }
    }

    private bool DrawActionPickerButton(MitigationActionDefinition action, string id, Vector2 size)
    {
        var clicked = false;
        var displayName = this.actionIconService.DisplayName(action);
        var iconSize = new Vector2(28, 28);
        var textWidth = size.X;

        ImGui.BeginGroup();
        if (this.actionIconService.TryGetIcon(action, out var texture))
        {
            var wrap = texture.GetWrapOrEmpty();
            ImGui.PushID($"icon_{id}");
            clicked |= ImGui.ImageButton(wrap.Handle, iconSize, Vector2.Zero, Vector2.One, -1, Vector4.Zero, Vector4.One);
            ImGui.PopID();
            ImGui.SameLine(0, 4);
            textWidth -= iconSize.X + 6;
        }

        clicked |= ImGui.Button($"{action.ShortName}##text_{id}", new Vector2(Math.Max(72, textWidth), iconSize.Y));
        ImGui.EndGroup();

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip($"{displayName}\n効果時間: {action.DurationSeconds:0}秒\nリキャスト: {action.CooldownSeconds:0}秒");
        }

        return clicked;
    }

    private void DrawActionIcon(MitigationActionDefinition action, Vector2 size, string id)
    {
        if (!this.actionIconService.TryGetIcon(action, out var texture))
        {
            return;
        }

        ImGui.PushID(id);
        var wrap = texture.GetWrapOrEmpty();
        ImGui.Image(wrap.Handle, size);
        ImGui.PopID();
    }

    private void DrawAssignmentEditor(TimelineEvent timelineEvent)
    {
        if (timelineEvent.Assignments.Count == 0)
        {
            ImGui.TextUnformatted("このイベントには担当がありません。");
            return;
        }

        ImGui.TextUnformatted("担当");
        if (ImGui.BeginTable("AssignmentEditor", 7, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable))
        {
            ImGui.TableSetupColumn("枠", ImGuiTableColumnFlags.WidthFixed, 55);
            ImGui.TableSetupColumn("ジョブ", ImGuiTableColumnFlags.WidthFixed, 55);
            ImGui.TableSetupColumn("スキル", ImGuiTableColumnFlags.WidthFixed, 180);
            ImGui.TableSetupColumn("使用", ImGuiTableColumnFlags.WidthFixed, 86);
            ImGui.TableSetupColumn("有効範囲", ImGuiTableColumnFlags.WidthFixed, 120);
            ImGui.TableSetupColumn("メモ");
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 54);
            ImGui.TableHeadersRow();

            for (var index = 0; index < timelineEvent.Assignments.Count; index++)
            {
                var assignment = timelineEvent.Assignments[index];
                var action = MitigationCatalog.FindAction(assignment.ActionId, this.configuration.Plan.ContentLevel);

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.TextUnformatted(assignment.Slot.ToString());

                ImGui.TableSetColumnIndex(1);
                ImGui.TextUnformatted(assignment.Job);

                ImGui.TableSetColumnIndex(2);
                if (action is not null)
                {
                    this.DrawActionIcon(action, new Vector2(20, 20), $"asg_icon_{assignment.Id}");
                    ImGui.SameLine();
                    ImGui.TextUnformatted(this.actionIconService.DisplayName(action));
                }
                else
                {
                    ImGui.TextUnformatted($"アクション {assignment.ActionId}");
                }

                ImGui.TableSetColumnIndex(3);
                var offset = assignment.UseOffsetSeconds;
                ImGui.SetNextItemWidth(74);
                if (ImGui.DragFloat($"##offset_{assignment.Id}", ref offset, 0.1f, -120, 30, "%+.1fs"))
                {
                    assignment.UseOffsetSeconds = offset;
                    this.configuration.Save();
                }

                ImGui.TableSetColumnIndex(4);
                if (action is not null)
                {
                    var useTime = timelineEvent.TimeSeconds + assignment.UseOffsetSeconds;
                    var endTime = useTime + action.DurationSeconds;
                    ImGui.TextUnformatted($"{PlannerValidator.FormatTimestamp(useTime)}-{PlannerValidator.FormatTimestamp(endTime)}");
                }

                ImGui.TableSetColumnIndex(5);
                var note = assignment.Note;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputText($"##note_{assignment.Id}", ref note, 160))
                {
                    assignment.Note = note;
                    this.configuration.Save();
                }

                ImGui.TableSetColumnIndex(6);
                if (ImGui.SmallButton($"削除##assignment_{assignment.Id}"))
                {
                    timelineEvent.Assignments.RemoveAt(index);
                    this.configuration.Save();
                    break;
                }
            }

            ImGui.EndTable();
        }
    }

    private void DrawAssignmentChips(TimelineEvent timelineEvent)
    {
        var first = true;
        foreach (var assignment in timelineEvent.Assignments.OrderBy(assignment => assignment.Slot))
        {
            var action = MitigationCatalog.FindAction(assignment.ActionId, this.configuration.Plan.ContentLevel);
            if (!first)
            {
                ImGui.SameLine();
            }

            first = false;
            ImGui.TextUnformatted($"[{assignment.Slot}:{action?.ShortName ?? assignment.ActionId.ToString()}]");
        }
    }

    private void DrawPartyTab()
    {
        if (ImGui.BeginTable("PartySetup", 4, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable))
        {
            ImGui.TableSetupColumn("枠", ImGuiTableColumnFlags.WidthFixed, 70);
            ImGui.TableSetupColumn("プレイヤー");
            ImGui.TableSetupColumn("ジョブ", ImGuiTableColumnFlags.WidthFixed, 130);
            ImGui.TableSetupColumn("ロール", ImGuiTableColumnFlags.WidthFixed, 140);
            ImGui.TableHeadersRow();

            foreach (var profile in this.configuration.Plan.Party.OrderBy(member => member.Slot))
            {
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                if (ImGui.Selectable(profile.Slot.ToString(), this.configuration.SelectedSlot == profile.Slot))
                {
                    this.configuration.SelectedSlot = profile.Slot;
                    this.configuration.Save();
                }

                ImGui.TableSetColumnIndex(1);
                var playerName = profile.PlayerName;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputText($"##player_{profile.Slot}", ref playerName, 64))
                {
                    profile.PlayerName = playerName;
                    this.configuration.Save();
                }

                ImGui.TableSetColumnIndex(2);
                ImGui.SetNextItemWidth(-1);
                var job = profile.Job;
                if (DrawJobCombo($"##job_{profile.Slot}", ref job, MitigationCatalog.JobsForSlot(profile.Slot)))
                {
                    profile.Job = job;
                    this.configuration.Save();
                }

                ImGui.TableSetColumnIndex(3);
                ImGui.TextUnformatted(profile.Slot == PartySlot.D2
                    ? "DPSフリー"
                    : DisplayText.PartyRoleName(MitigationCatalog.SuggestedRole(profile.Slot)));
            }

            ImGui.EndTable();
        }
    }

    private void DrawWarningsTab()
    {
        var warnings = PlannerValidator.Validate(this.configuration.Plan);
        if (warnings.Count == 0)
        {
            ImGui.TextUnformatted("警告はありません。");
            return;
        }

        foreach (var warning in warnings)
        {
            ImGui.TextColored(new Vector4(1f, 0.65f, 0.2f, 1f), warning.Message);
        }
    }

    private void DrawImportExportTab()
    {
        if (ImGui.Button("選択スロットを.radflow保存"))
        {
            this.OpenSavePersonalPlanDialog();
        }

        ImGui.SameLine();
        if (ImGui.Button("全体プランを.radflow保存"))
        {
            this.OpenSaveFullPlanDialog();
        }

        ImGui.SameLine();
        if (ImGui.Button(".radflowを読み込み"))
        {
            this.OpenImportPlanDialog();
        }

        if (!string.IsNullOrWhiteSpace(this.importStatus))
        {
            ImGui.Spacing();
            ImGui.PushTextWrapPos(0);
            ImGui.TextUnformatted(this.importStatus);
            ImGui.PopTextWrapPos();
        }

        ImGui.Separator();

        if (!ImGui.CollapsingHeader("JSON詳細"))
        {
            return;
        }

        if (ImGui.Button("選択スロットJSON生成"))
        {
            this.configuration.ExportBuffer = ImportExportService.ExportPersonal(
                this.configuration.Plan,
                this.configuration.SelectedSlot);
            ImGui.SetClipboardText(this.configuration.ExportBuffer);
            this.configuration.Save();
        }

        ImGui.SameLine();
        if (ImGui.Button("全体プランJSON生成"))
        {
            this.configuration.ExportBuffer = ImportExportService.ExportFullPlan(this.configuration.Plan);
            ImGui.SetClipboardText(this.configuration.ExportBuffer);
            this.configuration.Save();
        }

        ImGui.SameLine();
        if (ImGui.Button("コピー"))
        {
            ImGui.SetClipboardText(this.configuration.ExportBuffer);
        }

        ImGui.TextUnformatted("エクスポート");
        var exportBuffer = this.configuration.ExportBuffer;
        if (ImGui.InputTextMultiline("##export", ref exportBuffer, 20000, new Vector2(-1, 180), ImGuiInputTextFlags.ReadOnly))
        {
            this.configuration.ExportBuffer = exportBuffer;
        }

        ImGui.Separator();

        ImGui.TextUnformatted("インポート");
        var importBuffer = this.configuration.ImportBuffer;
        if (ImGui.InputTextMultiline("##import", ref importBuffer, 20000, new Vector2(-1, 180)))
        {
            this.configuration.ImportBuffer = importBuffer;
            this.configuration.Save();
        }

        if (ImGui.Button("JSONを合成/インポート"))
        {
            var result = ImportExportService.ImportInto(this.configuration.Plan, this.configuration.ImportBuffer);
            this.importStatus = result.Message;
            if (result.Success)
            {
                this.configuration.Save();
            }
        }

        if (!string.IsNullOrWhiteSpace(this.importStatus))
        {
            ImGui.SameLine();
            ImGui.TextUnformatted(this.importStatus);
        }
    }

    private void OpenSavePersonalPlanDialog()
    {
        this.fileDialogManager.SaveFileDialog(
            "個人プランを保存",
            ImportExportService.FileDialogFilter,
            ImportExportService.DefaultPersonalPlanFileName(this.configuration.Plan, this.configuration.SelectedSlot),
            ImportExportService.FileExtension,
            (success, filePath) =>
            {
                if (!success)
                {
                    return;
                }

                try
                {
                    this.importStatus = ImportExportService.SavePersonalPlanToFile(
                        this.configuration.Plan,
                        this.configuration.SelectedSlot,
                        filePath);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
                {
                    this.importStatus = $"ファイル保存に失敗しました: {exception.Message}";
                }
            });
    }

    private void OpenSaveFullPlanDialog()
    {
        this.fileDialogManager.SaveFileDialog(
            "全体プランを保存",
            ImportExportService.FileDialogFilter,
            ImportExportService.DefaultFullPlanFileName(this.configuration.Plan),
            ImportExportService.FileExtension,
            (success, filePath) =>
            {
                if (!success)
                {
                    return;
                }

                try
                {
                    this.importStatus = ImportExportService.SaveFullPlanToFile(this.configuration.Plan, filePath);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
                {
                    this.importStatus = $"ファイル保存に失敗しました: {exception.Message}";
                }
            });
    }

    private void OpenImportPlanDialog()
    {
        this.fileDialogManager.OpenFileDialog(
            "RaidFlowファイルを読み込み",
            ImportExportService.FileDialogFilter,
            (success, filePath) =>
            {
                if (!success)
                {
                    return;
                }

                var result = ImportExportService.ImportFileInto(this.configuration.Plan, filePath);
                this.importStatus = result.Message;
                if (result.Success)
                {
                    this.configuration.Save();
                }
            });
    }

    private void DrawFFLogsTab()
    {
        var settings = this.configuration.FFLogs;

        ImGui.TextUnformatted("レポート");
        var reportUrl = settings.ReportUrl;
        ImGui.TextUnformatted("レポートURL");
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputText("##fflogs_report_url", ref reportUrl, 500))
        {
            settings.ReportUrl = reportUrl;
            this.configuration.Save();
        }

        ImGui.PushTextWrapPos(0);
        ImGui.TextUnformatted("ja.fflogs.com / www.fflogs.com の公開レポートURLに対応しています。URL内の fight= があれば自動で使います。");
        ImGui.PopTextWrapPos();

        var fightId = settings.FightId;
        ImGui.TextUnformatted("Fight ID");
        ImGui.SetNextItemWidth(120);
        if (ImGui.InputInt("##fflogs_fight_id", ref fightId))
        {
            settings.FightId = Math.Max(0, fightId);
            this.configuration.Save();
        }
        ImGui.SameLine();
        ImGui.TextUnformatted("0ならURL内のfight、または最後のボス戦を使用");

        ImGui.TextUnformatted("敵castイベントはすべて取り込みます。");

        var replaceTimeline = settings.ReplaceTimelineOnImport;
        if (ImGui.Checkbox("読み込み時に現在のタイムラインを置き換える", ref replaceTimeline))
        {
            settings.ReplaceTimelineOnImport = replaceTimeline;
            this.configuration.Save();
        }

        ImGui.Separator();

        ImGui.TextUnformatted("FFLogs API認証");
        var accessToken = settings.AccessToken;
        ImGui.TextUnformatted("アクセストークン (Access Token)");
        ImGui.SetNextItemWidth(-1);
        var tokenFlags = this.showFFLogsAccessToken ? ImGuiInputTextFlags.None : ImGuiInputTextFlags.Password;
        if (ImGui.InputText("##fflogs_access_token", ref accessToken, 2000, tokenFlags))
        {
            settings.AccessToken = accessToken;
            settings.AccessTokenExpiresAtUtc = default;
            this.configuration.Save();
        }

        if (ImGui.Checkbox("トークンを表示", ref this.showFFLogsAccessToken))
        {
            // UI-only toggle.
        }

        ImGui.SameLine();
        if (ImGui.Button("トークンをクリア"))
        {
            settings.AccessToken = string.Empty;
            settings.AccessTokenExpiresAtUtc = default;
            this.configuration.Save();
        }

        if (!string.IsNullOrWhiteSpace(settings.AccessToken) && settings.AccessTokenExpiresAtUtc != default)
        {
            ImGui.TextUnformatted($"Token期限(UTC): {settings.AccessTokenExpiresAtUtc:yyyy-MM-dd HH:mm:ss}");
        }

        if (ImGui.CollapsingHeader("Client ID / Client Secret でトークン取得"))
        {
            var clientId = settings.ClientId;
            ImGui.TextUnformatted("Client ID");
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##fflogs_client_id", ref clientId, 200))
            {
                settings.ClientId = clientId;
                this.configuration.Save();
            }

            var clientSecret = settings.ClientSecret;
            ImGui.TextUnformatted("Client Secret");
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##fflogs_client_secret", ref clientSecret, 200, ImGuiInputTextFlags.Password))
            {
                settings.ClientSecret = clientSecret;
                this.configuration.Save();
            }
        }

        ImGui.Separator();

        if (this.fflogsImportTask is null)
        {
            if (ImGui.Button("URLからTL生成"))
            {
                this.StartFFLogsImport();
            }
        }
        else
        {
            ImGui.TextUnformatted("読み込み中...");
        }

        if (!string.IsNullOrWhiteSpace(this.fflogsImportStatus))
        {
            ImGui.Spacing();
            ImGui.PushTextWrapPos(0);
            ImGui.TextUnformatted(this.fflogsImportStatus);
            ImGui.PopTextWrapPos();
        }
    }

    private void StartFFLogsImport()
    {
        var settings = this.configuration.FFLogs;
        this.fflogsImportStatus = "FFLogsから読み込み中...";
        this.fflogsImportTask = FFLogsImportService.ImportTimelineAsync(new FFLogsImportRequest
        {
            ClientId = settings.ClientId,
            ClientSecret = settings.ClientSecret,
            AccessToken = settings.AccessToken,
            AccessTokenExpiresAtUtc = settings.AccessTokenExpiresAtUtc,
            ReportUrl = settings.ReportUrl,
            FightId = settings.FightId,
            LocalizedActionNames = this.actionIconService.GetLocalizedActionNames(),
        });
    }

    private void UpdateFFLogsImportTask()
    {
        var task = this.fflogsImportTask;
        if (task is null || !task.IsCompleted)
        {
            return;
        }

        try
        {
            var result = task.GetAwaiter().GetResult();
            this.ApplyFFLogsImportResult(result);
        }
        catch (Exception exception)
        {
            this.fflogsImportStatus = $"FFLogs読込に失敗しました: {exception.Message}";
        }
        finally
        {
            this.fflogsImportTask = null;
        }
    }

    private void ApplyFFLogsImportResult(FFLogsImportResult result)
    {
        if (!result.Success)
        {
            this.fflogsImportStatus = result.Message;
            return;
        }

        var settings = this.configuration.FFLogs;
        if (!string.IsNullOrWhiteSpace(result.AccessToken))
        {
            settings.AccessToken = result.AccessToken;
            settings.AccessTokenExpiresAtUtc = result.AccessTokenExpiresAtUtc;
        }

        settings.FightId = result.FightId;

        var plan = this.configuration.Plan;
        if (settings.ReplaceTimelineOnImport)
        {
            plan.Events = result.Events;
            this.selectedEventIndex = 0;
        }
        else
        {
            plan.Events.AddRange(result.Events);
            this.selectedEventIndex = Math.Clamp(this.selectedEventIndex, 0, Math.Max(0, plan.Events.Count - 1));
        }

        if (!string.IsNullOrWhiteSpace(result.FightName))
        {
            plan.ContentName = result.FightName;
        }

        plan.Revision = $"FFLogs fight {result.FightId}";
        plan.Normalize();
        this.configuration.Save();
        this.fflogsImportStatus = result.Message;
    }

    private void ClearTimeline()
    {
        this.configuration.Plan.Events.Clear();
        this.selectedEventIndex = 0;
        this.configuration.Save();
    }

    private void AddEvent()
    {
        var nextTime = this.configuration.Plan.Events.Count == 0
            ? 0
            : this.configuration.Plan.Events.Max(item => item.TimeSeconds) + 30;

        this.configuration.Plan.Events.Add(new TimelineEvent
        {
            Id = $"evt_{Guid.NewGuid():N}",
            TimeSeconds = nextTime,
            Name = "新規イベント",
            Type = TimelineEventType.Mechanic,
        });

        this.selectedEventIndex = this.configuration.Plan.Events.Count - 1;
        this.configuration.Save();
    }

    private static bool DrawEnumCombo<T>(string label, ref T value)
        where T : struct, Enum
    {
        var changed = false;
        if (ImGui.BeginCombo(label, DisplayText.EnumName(value)))
        {
            foreach (var candidate in Enum.GetValues<T>())
            {
                var selected = EqualityComparer<T>.Default.Equals(candidate, value);
                if (ImGui.Selectable(DisplayText.EnumName(candidate), selected))
                {
                    value = candidate;
                    changed = true;
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }

        return changed;
    }

    private static bool DrawJobCombo(string label, ref string job, IReadOnlyList<string> jobs)
    {
        var changed = false;
        if (string.IsNullOrWhiteSpace(job) || !jobs.Contains(job))
        {
            job = jobs.FirstOrDefault() ?? string.Empty;
            changed = true;
        }

        if (ImGui.BeginCombo(label, job))
        {
            foreach (var candidate in jobs)
            {
                var selected = candidate == job;
                if (ImGui.Selectable(candidate, selected))
                {
                    job = candidate;
                    changed = true;
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }

        return changed;
    }
}

using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.DutyState;
using Dalamud.Plugin.Services;
using RaidFlow.Windows;

namespace RaidFlow.Services;

public sealed class CombatSyncService : IDisposable
{
    private readonly Configuration configuration;
    private readonly PullTimerService pullTimer;
    private readonly OverlayWindow overlayWindow;
    private readonly ICondition condition;
    private readonly IDutyState dutyState;
    private readonly IFramework framework;

    private DateTimeOffset? pendingCombatEndResetAtUtc;

    public CombatSyncService(
        Configuration configuration,
        PullTimerService pullTimer,
        OverlayWindow overlayWindow,
        ICondition condition,
        IDutyState dutyState,
        IFramework framework)
    {
        this.configuration = configuration;
        this.pullTimer = pullTimer;
        this.overlayWindow = overlayWindow;
        this.condition = condition;
        this.dutyState = dutyState;
        this.framework = framework;

        this.condition.ConditionChange += this.OnConditionChange;
        this.dutyState.DutyWiped += this.OnDutyWiped;
        this.dutyState.DutyCompleted += this.OnDutyCompleted;
        this.framework.Update += this.OnFrameworkUpdate;
    }

    public string LastStatus { get; private set; } = "待機中。";

    public bool HasPendingCombatEndReset => this.pendingCombatEndResetAtUtc is not null;

    public float PendingCombatEndResetSeconds
    {
        get
        {
            if (this.pendingCombatEndResetAtUtc is null)
            {
                return 0;
            }

            var remaining = this.pendingCombatEndResetAtUtc.Value - DateTimeOffset.UtcNow;
            return Math.Max(0, (float)remaining.TotalSeconds);
        }
    }

    public void Dispose()
    {
        this.condition.ConditionChange -= this.OnConditionChange;
        this.dutyState.DutyWiped -= this.OnDutyWiped;
        this.dutyState.DutyCompleted -= this.OnDutyCompleted;
        this.framework.Update -= this.OnFrameworkUpdate;
    }

    private void OnConditionChange(ConditionFlag flag, bool value)
    {
        if (flag != ConditionFlag.InCombat)
        {
            return;
        }

        if (value)
        {
            this.pendingCombatEndResetAtUtc = null;
            this.AutoStartFromCombat();
            return;
        }

        this.ScheduleCombatEndReset();
    }

    private void OnDutyWiped(IDutyStateEventArgs args)
    {
        if (!this.configuration.Overlay.AutoResetOnDutyWipe)
        {
            return;
        }

        this.pendingCombatEndResetAtUtc = null;
        this.pullTimer.Reset();
        this.LastStatus = "ワイプ検知でタイマーをリセットしました。";
    }

    private void OnDutyCompleted(IDutyStateEventArgs args)
    {
        this.pendingCombatEndResetAtUtc = null;
        this.LastStatus = "コンテンツ完了。";
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (this.pendingCombatEndResetAtUtc is null)
        {
            return;
        }

        if (this.condition[ConditionFlag.InCombat])
        {
            this.pendingCombatEndResetAtUtc = null;
            this.LastStatus = "戦闘再開を検知したため、リセット待機をキャンセルしました。";
            return;
        }

        if (this.ShouldDelayCombatEndReset())
        {
            this.pendingCombatEndResetAtUtc = DateTimeOffset.UtcNow.AddSeconds(this.configuration.Overlay.CombatEndResetDelaySeconds);
            this.LastStatus = "戦闘終了を検知しましたが、カットシーン/エリア移動中のためリセットを延期しています。";
            return;
        }

        if (DateTimeOffset.UtcNow < this.pendingCombatEndResetAtUtc.Value)
        {
            return;
        }

        this.pendingCombatEndResetAtUtc = null;
        this.pullTimer.Reset();
        this.LastStatus = "戦闘終了後の保険リセットでタイマーをリセットしました。";
    }

    private void AutoStartFromCombat()
    {
        var settings = this.configuration.Overlay;
        if (!settings.AutoStartOnCombat)
        {
            this.LastStatus = "戦闘開始を検知しました。自動スタートは無効です。";
            return;
        }

        if (this.pullTimer.IsRunning || this.pullTimer.CurrentTimeSeconds > 0.05f)
        {
            this.LastStatus = "戦闘開始を検知しました。既存のタイマーを維持しました。";
            return;
        }

        this.pullTimer.StartFrom(settings.AutoStartOffsetSeconds);
        if (settings.AutoOpenOverlayOnCombatStart)
        {
            this.overlayWindow.IsOpen = true;
            settings.IsOpen = true;
            this.configuration.Save();
        }

        this.LastStatus = $"タイマーを {PlannerValidator.FormatTimestamp(this.pullTimer.CurrentTimeSeconds)} から自動スタートしました。";
    }

    private void ScheduleCombatEndReset()
    {
        var settings = this.configuration.Overlay;
        if (!settings.AutoResetAfterCombatEnd)
        {
            this.LastStatus = "戦闘終了を検知しました。戦闘終了後リセットは無効です。";
            return;
        }

        var delay = Math.Max(0, settings.CombatEndResetDelaySeconds);
        this.pendingCombatEndResetAtUtc = DateTimeOffset.UtcNow.AddSeconds(delay);
        this.LastStatus = $"戦闘終了を検知しました。{delay:0}秒後にリセット予定です。";
    }

    private bool ShouldDelayCombatEndReset()
    {
        if (!this.configuration.Overlay.DelayCombatEndResetDuringCutscene)
        {
            return false;
        }

        return this.condition.Any(
            ConditionFlag.WatchingCutscene,
            ConditionFlag.WatchingCutscene78,
            ConditionFlag.OccupiedInCutSceneEvent,
            ConditionFlag.BetweenAreas,
            ConditionFlag.BetweenAreas51);
    }
}

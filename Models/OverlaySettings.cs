namespace RaidFlow.Models;

public sealed class OverlaySettings
{
    public bool IsOpen { get; set; }

    public bool LockOverlay { get; set; }

    public bool ClickThroughWhenLocked { get; set; }

    public bool ShowOnlySelectedSlot { get; set; } = true;

    public bool AlwaysShowRaidMitigation { get; set; } = true;

    public bool ShowNotes { get; set; } = true;

    public bool AutoStartOnCombat { get; set; }

    public bool AutoOpenOverlayOnCombatStart { get; set; } = true;

    public bool AutoResetOnDutyWipe { get; set; } = true;

    public bool AutoResetAfterCombatEnd { get; set; }

    public bool DelayCombatEndResetDuringCutscene { get; set; } = true;

    public float AutoStartOffsetSeconds { get; set; }

    public float CombatEndResetDelaySeconds { get; set; } = 10;

    public int EventCount { get; set; } = 4;

    public float LookAheadSeconds { get; set; } = 120;

    public float RecentSeconds { get; set; } = 8;

    public float BackgroundAlpha { get; set; } = 0.82f;
}

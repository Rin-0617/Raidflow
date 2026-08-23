namespace RaidFlow.Models;

public enum PartySlot
{
    MT,
    ST,
    H1,
    H2,
    D1,
    D2,
    D3,
    D4,
}

public enum PartyRole
{
    Tank,
    Healer,
    Melee,
    PhysicalRanged,
    Caster,
}

public sealed class PartyMemberProfile
{
    public PartySlot Slot { get; set; }

    public string PlayerName { get; set; } = string.Empty;

    public string Job { get; set; } = string.Empty;
}

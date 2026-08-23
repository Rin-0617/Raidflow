using RaidFlow.Models;

namespace RaidFlow.Services;

public static class DisplayText
{
    public static string TimelineEventTypeName(TimelineEventType type)
    {
        return type switch
        {
            TimelineEventType.Raidwide => "全体攻撃",
            TimelineEventType.Tankbuster => "タンク強攻撃",
            TimelineEventType.Stack => "頭割り",
            TimelineEventType.Spread => "散開",
            TimelineEventType.Mechanic => "ギミック",
            TimelineEventType.Downtime => "履行/離脱",
            TimelineEventType.Burst => "バースト",
            _ => type.ToString(),
        };
    }

    public static string PartyRoleName(PartyRole role)
    {
        return role switch
        {
            PartyRole.Tank => "タンク",
            PartyRole.Healer => "ヒーラー",
            PartyRole.Melee => "近接",
            PartyRole.PhysicalRanged => "レンジ",
            PartyRole.Caster => "キャスター",
            _ => role.ToString(),
        };
    }

    public static string EnumName<T>(T value)
        where T : struct, Enum
    {
        if (value is TimelineEventType timelineEventType)
        {
            return TimelineEventTypeName(timelineEventType);
        }

        return value.ToString();
    }
}

using RaidFlow.Models;

namespace RaidFlow.Data;

public static class MitigationCatalog
{
    private static readonly IReadOnlyList<MitigationActionDefinition> Actions =
    [
        Action(7531, "ランパート", "ランパ", "TANK", PartyRole.Tank, 20, 90, false, requiredLevel: 8),
        Action(7535, "リプライザル", "リプ", "TANK", PartyRole.Tank, 15, 60, true, requiredLevel: 98, variants:
        [
            Variant(7535, "リプライザル", "リプ", 22, duration: 10),
        ]),

        Action(30, "インビンシブル", "インビン", "PLD", PartyRole.Tank, 10, 420, false, -1, requiredLevel: 50),
        Action(36920, "エクストリームガード", "EXガード", "PLD", PartyRole.Tank, 15, 120, false, requiredLevel: 92, variants:
        [
            Variant(17, "センチネル", "センチ", 38),
        ]),
        Action(3540, "ディヴァインヴェール", "ヴェール", "PLD", PartyRole.Tank, 30, 90, true, requiredLevel: 56),
        Action(7385, "パッセージ・オブ・アームズ", "パッセ", "PLD", PartyRole.Tank, 18, 120, true, requiredLevel: 70),
        Action(25746, "ホーリーシェルトロン", "シェル", "PLD", PartyRole.Tank, 8, 5, false, -2, requiredLevel: 82, variants:
        [
            Variant(3542, "シェルトロン", "シェル", 35, duration: 6),
        ]),

        Action(43, "ホルムギャング", "ホルム", "WAR", PartyRole.Tank, 10, 240, false, -1, requiredLevel: 42),
        Action(36923, "ダムネーション", "ダムネ", "WAR", PartyRole.Tank, 15, 120, false, requiredLevel: 92, variants:
        [
            Variant(44, "ヴェンジェンス", "ヴェンジ", 38),
        ]),
        Action(25751, "原初の血気", "血気", "WAR", PartyRole.Tank, 8, 25, false, -2, requiredLevel: 82, variants:
        [
            Variant(3551, "原初の直感", "直感", 56, duration: 6),
        ]),
        Action(7388, "シェイクオフ", "シェイク", "WAR", PartyRole.Tank, 30, 90, true, requiredLevel: 68),

        Action(3638, "リビングデッド", "リビデ", "DRK", PartyRole.Tank, 10, 300, false, -1, requiredLevel: 50),
        Action(36927, "シャドウヴィジル", "ヴィジル", "DRK", PartyRole.Tank, 15, 120, false, requiredLevel: 92, variants:
        [
            Variant(3636, "シャドウウォール", "ウォール", 38),
        ]),
        Action(7393, "ブラックナイト", "ブラナイ", "DRK", PartyRole.Tank, 7, 15, false, -2, requiredLevel: 70),
        Action(16471, "ダークミッショナリー", "ミッショ", "DRK", PartyRole.Tank, 15, 90, true, requiredLevel: 66),

        Action(16152, "ボーライド", "ボーライド", "GNB", PartyRole.Tank, 10, 360, false, -1, requiredLevel: 50),
        Action(36935, "グレートネビュラ", "Gネビュラ", "GNB", PartyRole.Tank, 15, 120, false, requiredLevel: 92, variants:
        [
            Variant(16148, "ネビュラ", "ネビュラ", 38),
        ]),
        Action(25758, "ハート・オブ・コランダム", "コランダム", "GNB", PartyRole.Tank, 8, 25, false, -2, requiredLevel: 82, variants:
        [
            Variant(16161, "ハート・オブ・ストーン", "ストーン", 68, duration: 7),
        ]),
        Action(16160, "ハート・オブ・ライト", "ライト", "GNB", PartyRole.Tank, 15, 90, true, requiredLevel: 64),

        Action(7549, "牽制", "牽制", "MELEE", PartyRole.Melee, 15, 90, true, requiredLevel: 98, variants:
        [
            Variant(7549, "牽制", "牽制", 22, duration: 10),
        ]),
        Action(7498, "心眼", "心眼", "SAM", PartyRole.Melee, 4, 15, false, -1, requiredLevel: 6),
        Action(2241, "残影", "残影", "NIN", PartyRole.Melee, 20, 120, false, requiredLevel: 2),
        Action(65, "マントラ", "マントラ", "MNK", PartyRole.Melee, 15, 90, true, requiredLevel: 42),
        Action(24404, "アルケインクレスト", "クレスト", "RPR", PartyRole.Melee, 5, 30, false, requiredLevel: 40),

        Action(16889, "タクティシャン", "タクティ", "MCH", PartyRole.PhysicalRanged, 15, 90, true, requiredLevel: 88, variants:
        [
            Variant(16889, "タクティシャン", "タクティ", 56, cooldown: 120),
        ]),
        Action(7405, "トルバドゥール", "トルバ", "BRD", PartyRole.PhysicalRanged, 15, 90, true, requiredLevel: 88, variants:
        [
            Variant(7405, "トルバドゥール", "トルバ", 62, cooldown: 120),
        ]),
        Action(7408, "地神のミンネ", "ミンネ", "BRD", PartyRole.PhysicalRanged, 15, 120, true, requiredLevel: 66),
        Action(16012, "守りのサンバ", "サンバ", "DNC", PartyRole.PhysicalRanged, 15, 90, true, requiredLevel: 88, variants:
        [
            Variant(16012, "守りのサンバ", "サンバ", 56, cooldown: 120),
        ]),

        Action(7560, "アドル", "アドル", "CASTER", PartyRole.Caster, 15, 90, true, requiredLevel: 98, variants:
        [
            Variant(7560, "アドル", "アドル", 8, duration: 10),
        ]),
        Action(25857, "バマジク", "バマジク", "RDM", PartyRole.Caster, 10, 120, true, requiredLevel: 86),

        Action(25861, "アクアヴェール", "アクア", "WHM", PartyRole.Healer, 8, 60, false, -2, requiredLevel: 86),
        Action(25862, "リタージー・オブ・ベル", "ベル", "WHM", PartyRole.Healer, 20, 180, true, requiredLevel: 90),
        Action(16536, "テンパランス", "テンパ", "WHM", PartyRole.Healer, 20, 120, true, requiredLevel: 80),

        Action(188, "野戦治療の陣", "陣", "SCH", PartyRole.Healer, 15, 30, true, requiredLevel: 50),
        Action(3585, "展開戦術", "展開", "SCH", PartyRole.Healer, 30, 90, true, requiredLevel: 88, variants:
        [
            Variant(3585, "展開戦術", "展開", 56, cooldown: 120),
        ]),
        Action(16538, "フェイルミネーション", "イルミ", "SCH", PartyRole.Healer, 20, 120, true, requiredLevel: 40),
        Action(25868, "疾風怒濤の計", "疾風", "SCH", PartyRole.Healer, 10, 120, true, requiredLevel: 90),

        Action(3613, "運命の輪", "輪", "AST", PartyRole.Healer, 18, 60, true, requiredLevel: 58),
        Action(16556, "星天交差", "交差", "AST", PartyRole.Healer, 30, 30, false, -2, maxCharges: 2, requiredLevel: 88, variants:
        [
            Variant(16556, "星天交差", "交差", 74, maxCharges: 1),
        ]),
        Action(25873, "エクザルテーション", "エグザ", "AST", PartyRole.Healer, 8, 60, false, -2, requiredLevel: 86),
        Action(25874, "マクロコスモス", "マクロ", "AST", PartyRole.Healer, 15, 180, true, requiredLevel: 90),

        Action(24298, "ケーラコレ", "ケーラ", "SGE", PartyRole.Healer, 15, 30, true, requiredLevel: 50),
        Action(24310, "ホーリズム", "ホーリズム", "SGE", PartyRole.Healer, 20, 120, true, requiredLevel: 76),
        Action(24311, "パンハイマ", "パンハ", "SGE", PartyRole.Healer, 15, 120, true, requiredLevel: 80),
        Action(24303, "タウロコレ", "タウロ", "SGE", PartyRole.Healer, 15, 45, false, -2, requiredLevel: 62),
    ];

    private static readonly IReadOnlyDictionary<PartyRole, IReadOnlyList<string>> JobsByRole =
        new Dictionary<PartyRole, IReadOnlyList<string>>
        {
            [PartyRole.Tank] = ["PLD", "WAR", "DRK", "GNB"],
            [PartyRole.Healer] = ["WHM", "SCH", "AST", "SGE"],
            [PartyRole.Melee] = ["MNK", "DRG", "NIN", "SAM", "RPR", "VPR"],
            [PartyRole.PhysicalRanged] = ["BRD", "MCH", "DNC"],
            [PartyRole.Caster] = ["BLM", "SMN", "RDM", "PCT"],
        };

    private static readonly IReadOnlyList<string> DpsJobs =
    [
        "MNK", "DRG", "NIN", "SAM", "RPR", "VPR",
        "BRD", "MCH", "DNC",
        "BLM", "SMN", "RDM", "PCT",
    ];

    public static IReadOnlyList<MitigationActionDefinition> AllActions => Actions;

    public static IReadOnlyList<string> JobsForSlot(PartySlot slot)
    {
        if (slot == PartySlot.D2)
        {
            return DpsJobs;
        }

        return SuggestedRole(slot) switch
        {
            PartyRole.Tank => JobsByRole[PartyRole.Tank],
            PartyRole.Healer => JobsByRole[PartyRole.Healer],
            PartyRole.Melee => JobsByRole[PartyRole.Melee],
            PartyRole.PhysicalRanged => JobsByRole[PartyRole.PhysicalRanged],
            PartyRole.Caster => JobsByRole[PartyRole.Caster],
            _ => JobsByRole[PartyRole.Melee],
        };
    }

    public static PartyRole SuggestedRole(PartySlot slot)
    {
        return slot switch
        {
            PartySlot.MT or PartySlot.ST => PartyRole.Tank,
            PartySlot.H1 or PartySlot.H2 => PartyRole.Healer,
            PartySlot.D3 => PartyRole.PhysicalRanged,
            PartySlot.D4 => PartyRole.Caster,
            _ => PartyRole.Melee,
        };
    }

    public static IReadOnlyList<MitigationActionDefinition> ActionsForJob(string job)
    {
        return ActionsForJob(job, 100);
    }

    public static IReadOnlyList<MitigationActionDefinition> ActionsForJob(string job, int contentLevel)
    {
        var role = RoleForJob(job);
        return Actions
            .Where(action => action.Job == job || action.Job == role.ToString().ToUpperInvariant())
            .Where(action => IsAvailableAtLevel(action, contentLevel))
            .Select(action => ResolveForLevel(action, contentLevel))
            .OrderByDescending(action => action.IsRaidMitigation)
            .ThenBy(action => action.CooldownSeconds)
            .ThenBy(action => action.Name)
            .ToList();
    }

    public static MitigationActionDefinition? FindAction(uint actionId)
    {
        return FindBaseAction(actionId);
    }

    public static MitigationActionDefinition? FindAction(uint actionId, int contentLevel)
    {
        var baseAction = FindBaseAction(actionId);
        return baseAction is null ? null : ResolveForLevel(baseAction, contentLevel);
    }

    public static int RequiredLevelForAction(uint actionId)
    {
        var baseAction = FindBaseAction(actionId);
        if (baseAction is null)
        {
            return 1;
        }

        return new[] { baseAction.RequiredLevel }
            .Concat(baseAction.LevelVariants.Select(variant => variant.RequiredLevel))
            .Min();
    }

    public static PartyRole RoleForJob(string job)
    {
        foreach (var (role, jobs) in JobsByRole)
        {
            if (jobs.Contains(job))
            {
                return role;
            }
        }

        return PartyRole.Melee;
    }

    private static MitigationActionDefinition? FindBaseAction(uint actionId)
    {
        return Actions.FirstOrDefault(action =>
            action.ActionId == actionId ||
            action.LevelVariants.Any(variant => variant.ActionId == actionId));
    }

    private static bool IsAvailableAtLevel(MitigationActionDefinition action, int contentLevel)
    {
        return contentLevel >= action.RequiredLevel ||
               action.LevelVariants.Any(variant => contentLevel >= variant.RequiredLevel);
    }

    private static MitigationActionDefinition ResolveForLevel(MitigationActionDefinition action, int contentLevel)
    {
        if (contentLevel >= action.RequiredLevel)
        {
            return action;
        }

        var variant = action.LevelVariants
            .Where(variant => contentLevel >= variant.RequiredLevel)
            .OrderByDescending(variant => variant.RequiredLevel)
            .FirstOrDefault();

        if (variant is null)
        {
            return action;
        }

        return new MitigationActionDefinition
        {
            ActionId = variant.ActionId,
            CanonicalActionId = action.CanonicalActionId,
            Name = variant.Name,
            ShortName = variant.ShortName,
            Job = action.Job,
            Role = action.Role,
            DurationSeconds = variant.DurationSeconds ?? action.DurationSeconds,
            CooldownSeconds = variant.CooldownSeconds ?? action.CooldownSeconds,
            IsRaidMitigation = action.IsRaidMitigation,
            DefaultUseOffsetSeconds = variant.DefaultUseOffsetSeconds ?? action.DefaultUseOffsetSeconds,
            MaxCharges = variant.MaxCharges ?? action.MaxCharges,
            RequiredLevel = variant.RequiredLevel,
            LevelVariants = action.LevelVariants,
        };
    }

    private static MitigationActionDefinition Action(
        uint actionId,
        string name,
        string shortName,
        string job,
        PartyRole role,
        float duration,
        float cooldown,
        bool isRaidMitigation,
        float defaultUseOffset = -3,
        int maxCharges = 1,
        int requiredLevel = 1,
        IReadOnlyList<MitigationActionVariant>? variants = null)
    {
        return new MitigationActionDefinition
        {
            ActionId = actionId,
            CanonicalActionId = actionId,
            Name = name,
            ShortName = shortName,
            Job = job,
            Role = role,
            DurationSeconds = duration,
            CooldownSeconds = cooldown,
            IsRaidMitigation = isRaidMitigation,
            DefaultUseOffsetSeconds = defaultUseOffset,
            MaxCharges = maxCharges,
            RequiredLevel = requiredLevel,
            LevelVariants = variants ?? [],
        };
    }

    private static MitigationActionVariant Variant(
        uint actionId,
        string name,
        string shortName,
        int requiredLevel,
        float? duration = null,
        float? cooldown = null,
        float? defaultUseOffset = null,
        int? maxCharges = null)
    {
        return new MitigationActionVariant
        {
            ActionId = actionId,
            Name = name,
            ShortName = shortName,
            RequiredLevel = requiredLevel,
            DurationSeconds = duration,
            CooldownSeconds = cooldown,
            DefaultUseOffsetSeconds = defaultUseOffset,
            MaxCharges = maxCharges,
        };
    }
}

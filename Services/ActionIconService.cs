using Dalamud.Game;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;
using RaidFlow.Models;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace RaidFlow.Services;

public sealed class ActionIconService
{
    private readonly IDataManager dataManager;
    private readonly ITextureProvider textureProvider;
    private readonly Dictionary<uint, ActionGameData> actionCache = [];
    private readonly Dictionary<uint, ISharedImmediateTexture?> textureCache = [];
    private IReadOnlyDictionary<uint, string>? localizedActionNames;
    private IReadOnlyDictionary<string, string>? englishToLocalizedActionNames;

    public ActionIconService(IDataManager dataManager, ITextureProvider textureProvider)
    {
        this.dataManager = dataManager;
        this.textureProvider = textureProvider;
    }

    public string DisplayName(MitigationActionDefinition action)
    {
        var gameData = this.GetActionData(action.ActionId);
        return string.IsNullOrWhiteSpace(gameData.Name) ? action.Name : gameData.Name;
    }

    public IReadOnlyDictionary<uint, string> GetLocalizedActionNames()
    {
        if (this.localizedActionNames is not null)
        {
            return this.localizedActionNames;
        }

        var names = new Dictionary<uint, string>();
        this.AddActionNames(names, ClientLanguage.Japanese, overwrite: true);
        if (names.Count == 0)
        {
            this.AddActionNames(names, null, overwrite: true);
        }

        this.localizedActionNames = names;
        return this.localizedActionNames;
    }

    public IReadOnlyDictionary<string, string> GetEnglishToLocalizedActionNames()
    {
        if (this.englishToLocalizedActionNames is not null)
        {
            return this.englishToLocalizedActionNames;
        }

        var localizedNames = this.GetLocalizedActionNames();
        var englishNames = new Dictionary<uint, string>();
        this.AddActionNames(englishNames, ClientLanguage.English, overwrite: true);

        var mappedNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (actionId, englishName) in englishNames)
        {
            if (string.IsNullOrWhiteSpace(englishName) ||
                !localizedNames.TryGetValue(actionId, out var localizedName) ||
                string.IsNullOrWhiteSpace(localizedName) ||
                string.Equals(englishName, localizedName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            AddEnglishToLocalizedActionName(mappedNames, englishName.Trim(), localizedName);
            AddEnglishToLocalizedActionName(
                mappedNames,
                FFLogsNameResolver.NormalizeAbilityNameLookupKey(englishName),
                localizedName);
        }

        this.englishToLocalizedActionNames = mappedNames;
        return this.englishToLocalizedActionNames;
    }

    private static void AddEnglishToLocalizedActionName(
        Dictionary<string, string> mappedNames,
        string englishName,
        string localizedName)
    {
        if (string.IsNullOrWhiteSpace(englishName) || mappedNames.ContainsKey(englishName))
        {
            return;
        }

        mappedNames[englishName] = localizedName;
    }

    private void AddActionNames(Dictionary<uint, string> names, ClientLanguage? language, bool overwrite)
    {
        try
        {
            var sheet = this.dataManager.GetExcelSheet<LuminaAction>(language);
            if (sheet is null)
            {
                return;
            }

            foreach (var action in sheet)
            {
                var name = action.Name.ToString();
                if (action.RowId == 0 || string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var actionId = (uint)action.RowId;
                if (overwrite || !names.ContainsKey(actionId))
                {
                    names[actionId] = name;
                }
            }
        }
        catch
        {
            if (overwrite)
            {
                names.Clear();
            }
        }
    }

    public bool TryGetIcon(MitigationActionDefinition action, out ISharedImmediateTexture texture)
    {
        texture = null!;
        var iconId = this.GetActionData(action.ActionId).IconId;
        if (iconId == 0)
        {
            return false;
        }

        if (!this.textureCache.TryGetValue(iconId, out var cachedTexture))
        {
            var lookup = new GameIconLookup(iconId, false, true);
            cachedTexture = this.textureProvider.GetFromGameIcon(lookup);
            this.textureCache[iconId] = cachedTexture;
        }

        if (cachedTexture is null)
        {
            return false;
        }

        texture = cachedTexture;
        return true;
    }

    private ActionGameData GetActionData(uint actionId)
    {
        if (this.actionCache.TryGetValue(actionId, out var cached))
        {
            return cached;
        }

        var data = ActionGameData.Empty;
        try
        {
            var sheet = this.dataManager.GetExcelSheet<LuminaAction>();
            var row = sheet?.GetRow(actionId);
            if (row is not null)
            {
                var action = row.Value;
                data = new ActionGameData((uint)action.Icon, action.Name.ToString());
            }
        }
        catch
        {
            data = ActionGameData.Empty;
        }

        this.actionCache[actionId] = data;
        return data;
    }

    private sealed record ActionGameData(uint IconId, string Name)
    {
        public static readonly ActionGameData Empty = new(0, string.Empty);
    }
}

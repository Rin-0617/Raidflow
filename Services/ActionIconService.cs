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

using Dalamud.Configuration;
using Dalamud.Plugin;
using RaidFlow.Models;

namespace RaidFlow;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    public int Version { get; set; } = 1;

    public RaidFlowDocument Plan { get; set; } = RaidFlowDocument.CreateDefault();

    public PartySlot SelectedSlot { get; set; } = PartySlot.MT;

    public OverlaySettings Overlay { get; set; } = new();

    public FFLogsSettings FFLogs { get; set; } = new();

    public string ImportBuffer { get; set; } = string.Empty;

    public string ExportBuffer { get; set; } = string.Empty;

    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
        this.Overlay ??= new OverlaySettings();
        this.FFLogs ??= new FFLogsSettings();
        if (this.Plan.Normalize())
        {
            this.Save();
        }
    }

    public void Save()
    {
        this.Plan.UpdatedAtUtc = DateTimeOffset.UtcNow;
        this.pluginInterface?.SavePluginConfig(this);
    }
}

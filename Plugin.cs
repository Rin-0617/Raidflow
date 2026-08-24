using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using RaidFlow.Services;
using RaidFlow.Windows;

namespace RaidFlow;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/raidflow";

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commandManager;
    private readonly Configuration configuration;
    private readonly PullTimerService pullTimer = new();
    private readonly WindowSystem windowSystem = new("RaidFlow");
    private readonly MainWindow mainWindow;
    private readonly OverlayWindow overlayWindow;
    private readonly CombatSyncService combatSyncService;
    private readonly ActionIconService actionIconService;

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        ICondition condition,
        IDutyState dutyState,
        IFramework framework,
        IDataManager dataManager,
        ITextureProvider textureProvider)
    {
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;

        this.configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        this.configuration.Initialize(pluginInterface);

        this.actionIconService = new ActionIconService(dataManager, textureProvider);
        this.overlayWindow = new OverlayWindow(this.configuration, this.pullTimer, this.actionIconService);
        this.combatSyncService = new CombatSyncService(
            this.configuration,
            this.pullTimer,
            this.overlayWindow,
            condition,
            dutyState,
            framework);
        this.mainWindow = new MainWindow(this.configuration, this.pullTimer, this.overlayWindow, this.combatSyncService, this.actionIconService);
        this.windowSystem.AddWindow(this.mainWindow);
        this.windowSystem.AddWindow(this.overlayWindow);

        this.pluginInterface.UiBuilder.Draw += this.DrawUi;
        this.pluginInterface.UiBuilder.OpenMainUi += this.OpenOverlayUi;
        this.pluginInterface.UiBuilder.OpenConfigUi += this.OpenMainUi;

        this.commandManager.AddHandler(CommandName, new CommandInfo(this.OnCommand)
        {
            HelpMessage = "RaidFlowを開きます。サブコマンド: overlay, start, pause, resume, reset, autosync",
        });
    }

    public void Dispose()
    {
        this.commandManager.RemoveHandler(CommandName);
        this.combatSyncService.Dispose();

        this.pluginInterface.UiBuilder.Draw -= this.DrawUi;
        this.pluginInterface.UiBuilder.OpenMainUi -= this.OpenOverlayUi;
        this.pluginInterface.UiBuilder.OpenConfigUi -= this.OpenMainUi;

        this.windowSystem.RemoveAllWindows();
    }

    private void DrawUi()
    {
        this.windowSystem.Draw();
    }

    private void OpenMainUi()
    {
        this.mainWindow.IsOpen = true;
    }

    private void OpenOverlayUi()
    {
        this.overlayWindow.IsOpen = true;
        this.configuration.Overlay.IsOpen = true;
        this.configuration.Save();
    }

    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "overlay":
                this.overlayWindow.Toggle();
                this.configuration.Overlay.IsOpen = this.overlayWindow.IsOpen;
                this.configuration.Save();
                break;
            case "start":
                this.pullTimer.StartFrom();
                this.overlayWindow.IsOpen = true;
                this.configuration.Overlay.IsOpen = true;
                this.configuration.Save();
                break;
            case "pause":
                this.pullTimer.Pause();
                break;
            case "resume":
                this.pullTimer.Resume();
                break;
            case "reset":
                this.pullTimer.Reset();
                break;
            case "autosync":
                this.configuration.Overlay.AutoStartOnCombat = !this.configuration.Overlay.AutoStartOnCombat;
                this.configuration.Save();
                break;
            default:
                this.OpenMainUi();
                break;
        }
    }
}

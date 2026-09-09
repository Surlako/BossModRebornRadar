using Dalamud.Common;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System.Reflection;
using System.Threading;

[module: SkipLocalsInit]
namespace BossMod;

// Radar-only companion build of BossModReborn.
// Questionable remains connected exclusively to the original BossMod because this plugin
// intentionally exposes no BossMod IPC and owns no code path that can execute actions.
public sealed class Plugin : IAsyncDalamudPlugin
{
    public string Name => "BossMod Reborn Radar";

    private readonly IDalamudPluginInterface _dalamud;
    private readonly ICommandManager _commandManager;
    private readonly string _gameVersion = "unknown";

    private WorldState _ws = null!;
    private PassiveActionObserver _actionObserver = null!;
    private BossModuleManager _bossmod = null!;
    private ZoneModuleManager _zonemod = null!;
    private WorldStateGameSync _wsSync = null!;
    private PartyRolesManager _partyRoles = null!;
    private TimeSpan _prevUpdateTime;
    private WorldOverlayNode? _worldOverlayNode;

    private ConfigUI _configUI = null!;
    private BossModuleMainWindow _wndBossmod = null!;
    private BossModuleHintsWindow _wndBossmodHints = null!;

    public Plugin(IDalamudPluginInterface dalamud, ICommandManager commandManager, ISigScanner sigScanner, IDataManager dataManager)
    {
        _dalamud = dalamud;
        _commandManager = commandManager;

        if (!dalamud.ConfigDirectory.Exists)
            dalamud.ConfigDirectory.Create();

        var dalamudRoot = dalamud.GetType().Assembly.
            GetType("Dalamud.Service`1", true)!.MakeGenericType(dalamud.GetType().Assembly.GetType("Dalamud.Dalamud", true)!).
            GetMethod("Get")!.Invoke(null, BindingFlags.Default, null, [], null);
        var dalamudStartInfo = dalamudRoot?.GetType().GetProperty("StartInfo", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(dalamudRoot) as DalamudStartInfo;
        _gameVersion = dalamudStartInfo?.GameVersion?.ToString() ?? "unknown";

        InteropGenerator.Runtime.Resolver.GetInstance.Setup(sigScanner.SearchBase, _gameVersion, new(dalamud.ConfigDirectory.FullName + "/cs.json"));
        FFXIVClientStructs.Interop.Generated.Addresses.Register();
        Dx11ArenaRenderer.Initialize(_dalamud.UiBuilder.DeviceHandle);
        dalamud.Create<Service>();
        Service.LogHandlerDebug = msg => Service.Logger.Debug(msg);
        Service.LogHandlerVerbose = msg => Service.Logger.Verbose(msg);
        Service.LuminaGameData = dataManager.GameData;
        Service.WindowSystem = new("bmrr");
    }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        await Task.Run(InteropGenerator.Runtime.Resolver.GetInstance.Resolve, cancellationToken);
        await Task.Run(() =>
        {
            Service.Config.Initialize();
            Service.Config.LoadFromFile(_dalamud.ConfigFile);
        }, cancellationToken);
        await Service.Framework.RunOnFrameworkThread(InitOnFrameworkThread);
    }

    private unsafe void InitOnFrameworkThread()
    {
        Service.Condition.ConditionChange += OnConditionChanged;
        Camera.Instance = new();
        _worldOverlayNode = new();
        Dx11ArenaRenderer.SetWorldOverlayNode(_worldOverlayNode);
        Service.Config.Modified.Subscribe(() => Task.Run(() => Service.Config.SaveToFile(_dalamud.ConfigFile)));

        _commandManager.AddHandler("/bmrr", new CommandInfo(OnCommand) { HelpMessage = "Show BossMod Reborn Radar settings" });

        var qpf = (ulong)FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance()->PerformanceCounterFrequency;
        _ws = new(qpf, _gameVersion);
        _actionObserver = new();
        _bossmod = new(_ws);
        _zonemod = new(_ws);
        _wsSync = new(_ws, _actionObserver);
        _partyRoles = new(_ws);

        // Defense in depth: these settings cannot enable automation because the corresponding
        // managers are not constructed, but keep persisted upstream defaults disabled as well.
        Service.Config.Get<BossModuleConfig>().AllowAutomaticActions = false;
        var zoneConfig = Service.Config.Get<ZoneModuleConfig>();
        zoneConfig.EnableQuestBattles = false;
        zoneConfig.ShowWaypoints = false;

        _wndBossmod = new(_bossmod, _zonemod);
        Service.BossModWindow = _wndBossmod;
        _wndBossmodHints = new(_bossmod, _zonemod);
        _configUI = new(Service.Config, _ws, null, null, true);

        _dalamud.UiBuilder.DisableAutomaticUiHide = true;
        _dalamud.UiBuilder.Draw += DrawUI;
        _dalamud.UiBuilder.OpenMainUi += OpenConfigUI;
        _dalamud.UiBuilder.OpenConfigUi += OpenConfigUI;
    }

    public async ValueTask DisposeAsync()
    {
        await Service.Framework.RunOnFrameworkThread(() =>
        {
            _dalamud.UiBuilder.Draw -= DrawUI;
            _dalamud.UiBuilder.OpenMainUi -= OpenConfigUI;
            _dalamud.UiBuilder.OpenConfigUi -= OpenConfigUI;
            Service.Condition.ConditionChange -= OnConditionChanged;
            Dx11ArenaRenderer.SetWorldOverlayNode(null);
            _worldOverlayNode?.Dispose();
            _worldOverlayNode = null;
            Dx11ArenaRenderer.Shutdown();
        });

        _wndBossmodHints.Dispose();
        _wndBossmod.Dispose();
        _configUI.Dispose();
        _partyRoles.Dispose();
        _wsSync.Dispose();
        _zonemod.Dispose();
        _bossmod.Dispose();
        _actionObserver.Dispose();
        _commandManager.RemoveHandler("/bmrr");
        GarbageCollection();
    }

    private void OnCommand(string cmd, string args)
    {
        Service.Log($"OnCommand: {cmd} {args}");
        var split = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (split.Length == 0)
        {
            OpenConfigUI();
            return;
        }

        switch (split[0].ToUpperInvariant())
        {
            case "GC":
                GarbageCollection();
                break;
            case "RESETCOLORS":
                ResetColors();
                break;
            case "RADAR":
                ToggleRadar(split);
                break;
            default:
                Service.ChatGui.PrintError($"[BMR Radar] Unknown command: {split[0]}");
                break;
        }
    }

    private void OpenConfigUI()
    {
        _ = new UISimpleWindow("BossModRebornRadar", _configUI.Draw, true, new(300, 300));
    }

    private void DrawUI()
    {
        var tsStart = DateTime.Now;
        Camera.Instance?.Update();
        _wsSync.Update(_prevUpdateTime);
        _partyRoles.Update();
        _bossmod.Update();

        var uiHidden = Service.GameGui.GameUiHidden || Service.Condition[ConditionFlag.OccupiedInCutSceneEvent] || Service.Condition[ConditionFlag.WatchingCutscene78] || Service.Condition[ConditionFlag.WatchingCutscene];
        UpdateScreenRiskBorder(uiHidden);
        if (!uiHidden)
            Service.WindowSystem?.Draw();

        Camera.Instance?.DrawWorldPrimitives();
        _prevUpdateTime = DateTime.Now - tsStart;
    }

    private void UpdateScreenRiskBorder(bool uiHidden)
    {
        var config = BossModuleManager.Config;
        var module = _bossmod.ActiveModule;
        var pc = _ws.Party[PartyState.PlayerSlot];
        var enabled = config.ShowScreenRiskBorder && !uiHidden && module != null && pc != null && !pc.IsDead;
        var haveRisks = false;
        if (enabled && config.ScreenRiskBorderIntensity > 0f)
        {
            var hints = module!.CalculateHintsForRaidMember(PartyState.PlayerSlot, pc!);
            var count = hints.Count;
            for (var i = 0; i < count; ++i)
            {
                if (hints[i].Item2)
                {
                    haveRisks = true;
                    break;
                }
            }
        }

        Camera.Instance?.UpdateScreenRiskBorder(enabled, haveRisks, Colors.Enemy, config.ScreenRiskBorderIntensity);
    }

    private static void ResetColors()
    {
        var defaultConfig = ColorConfig.DefaultConfig;
        var currentConfig = Service.Config.Get<ColorConfig>();
        foreach (var field in GeneratedConfigMetadata.Get<ColorConfig>().Fields)
        {
            var value = field.Getter(defaultConfig);
            if (value is Color or Color[])
                field.Setter(currentConfig, value);
        }
        currentConfig.Modified.Fire();
        Service.Log("Colors have been reset to default values.");
    }

    private static void OnConditionChanged(ConditionFlag flag, bool value) => Service.Log($"Condition change: {flag}={value}");

    public static void GarbageCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static bool ToggleRadar(string[] messageData)
    {
        var config = MiniArena.Config;
        if (messageData.Length == 1)
            config.EnableRadar = !config.EnableRadar;
        else
        {
            switch (messageData[1].ToUpperInvariant())
            {
                case "ON":
                    config.EnableRadar = true;
                    break;
                case "OFF":
                    config.EnableRadar = false;
                    break;
                case "RESET":
                    Service.BossModWindow?.RecenterWindow();
                    break;
                default:
                    Service.ChatGui.Print($"[BMR Radar] Unknown radar command: {messageData[1]}");
                    return false;
            }
        }

        config.Modified.Fire();
        Service.Log($"Radar is now {(config.EnableRadar ? "enabled" : "disabled")}");
        return true;
    }
}

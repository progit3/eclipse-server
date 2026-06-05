using Content.Server._Eclipse.ProtoCore.Components;
using Content.Server.AlertLevel;
using Content.Server.Audio;
using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Server.Power.NodeGroups;
using Content.Server.RoundEnd;
using Content.Server.Station.Systems;
using Content.Shared._Eclipse.ProtoCore;
using Content.Shared.Audio;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.GameTicking.Components;
using Content.Shared.Interaction;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Server._Eclipse.ProtoCore;

public sealed class ProtoCoreSystem : GameRuleSystem<AshLegionRuleComponent>
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly RoundEndSystem _roundEnd = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedBatterySystem _battery = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly AlertLevelSystem _alertLevel = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly ServerGlobalSoundSystem _sound = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;

    private readonly SoundSpecifier _beep = new SoundPathSpecifier("/Audio/Machines/quickbeep.ogg");
    private readonly SoundSpecifier _deny = new SoundPathSpecifier("/Audio/Machines/buzz-two.ogg");
    private readonly SoundSpecifier _breachAnnouncement = new SoundPathSpecifier("/Audio/Machines/warning_buzzer.ogg");
    private const float StorageEmptyThreshold = 1f;
    private float _consoleUiUpdateAccumulator;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ProtoCoreComponent, ExaminedEvent>(OnCoreExamined);
        SubscribeLocalEvent<ProtoCoreConsoleComponent, ComponentInit>(OnConsoleInit);
        SubscribeLocalEvent<ProtoCoreConsoleComponent, ComponentRemove>(OnConsoleRemove);
        SubscribeLocalEvent<ProtoCoreConsoleComponent, ExaminedEvent>(OnConsoleExamined);
        SubscribeLocalEvent<ProtoCoreConsoleComponent, InteractUsingEvent>(OnConsoleInteractUsing, before: [typeof(ItemSlotsSystem)]);
        SubscribeLocalEvent<ProtoCoreConsoleComponent, ItemSlotInsertAttemptEvent>(OnConsoleItemSlotInsertAttempt);
        SubscribeLocalEvent<ProtoCoreConsoleComponent, EntInsertedIntoContainerMessage>(OnConsoleItemSlotChanged);
        SubscribeLocalEvent<ProtoCoreConsoleComponent, EntRemovedFromContainerMessage>(OnConsoleItemSlotChanged);
        SubscribeLocalEvent<ProtoCoreConsoleComponent, ItemSlotEjectAttemptEvent>(OnConsoleItemSlotEjectAttempt);
        SubscribeLocalEvent<AshHackingDeviceComponent, InteractHandEvent>(OnDeviceInteractHand);
        SubscribeLocalEvent<ProtoCoreConsoleComponent, GetVerbsEvent<ActivationVerb>>(OnConsoleGetVerbs);
        SubscribeLocalEvent<ProtoCoreConsoleComponent, ProtoCoreInstallDeviceDoAfterEvent>(OnInstallDeviceDoAfter);
        SubscribeLocalEvent<ProtoCoreConsoleComponent, ProtoCoreCutDeviceDoAfterEvent>(OnCutDeviceDoAfter);
        SubscribeLocalEvent<ProtoCoreConsoleComponent, ProtoCoreStartDoAfterEvent>(OnStartDoAfter);
        SubscribeLocalEvent<ProtoCoreConsoleComponent, ProtoCoreStabilizeDoAfterEvent>(OnStabilizeDoAfter);
        SubscribeLocalEvent<ProtoCoreConsoleComponent, ProtoCorePhysicalOverrideDoAfterEvent>(OnPhysicalOverrideDoAfter);
        SubscribeLocalEvent<ProtoCoreConsoleComponent, DamageChangedEvent>(OnConsoleDamaged);

        Subs.BuiEvents<ProtoCoreConsoleComponent>(ProtoCoreConsoleUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnConsoleUiOpened);
            subs.Event<ProtoCoreConsoleActionMessage>(OnConsoleActionMessage);
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _consoleUiUpdateAccumulator += frameTime;
        var updateConsoleUi = _consoleUiUpdateAccumulator >= 1f;
        if (updateConsoleUi)
            _consoleUiUpdateAccumulator = 0f;

        var consoleQuery = EntityQueryEnumerator<ProtoCoreConsoleComponent>();
        while (consoleQuery.MoveNext(out var consoleUid, out var console))
        {
            if (!console.HackInProgress)
                continue;

            console.HackRemainingTime = MathF.Max(0f, console.HackRemainingTime - frameTime);

            if (console.HackRemainingTime <= 0f)
                FinishHack((consoleUid, console));
            else if (updateConsoleUi)
                UpdateConsoleUi((consoleUid, console));
        }

        var query = EntityQueryEnumerator<ProtoCoreComponent>();
        while (query.MoveNext(out var uid, out var core))
        {
            if (core.State is not (ProtoCoreState.Meltdown or ProtoCoreState.Critical))
                continue;

            // TODO: Replace this with a power-network/SMES check. The first pass keeps the timer deterministic
            // until the project decides how "station-wide power loss" should be measured.
            core.RemainingTime = MathF.Max(0f, core.RemainingTime - frameTime);
            CheckStorageDisconnectedDuringMeltdown((uid, core));

            if (core.State == ProtoCoreState.Meltdown && core.RemainingTime <= core.MeltdownMusicDuration)
                EnterWarningStage(uid, core);

            if (core.State == ProtoCoreState.Meltdown && core.RemainingTime <= core.CriticalThreshold)
                EnterCritical(uid, core);

            if (core.RemainingTime <= 0f)
                FinishMeltdown(uid, core);

            if (updateConsoleUi)
                UpdateConsoleUisForCore((uid, core));
        }
    }

    protected override void AppendRoundEndText(EntityUid uid, AshLegionRuleComponent component, GameRuleComponent gameRule, ref RoundEndTextAppendEvent args)
    {
        switch (component.Result)
        {
            case ProtoCoreState.Critical:
                args.AddLine(Loc.GetString("ash-legion-roundend-critical"));
                break;
            case ProtoCoreState.Stabilized:
                args.AddLine(Loc.GetString("ash-legion-roundend-stabilized"));
                break;
            case ProtoCoreState.Meltdown:
                args.AddLine(Loc.GetString("ash-legion-roundend-started"));
                break;
        }
    }

    private void OnCoreExamined(Entity<ProtoCoreComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("proto-core-examine-status",
            ("state", Loc.GetString($"proto-core-state-{ent.Comp.State.ToString().ToLowerInvariant()}")),
            ("time", FormatTime(ent.Comp.RemainingTime))));
    }

    private void OnConsoleExamined(Entity<ProtoCoreConsoleComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("proto-core-console-examine",
            ("authorized", IsAuthorized(ent)),
            ("device", IsHackingDeviceConnected(ent))));
    }

    private void OnConsoleInit(Entity<ProtoCoreConsoleComponent> ent, ref ComponentInit args)
    {
        _itemSlots.AddItemSlot(ent, ProtoCoreConsoleUi.ActivationKeySlotId, ent.Comp.ActivationKeySlot);
        _itemSlots.AddItemSlot(ent, ProtoCoreConsoleUi.HackingDeviceSlotId, ent.Comp.HackingDeviceSlot);
        UpdateHackingDeviceVisual(ent);
    }

    private void OnConsoleRemove(Entity<ProtoCoreConsoleComponent> ent, ref ComponentRemove args)
    {
        _itemSlots.RemoveItemSlot(ent, ent.Comp.ActivationKeySlot);
        _itemSlots.RemoveItemSlot(ent, ent.Comp.HackingDeviceSlot);
    }

    private void OnConsoleItemSlotChanged(Entity<ProtoCoreConsoleComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        OnConsoleItemSlotChanged(ent, args);
    }

    private void OnConsoleItemSlotChanged(Entity<ProtoCoreConsoleComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        OnConsoleItemSlotChanged(ent, args);
    }

    private void OnConsoleItemSlotChanged(Entity<ProtoCoreConsoleComponent> ent, ContainerModifiedMessage args)
    {
        if (args.Container.ID == ent.Comp.ActivationKeySlot.ID)
        {
            UpdateConsoleUi(ent);
            _audio.PlayPvs(_beep, ent);
            return;
        }

        if (args.Container.ID != ent.Comp.HackingDeviceSlot.ID)
            return;

        if (ent.Comp.HackingDeviceSlot.HasItem)
        {
            StartHack(ent, args);
        }
        else
        {
            if (args is EntRemovedFromContainerMessage removed &&
                TryComp<AshHackingDeviceComponent>(removed.Entity, out var removedDevice))
                removedDevice.Locked = false;

            DisconnectHackingDevice(ent);
        }

        UpdateHackingDeviceVisual(ent);
        UpdateConsoleUi(ent);
        _audio.PlayPvs(_beep, ent);
    }

    private void OnConsoleInteractUsing(Entity<ProtoCoreConsoleComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp<ToolComponent>(args.Used, out var tool) &&
            _tool.HasQuality(args.Used, SharedToolSystem.CutQuality, tool) &&
            ent.Comp.HackingDeviceSlot.HasItem)
        {
            args.Handled = _tool.UseTool(
                args.Used,
                args.User,
                ent.Owner,
                ent.Comp.CutDeviceTime,
                SharedToolSystem.CutQuality,
                new ProtoCoreCutDeviceDoAfterEvent(),
                toolComponent: tool);
            return;
        }

        if (!HasComp<AshHackingDeviceComponent>(args.Used) ||
            ent.Comp.HackingDeviceSlot.HasItem ||
            ent.Comp.HackInProgress ||
            ent.Comp.InstallingHackingDevice)
            return;

        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.InstallDeviceTime, new ProtoCoreInstallDeviceDoAfterEvent(), ent, target: ent, used: args.Used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
        {
            args.Handled = true;
            _popup.PopupEntity(Loc.GetString("proto-core-console-device-install-start"), ent, args.User);
        }
    }

    private void OnConsoleItemSlotInsertAttempt(Entity<ProtoCoreConsoleComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Slot.ID == ent.Comp.HackingDeviceSlot.ID && !ent.Comp.InstallingHackingDevice)
            args.Cancelled = true;
    }

    private void OnConsoleItemSlotEjectAttempt(Entity<ProtoCoreConsoleComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        if (args.Slot.ID != ent.Comp.HackingDeviceSlot.ID ||
            !IsHackingDeviceConnected(ent) ||
            ent.Comp.ForceDeviceEjectInProgress)
            return;

        args.Cancelled = true;
        if (args.User is { } user)
            _popup.PopupEntity(Loc.GetString("ash-hacking-device-locked"), ent, user);
    }

    private void OnDeviceInteractHand(Entity<AshHackingDeviceComponent> ent, ref InteractHandEvent args)
    {
        if (!ent.Comp.Locked)
            return;

        _popup.PopupEntity(Loc.GetString("ash-hacking-device-locked"), ent, args.User);
        args.Handled = true;
    }

    private void StartHack(Entity<ProtoCoreConsoleComponent> ent, ContainerModifiedMessage args)
    {
        if (ent.Comp.DeviceConnected || ent.Comp.HackInProgress)
            return;

        if (args is not EntInsertedIntoContainerMessage inserted ||
            !TryComp<AshHackingDeviceComponent>(inserted.Entity, out var device))
            return;

        ent.Comp.HackInProgress = true;
        ent.Comp.HackRemainingTime = ent.Comp.HackTime;
        device.Locked = true;
        UpdateConsoleUi(ent);
        _popup.PopupEntity(Loc.GetString("proto-core-console-hack-start"), ent);
    }

    private void OnInstallDeviceDoAfter(Entity<ProtoCoreConsoleComponent> ent, ref ProtoCoreInstallDeviceDoAfterEvent args)
    {
        if (args.Cancelled ||
            args.Used is not { } device ||
            ent.Comp.HackingDeviceSlot.HasItem ||
            !HasComp<AshHackingDeviceComponent>(device))
            return;

        ent.Comp.InstallingHackingDevice = true;
        _itemSlots.TryInsert(ent, ent.Comp.HackingDeviceSlot, device, args.User);
        ent.Comp.InstallingHackingDevice = false;
    }

    private void OnCutDeviceDoAfter(Entity<ProtoCoreConsoleComponent> ent, ref ProtoCoreCutDeviceDoAfterEvent args)
    {
        if (args.Cancelled || !ent.Comp.HackingDeviceSlot.HasItem)
            return;

        DisconnectHackingDevice(ent);
        UpdateHackingDeviceVisual(ent);
        UpdateConsoleUi(ent);
        _itemSlots.TryEjectToHands(ent, ent.Comp.HackingDeviceSlot, args.User, true);
        _popup.PopupEntity(Loc.GetString("proto-core-console-device-disconnected"), ent, args.User);
    }

    private void OnConsoleGetVerbs(Entity<ProtoCoreConsoleComponent> ent, ref GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        var user = args.User;

        if (TryFindCore(ent, out var core) && IsAuthorized(ent) && IsHackingDeviceConnected(ent) && core.Comp.State is ProtoCoreState.Hacked or ProtoCoreState.Idle)
        {
            args.Verbs.Add(new ActivationVerb
            {
                Text = Loc.GetString("proto-core-verb-start"),
                Act = () => StartDoAfter(ent, user, new ProtoCoreStartDoAfterEvent(), ent.Comp.StartTime),
            });
        }

        if (TryFindCore(ent, out core) && core.Comp.State is ProtoCoreState.Meltdown or ProtoCoreState.Critical)
        {
            args.Verbs.Add(new ActivationVerb
            {
                Text = Loc.GetString("proto-core-verb-stabilize"),
                Act = () => StartDoAfter(ent, user, new ProtoCoreStabilizeDoAfterEvent(), ent.Comp.StabilizeTime),
            });

            if (IsHackingDeviceConnected(ent))
            {
                args.Verbs.Add(new ActivationVerb
                {
                    Text = Loc.GetString("proto-core-verb-physical-override"),
                    Act = () => StartDoAfter(ent, user, new ProtoCorePhysicalOverrideDoAfterEvent(), ent.Comp.PhysicalOverrideTime),
                });
            }
        }
    }

    private void FinishHack(Entity<ProtoCoreConsoleComponent> ent)
    {
        ent.Comp.HackInProgress = false;
        ent.Comp.HackRemainingTime = 0f;

        if (!ent.Comp.HackingDeviceSlot.HasItem)
        {
            UpdateConsoleUi(ent);
            return;
        }

        ent.Comp.DeviceConnected = true;
        UpdateConsoleUi(ent);

        if (TryFindCore(ent, out var core) && core.Comp.State is ProtoCoreState.Idle or ProtoCoreState.Hacked)
        {
            SetState(core, ProtoCoreState.Hacked);
        }

        _chat.DispatchGlobalAnnouncement(
            Loc.GetString("proto-core-announcement-unauthorized-connection"),
            sender: Loc.GetString("proto-core-announcement-sender"),
            announcementSound: _breachAnnouncement,
            colorOverride: Color.Red);
        _audio.PlayPvs(_beep, ent);
    }

    private void OnStartDoAfter(Entity<ProtoCoreConsoleComponent> ent, ref ProtoCoreStartDoAfterEvent args)
    {
        if (args.Cancelled || !TryFindCore(ent, out var core))
            return;

        if (!IsAuthorized(ent) || !IsHackingDeviceConnected(ent))
        {
            _audio.PlayPvs(_deny, ent);
            return;
        }

        core.Comp.State = ProtoCoreState.Meltdown;
        core.Comp.RemainingTime = core.Comp.MeltdownTime;
        core.Comp.WarningStageStarted = false;
        core.Comp.CriticalStageStarted = false;
        core.Comp.EmergencyShuttleCalled = false;
        var storage = GetCoreNetworkStorage(core.Owner);
        core.Comp.LastStorageCharge = storage.Charge;
        core.Comp.LastStorageCapacity = storage.Capacity;
        core.Comp.HadChargedStorage = storage.Capacity > 0f && storage.Charge > StorageEmptyThreshold;
        UpdateConsoleUisForCore(core);
        SetRuleResult(ProtoCoreState.Meltdown);

        SetDeltaAlert(core.Owner);
        _chat.DispatchGlobalAnnouncement(
            Loc.GetString("proto-core-announcement-meltdown-started", ("time", FormatTime(core.Comp.RemainingTime))),
            sender: Loc.GetString("proto-core-announcement-sender"),
            colorOverride: Color.Red);
        _audio.PlayPvs(_beep, ent);
    }

    private void OnStabilizeDoAfter(Entity<ProtoCoreConsoleComponent> ent, ref ProtoCoreStabilizeDoAfterEvent args)
    {
        if (args.Cancelled || !TryFindCore(ent, out var core))
            return;

        if (!CanStabilize(core))
        {
            _audio.PlayPvs(_deny, ent);
            _popup.PopupEntity(Loc.GetString("proto-core-console-storage-charged"), ent, args.User);
            return;
        }

        Stabilize(core);
    }

    private void OnPhysicalOverrideDoAfter(Entity<ProtoCoreConsoleComponent> ent, ref ProtoCorePhysicalOverrideDoAfterEvent args)
    {
        if (args.Cancelled || !TryFindCore(ent, out var core))
            return;

        ent.Comp.ForceDeviceEjectInProgress = true;
        DisconnectHackingDevice(ent);
        _itemSlots.TryEjectToHands(ent, ent.Comp.HackingDeviceSlot, args.User, true);
        ent.Comp.ForceDeviceEjectInProgress = false;
        UpdateConsoleUi(ent);
        Stabilize(core);
        _popup.PopupEntity(Loc.GetString("proto-core-console-device-disconnected"), ent);
    }

    private void OnConsoleDamaged(Entity<ProtoCoreConsoleComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || !IsHackingDeviceConnected(ent) || !TryFindCore(ent, out var core))
            return;

        DisconnectHackingDevice(ent);
        UpdateConsoleUi(ent);
        Stabilize(core);
        _chat.DispatchGlobalAnnouncement(
            Loc.GetString("proto-core-announcement-stabilized"),
            sender: Loc.GetString("proto-core-announcement-sender"));
    }

    private void StartDoAfter(Entity<ProtoCoreConsoleComponent> ent, EntityUid user, SimpleDoAfterEvent ev, float delay)
    {
        var doAfter = new DoAfterArgs(EntityManager, user, delay, ev, ent, target: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void EnterWarningStage(EntityUid uid, ProtoCoreComponent core)
    {
        if (core.WarningStageStarted)
            return;

        core.WarningStageStarted = true;
        _sound.DispatchStationEventMusic(uid, core.MeltdownMusic, StationEventMusicType.ProtoCore);
    }

    private void EnterCritical(EntityUid uid, ProtoCoreComponent core)
    {
        if (core.CriticalStageStarted)
            return;

        core.CriticalStageStarted = true;
        core.State = ProtoCoreState.Critical;
        UpdateConsoleUisForCore((uid, core));
        SetRuleResult(ProtoCoreState.Critical);

        _chat.DispatchGlobalAnnouncement(
            Loc.GetString("proto-core-announcement-critical"),
            sender: Loc.GetString("proto-core-announcement-sender"),
            announcementSound: _breachAnnouncement,
            colorOverride: Color.Red);

        if (core.EmergencyShuttleCalled)
            return;

        core.EmergencyShuttleCalled = true;
        _roundEnd.RequestRoundEnd(
            TimeSpan.FromSeconds(core.EmergencyShuttleTime),
            checkCooldown: false,
            text: "proto-core-announcement-shuttle-called",
            name: "proto-core-announcement-sender",
            cantRecall: true);
    }

    private void FinishMeltdown(EntityUid uid, ProtoCoreComponent core)
    {
        if (core.Exploded)
            return;

        core.Exploded = true;
        _sound.StopStationEventMusic(uid, StationEventMusicType.ProtoCore);
        core.RemainingTime = 0f;
        core.State = ProtoCoreState.Critical;
        UpdateConsoleUisForCore((uid, core));
        SetRuleResult(ProtoCoreState.Critical);

        _explosion.QueueExplosion(
            uid,
            core.ExplosionType,
            core.ExplosionTotalIntensity,
            core.ExplosionIntensitySlope,
            core.ExplosionMaxTileIntensity);

        Del(uid);
        _roundEnd.EndRound(TimeSpan.FromSeconds(core.RoundEndDelay));
    }

    private void Stabilize(Entity<ProtoCoreComponent> core)
    {
        if (core.Comp.State is not (ProtoCoreState.Meltdown or ProtoCoreState.Critical))
            return;

        _sound.StopStationEventMusic(core.Owner, StationEventMusicType.ProtoCore);
        core.Comp.State = ProtoCoreState.Stabilized;
        core.Comp.RemainingTime = 0f;
        core.Comp.WarningStageStarted = false;
        core.Comp.CriticalStageStarted = false;
        UpdateConsoleUisForCore(core);
        SetRuleResult(ProtoCoreState.Stabilized);

        _chat.DispatchGlobalAnnouncement(
            Loc.GetString("proto-core-announcement-stabilized"),
            sender: Loc.GetString("proto-core-announcement-sender"));
    }

    private void SetState(Entity<ProtoCoreComponent> core, ProtoCoreState state)
    {
        core.Comp.State = state;
        UpdateConsoleUisForCore(core);
    }

    private void SetRuleResult(ProtoCoreState state)
    {
        var query = QueryActiveRules();
        while (query.MoveNext(out var uid, out _, out var rule, out _))
        {
            rule.Result = state;
        }
    }

    private void SetDeltaAlert(EntityUid core)
    {
        var station = _station.GetOwningStation(core) ?? _station.GetStationInMap(Transform(core).MapID);
        if (station is not { } stationUid)
            return;

        _alertLevel.SetLevel(stationUid, "delta", true, true, true, true);
    }

    private void OnConsoleUiOpened(Entity<ProtoCoreConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateConsoleUi(ent);
    }

    private void OnConsoleActionMessage(Entity<ProtoCoreConsoleComponent> ent, ref ProtoCoreConsoleActionMessage args)
    {
        if (args.Actor is not { Valid: true } user)
            return;

        if (!TryFindCore(ent, out var core))
        {
            _audio.PlayPvs(_deny, ent);
            UpdateConsoleUi(ent);
            return;
        }

        switch (args.Action)
        {
            case ProtoCoreConsoleAction.Start:
                if (IsAuthorized(ent) &&
                    IsHackingDeviceConnected(ent) &&
                    core.Comp.State is ProtoCoreState.Hacked or ProtoCoreState.Idle)
                    StartDoAfter(ent, user, new ProtoCoreStartDoAfterEvent(), ent.Comp.StartTime);
                else
                    _audio.PlayPvs(_deny, ent);
                break;
            case ProtoCoreConsoleAction.Stabilize:
                if (core.Comp.State is ProtoCoreState.Meltdown or ProtoCoreState.Critical)
                    StartDoAfter(ent, user, new ProtoCoreStabilizeDoAfterEvent(), ent.Comp.StabilizeTime);
                else
                    _audio.PlayPvs(_deny, ent);
                break;
            case ProtoCoreConsoleAction.PhysicalOverride:
                if (IsHackingDeviceConnected(ent) && core.Comp.State is (ProtoCoreState.Meltdown or ProtoCoreState.Critical))
                    StartDoAfter(ent, user, new ProtoCorePhysicalOverrideDoAfterEvent(), ent.Comp.PhysicalOverrideTime);
                else
                    _audio.PlayPvs(_deny, ent);
                break;
        }

        UpdateConsoleUi(ent);
    }

    private void UpdateConsoleUisForCore(Entity<ProtoCoreComponent> core)
    {
        var query = EntityQueryEnumerator<ProtoCoreConsoleComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            var consoleEnt = (Entity<ProtoCoreConsoleComponent>) (uid, console);
            if (!TryFindCore(consoleEnt, out var foundCore) || foundCore.Owner != core.Owner)
                continue;

            UpdateConsoleUi(consoleEnt);
        }
    }

    private void UpdateConsoleUi(Entity<ProtoCoreConsoleComponent> ent)
    {
        var coreInRange = TryFindCore(ent, out var core);
        var state = coreInRange ? core.Comp.State : ProtoCoreState.Idle;
        var remainingTime = ent.Comp.HackInProgress
            ? FormatTime(ent.Comp.HackRemainingTime)
            : coreInRange ? FormatTime(core.Comp.RemainingTime) : "--:--";
        var powerOutput = coreInRange ? GetPowerOutput(core.Owner) : "--";
        var storedEnergy = coreInRange ? GetStoredEnergy(core.Owner) : "--";
        var authorized = IsAuthorized(ent);
        var hackingDeviceConnected = IsHackingDeviceConnected(ent);
        var canStart = coreInRange &&
                       authorized &&
                       hackingDeviceConnected &&
                       state is (ProtoCoreState.Hacked or ProtoCoreState.Idle);
        var canStabilize = coreInRange && state is (ProtoCoreState.Meltdown or ProtoCoreState.Critical);
        if (canStabilize && !CanStabilize(core))
            canStabilize = false;

        _ui.SetUiState(ent.Owner, ProtoCoreConsoleUiKey.Key, new ProtoCoreConsoleBoundUserInterfaceState(
            state,
            remainingTime,
            powerOutput,
            storedEnergy,
            canStart,
            canStabilize));
    }

    private void DisconnectHackingDevice(Entity<ProtoCoreConsoleComponent> ent)
    {
        ent.Comp.HackInProgress = false;
        ent.Comp.HackRemainingTime = 0f;
        ent.Comp.DeviceConnected = false;

        if (ent.Comp.HackingDeviceSlot.Item is { } device && TryComp<AshHackingDeviceComponent>(device, out var hackingDevice))
            hackingDevice.Locked = false;
    }

    private void UpdateHackingDeviceVisual(Entity<ProtoCoreConsoleComponent> ent)
    {
        _appearance.SetData(ent, ProtoCoreVisuals.HackingDeviceInstalled, ent.Comp.HackingDeviceSlot.HasItem);
    }

    private static bool IsAuthorized(Entity<ProtoCoreConsoleComponent> ent)
    {
        return ent.Comp.ActivationKeySlot.HasItem;
    }

    private static bool IsHackingDeviceConnected(Entity<ProtoCoreConsoleComponent> ent)
    {
        return ent.Comp.DeviceConnected && ent.Comp.HackingDeviceSlot.HasItem;
    }

    private string GetPowerOutput(EntityUid core)
    {
        return TryComp<PowerSupplierComponent>(core, out var supplier)
            ? FormatPower(supplier.CurrentSupply)
            : "--";
    }

    private string GetStoredEnergy(EntityUid core)
    {
        var (charge, capacity) = GetCoreNetworkStorage(core);
        if (capacity <= 0f)
            return "--";

        return $"{FormatEnergy(charge)} / {FormatEnergy(capacity)}";
    }

    private (float Charge, float Capacity) GetCoreNetworkStorage(EntityUid core)
    {
        if (!TryComp<BatteryDischargerComponent>(core, out var discharger) ||
            discharger.Net is not PowerNet powerNet)
        {
            return GetBatteryStorage(core);
        }

        var seen = new HashSet<EntityUid>();
        var charge = 0f;
        var capacity = 0f;

        foreach (var linkedDischarger in powerNet.Dischargers)
            AddProtoCoreSmesStorage(linkedDischarger.Owner);

        foreach (var linkedCharger in powerNet.Chargers)
            AddProtoCoreSmesStorage(linkedCharger.Owner);

        if (capacity <= 0f)
            return (0f, 0f);

        return (charge, capacity);

        void AddProtoCoreSmesStorage(EntityUid uid)
        {
            if (!seen.Add(uid))
                return;

            if (!HasComp<ProtoCoreSmesComponent>(uid))
                return;

            var storage = GetBatteryStorage(uid);
            charge += storage.Charge;
            capacity += storage.Capacity;
        }
    }

    private bool CanStabilize(Entity<ProtoCoreComponent> core)
    {
        var (charge, capacity) = GetCoreNetworkStorage(core.Owner);
        return capacity > 0f && charge <= StorageEmptyThreshold;
    }

    private void CheckStorageDisconnectedDuringMeltdown(Entity<ProtoCoreComponent> core)
    {
        var (charge, capacity) = GetCoreNetworkStorage(core.Owner);
        var hasChargedStorage = capacity > 0f && charge > StorageEmptyThreshold;

        var chargedStorageDisconnected = core.Comp.HadChargedStorage &&
                                         core.Comp.LastStorageCharge > StorageEmptyThreshold &&
                                         capacity < core.Comp.LastStorageCapacity - StorageEmptyThreshold;

        if (chargedStorageDisconnected)
        {
            core.Comp.RemainingTime = MathF.Min(core.Comp.RemainingTime, core.Comp.CriticalThreshold);
            EnterCritical(core.Owner, core.Comp);
            _chat.DispatchGlobalAnnouncement(
                Loc.GetString("proto-core-announcement-storage-disconnected"),
                sender: Loc.GetString("proto-core-announcement-sender"),
                announcementSound: _breachAnnouncement,
                colorOverride: Color.Red);
        }

        core.Comp.LastStorageCharge = charge;
        core.Comp.LastStorageCapacity = capacity;
        core.Comp.HadChargedStorage = hasChargedStorage;
    }

    private (float Charge, float Capacity) GetBatteryStorage(EntityUid uid)
    {
        if (!TryComp<BatteryComponent>(uid, out var battery))
            return (0f, 0f);

        return (_battery.GetCharge((uid, battery)), battery.MaxCharge);
    }

    private bool TryFindCore(Entity<ProtoCoreConsoleComponent> console, out Entity<ProtoCoreComponent> core)
    {
        var consoleXform = Transform(console);
        var query = EntityQueryEnumerator<ProtoCoreComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (!SameMap(consoleXform.Coordinates, xform.Coordinates))
                continue;

            if ((consoleXform.LocalPosition - xform.LocalPosition).Length() > console.Comp.MaxCoreDistance)
                continue;

            core = (uid, comp);
            return true;
        }

        core = default;
        return false;
    }

    private static bool SameMap(EntityCoordinates a, EntityCoordinates b)
    {
        return a.EntityId == b.EntityId;
    }

    private static string FormatTime(float seconds)
    {
        var time = TimeSpan.FromSeconds(MathF.Max(0f, seconds));
        return $"{(int) time.TotalMinutes:00}:{time.Seconds:00}";
    }

    private static string FormatPower(float watts)
    {
        return MathF.Abs(watts) >= 1_000_000f
            ? $"{watts / 1_000_000f:0.##} MW"
            : $"{watts / 1_000f:0.##} kW";
    }

    private static string FormatEnergy(float joules)
    {
        return MathF.Abs(joules) >= 1_000_000_000f
            ? $"{joules / 1_000_000_000f:0.##} GJ"
            : $"{joules / 1_000_000f:0.##} MJ";
    }
}

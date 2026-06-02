using Content.Server._Eclipse.ProtoCore.Components;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Popups;
using Content.Server.RoundEnd;
using Content.Shared._Eclipse.ProtoCore;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.GameTicking.Components;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
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

    private readonly SoundSpecifier _beep = new SoundPathSpecifier("/Audio/Machines/terminal_success.ogg");
    private readonly SoundSpecifier _deny = new SoundPathSpecifier("/Audio/Machines/terminal_error.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ProtoCoreComponent, ExaminedEvent>(OnCoreExamined);
        SubscribeLocalEvent<ProtoCoreConsoleComponent, ExaminedEvent>(OnConsoleExamined);
        SubscribeLocalEvent<AshHackingDeviceComponent, InteractHandEvent>(OnDeviceInteractHand);
        SubscribeLocalEvent<ProtoCoreConsoleComponent, InteractUsingEvent>(OnConsoleInteractUsing);
        SubscribeLocalEvent<ProtoCoreConsoleComponent, GetVerbsEvent<ActivationVerb>>(OnConsoleGetVerbs);
        SubscribeLocalEvent<ProtoCoreConsoleComponent, ProtoCoreHackDoAfterEvent>(OnHackDoAfter);
        SubscribeLocalEvent<ProtoCoreConsoleComponent, ProtoCoreStartDoAfterEvent>(OnStartDoAfter);
        SubscribeLocalEvent<ProtoCoreConsoleComponent, ProtoCoreStabilizeDoAfterEvent>(OnStabilizeDoAfter);
        SubscribeLocalEvent<ProtoCoreConsoleComponent, ProtoCorePhysicalOverrideDoAfterEvent>(OnPhysicalOverrideDoAfter);
        SubscribeLocalEvent<ProtoCoreConsoleComponent, DamageChangedEvent>(OnConsoleDamaged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ProtoCoreComponent>();
        while (query.MoveNext(out var uid, out var core))
        {
            if (core.State is not (ProtoCoreState.Meltdown or ProtoCoreState.Critical))
                continue;

            // TODO: Replace this with a power-network/SMES check. The first pass keeps the timer deterministic
            // until the project decides how "station-wide power loss" should be measured.
            core.RemainingTime = MathF.Max(0f, core.RemainingTime - frameTime);

            if (core.State == ProtoCoreState.Meltdown && core.RemainingTime <= core.CriticalThreshold)
                EnterCritical(uid, core);

            if (core.RemainingTime <= 0f)
                FinishMeltdown(uid, core);
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
            ("authorized", ent.Comp.Authorized),
            ("device", ent.Comp.DeviceConnected)));
    }

    private void OnDeviceInteractHand(Entity<AshHackingDeviceComponent> ent, ref InteractHandEvent args)
    {
        if (!ent.Comp.Locked)
            return;

        _popup.PopupEntity(Loc.GetString("ash-hacking-device-locked"), ent, args.User);
        args.Handled = true;
    }

    private void OnConsoleInteractUsing(Entity<ProtoCoreConsoleComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<ProtoCoreActivationKeyComponent>(args.Used))
        {
            ent.Comp.Authorized = true;
            Dirty(ent);
            _popup.PopupEntity(Loc.GetString("proto-core-console-key-accepted"), ent, args.User);
            _audio.PlayPvs(_beep, ent);
            args.Handled = true;
            return;
        }

        if (!TryComp<AshHackingDeviceComponent>(args.Used, out var device))
            return;

        if (ent.Comp.DeviceConnected)
        {
            _popup.PopupEntity(Loc.GetString("proto-core-console-device-already-connected"), ent, args.User);
            args.Handled = true;
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.HackTime, new ProtoCoreHackDoAfterEvent(), ent, target: ent, used: args.Used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
        {
            device.Locked = true;
            Dirty(args.Used, device);
            _popup.PopupEntity(Loc.GetString("proto-core-console-hack-start"), ent, args.User);
            args.Handled = true;
        }
    }

    private void OnConsoleGetVerbs(Entity<ProtoCoreConsoleComponent> ent, ref GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        var user = args.User;

        if (TryFindCore(ent, out var core) && ent.Comp.Authorized && ent.Comp.DeviceConnected && core.Comp.State is ProtoCoreState.Hacked or ProtoCoreState.Idle)
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

            if (ent.Comp.DeviceConnected)
            {
                args.Verbs.Add(new ActivationVerb
                {
                    Text = Loc.GetString("proto-core-verb-physical-override"),
                    Act = () => StartDoAfter(ent, user, new ProtoCorePhysicalOverrideDoAfterEvent(), ent.Comp.PhysicalOverrideTime),
                });
            }
        }
    }

    private void OnHackDoAfter(Entity<ProtoCoreConsoleComponent> ent, ref ProtoCoreHackDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        ent.Comp.DeviceConnected = true;
        Dirty(ent);

        if (TryFindCore(ent, out var core) && core.Comp.State == ProtoCoreState.Idle)
            SetState(core, ProtoCoreState.Hacked);

        _chat.DispatchGlobalAnnouncement(
            Loc.GetString("proto-core-announcement-unauthorized-connection"),
            sender: Loc.GetString("proto-core-announcement-sender"));
        _audio.PlayPvs(_beep, ent);
    }

    private void OnStartDoAfter(Entity<ProtoCoreConsoleComponent> ent, ref ProtoCoreStartDoAfterEvent args)
    {
        if (args.Cancelled || !TryFindCore(ent, out var core))
            return;

        if (!ent.Comp.Authorized || !ent.Comp.DeviceConnected)
        {
            _audio.PlayPvs(_deny, ent);
            return;
        }

        core.Comp.State = ProtoCoreState.Meltdown;
        core.Comp.RemainingTime = core.Comp.MeltdownTime;
        Dirty(core);
        SetRuleResult(ProtoCoreState.Meltdown);

        _chat.DispatchGlobalAnnouncement(
            Loc.GetString("proto-core-announcement-meltdown-started", ("time", FormatTime(core.Comp.RemainingTime))),
            sender: Loc.GetString("proto-core-announcement-sender"));
        _audio.PlayPvs(_beep, ent);
    }

    private void OnStabilizeDoAfter(Entity<ProtoCoreConsoleComponent> ent, ref ProtoCoreStabilizeDoAfterEvent args)
    {
        if (args.Cancelled || !TryFindCore(ent, out var core))
            return;

        Stabilize(core);
    }

    private void OnPhysicalOverrideDoAfter(Entity<ProtoCoreConsoleComponent> ent, ref ProtoCorePhysicalOverrideDoAfterEvent args)
    {
        if (args.Cancelled || !TryFindCore(ent, out var core))
            return;

        ent.Comp.DeviceConnected = false;
        Dirty(ent);
        Stabilize(core);
        _popup.PopupEntity(Loc.GetString("proto-core-console-device-disconnected"), ent);
    }

    private void OnConsoleDamaged(Entity<ProtoCoreConsoleComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || !ent.Comp.DeviceConnected || !TryFindCore(ent, out var core))
            return;

        ent.Comp.DeviceConnected = false;
        Dirty(ent);
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

    private void EnterCritical(EntityUid uid, ProtoCoreComponent core)
    {
        core.State = ProtoCoreState.Critical;
        Dirty(uid, core);
        SetRuleResult(ProtoCoreState.Critical);

        _chat.DispatchGlobalAnnouncement(
            Loc.GetString("proto-core-announcement-critical"),
            sender: Loc.GetString("proto-core-announcement-sender"));

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
        core.RemainingTime = 0f;
        core.State = ProtoCoreState.Critical;
        Dirty(uid, core);
        SetRuleResult(ProtoCoreState.Critical);

        // The first implementation intentionally leaves actual station destruction to round flow.
        // A future pass can hook this point into a map-wide disaster once design approves the blast effect.
        _roundEnd.EndRound();
    }

    private void Stabilize(Entity<ProtoCoreComponent> core)
    {
        if (core.Comp.State is not (ProtoCoreState.Meltdown or ProtoCoreState.Critical))
            return;

        core.Comp.State = ProtoCoreState.Stabilized;
        core.Comp.RemainingTime = 0f;
        Dirty(core);
        SetRuleResult(ProtoCoreState.Stabilized);

        _chat.DispatchGlobalAnnouncement(
            Loc.GetString("proto-core-announcement-stabilized"),
            sender: Loc.GetString("proto-core-announcement-sender"));
    }

    private void SetState(Entity<ProtoCoreComponent> core, ProtoCoreState state)
    {
        core.Comp.State = state;
        Dirty(core);
    }

    private void SetRuleResult(ProtoCoreState state)
    {
        var query = QueryActiveRules();
        while (query.MoveNext(out var uid, out _, out var rule, out _))
        {
            rule.Result = state;
            Dirty(uid, rule);
        }
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
}

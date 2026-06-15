using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;

namespace Content.Shared._Eclipse.Industrial;

public abstract class SharedItemPipeSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedToolSystem _tools = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ItemPipeComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ItemPipeComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<ItemPipeComponent, AfterInteractUsingEvent>(OnAfterInteractUsing);
    }

    private void OnAfterInteractUsing(Entity<ItemPipeComponent> ent, ref AfterInteractUsingEvent args)
    {
        if (args.Handled || !args.CanReach || !_tools.HasQuality(args.Used, SharedToolSystem.PulseQuality))
            return;

        CycleTransferMode(ent, args.User);
        args.Handled = true;
    }

    private void OnGetVerbs(Entity<ItemPipeComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var held = args.Using;
        if (held == null || !_tools.HasQuality(held.Value, SharedToolSystem.PulseQuality))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("industrial-pipe-toggle-mode"),
            Act = () => CycleTransferMode(ent, user),
        });
    }

    private void OnExamined(Entity<ItemPipeComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("industrial-pipe-examine-tier", ("tier", GetPipeTierName(ent.Comp.Tier))));
        args.PushMarkup(Loc.GetString("industrial-pipe-examine-mode", ("mode", GetTransferModeName(ent.Comp.TransferMode))));
        args.PushMarkup(Loc.GetString("industrial-pipe-examine-throughput",
            ("throughput", ent.Comp.ThroughputPerSecond)));
        PushNetworkExamine(ent, args);
    }

    protected virtual void PushNetworkExamine(Entity<ItemPipeComponent> ent, ExaminedEvent args) { }

    public void CycleTransferMode(Entity<ItemPipeComponent> ent, EntityUid user)
    {
        ent.Comp.TransferMode = ent.Comp.TransferMode switch
        {
            PipeTransferMode.Transit => PipeTransferMode.Extract,
            PipeTransferMode.Extract => PipeTransferMode.Insert,
            _ => PipeTransferMode.Transit,
        };

        Dirty(ent);
        _popup.PopupClient(Loc.GetString("industrial-pipe-mode-switched",
            ("mode", GetTransferModeName(ent.Comp.TransferMode))), ent, user);
    }

    public static string GetTransferModeName(PipeTransferMode mode)
    {
        return Robust.Shared.Localization.Loc.GetString(mode switch
        {
            PipeTransferMode.Extract => "industrial-pipe-mode-extract",
            PipeTransferMode.Insert => "industrial-pipe-mode-insert",
            _ => "industrial-pipe-mode-transit",
        });
    }

    public static string GetPipeTierName(PipeTier tier)
    {
        return Robust.Shared.Localization.Loc.GetString(tier switch
        {
            PipeTier.Industrial => "industrial-machine-tier-industrial",
            PipeTier.Perfect => "industrial-machine-tier-perfect",
            _ => "industrial-machine-tier-basic",
        });
    }
}

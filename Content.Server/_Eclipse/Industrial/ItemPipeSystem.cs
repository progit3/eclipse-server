using Content.Shared._Eclipse.Industrial;
using Content.Shared.Examine;
using Robust.Shared.Map.Components;

namespace Content.Server._Eclipse.Industrial;

public sealed class ItemPipeSystem : SharedItemPipeSystem
{
    [Dependency] private readonly ItemPipeNetworkSystem _network = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ItemPipeComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<ItemPipeComponent, MapInitEvent>(OnMapInit);
    }

    private void OnComponentInit(Entity<ItemPipeComponent> ent, ref ComponentInit args)
    {
        ApplyTierSettings(ent);
        _network.RebuildNetworkFrom(ent);
    }

    private void OnMapInit(Entity<ItemPipeComponent> ent, ref MapInitEvent args)
    {
        ApplyTierSettings(ent);
        _network.RebuildNetworkFrom(ent);
    }

    private void ApplyTierSettings(Entity<ItemPipeComponent> ent)
    {
        var specs = PipeTierHelper.GetSpecs(ent.Comp.Tier);
        ent.Comp.ThroughputPerSecond = specs.ThroughputPerSecond;
        ent.Comp.TransferDelay = specs.TransferDelay;
        Dirty(ent);
    }

    protected override void PushNetworkExamine(Entity<ItemPipeComponent> ent, ExaminedEvent args)
    {
        if (ent.Comp.NetworkId < 0)
        {
            args.PushMarkup(Loc.GetString("industrial-pipe-no-network"));
            return;
        }

        if (!_network.TryGetNetwork(ent.Comp.NetworkId, out var network))
        {
            args.PushMarkup(Loc.GetString("industrial-pipe-no-network"));
            return;
        }

        args.PushMarkup(Loc.GetString("industrial-pipe-examine-network",
            ("pipes", network.Pipes.Count),
            ("tier", SharedItemPipeSystem.GetPipeTierName(network.EffectiveTier))));
    }
}

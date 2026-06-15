using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Server.Stack;
using Content.Shared._Eclipse.Industrial;
using Content.Shared._Eclipse.Industrial.Prototypes;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Stacks;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;

namespace Content.Server._Eclipse.Industrial;

public sealed class IndustrialProcessorSystem : SharedIndustrialProcessorSystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly ItemPipeNetworkSystem _pipeNetwork = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IndustrialProcessorComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<IndustrialProcessorComponent, MapInitEvent>(OnMapInit);
    }

    private void OnComponentInit(Entity<IndustrialProcessorComponent> ent, ref ComponentInit args)
    {
        ApplyTierSettings(ent);
        UpdateAppearance(ent);
    }

    private void OnMapInit(Entity<IndustrialProcessorComponent> ent, ref MapInitEvent args)
    {
        ApplyTierSettings(ent);
        UpdateAppearance(ent);
    }

    private void ApplyTierSettings(Entity<IndustrialProcessorComponent> ent)
    {
        var specs = MachineTierHelper.GetSpecs(ent.Comp.Tier);

        ent.Comp.ProcessingSpeedMultiplier = specs.ProcessingSpeedMultiplier;
        ent.Comp.MaxInputSlots = specs.MaxInputSlots;
        ent.Comp.MaxOutputSlots = specs.MaxOutputSlots;
        ent.Comp.MaxAutoTransferPerSecond = specs.MaxAutoTransferPerSecond;

        if (TryComp<ApcPowerReceiverComponent>(ent, out var receiver))
        {
            var baseLoad = ent.Comp.BasePowerLoad > 0 ? ent.Comp.BasePowerLoad : receiver.Load;
            ent.Comp.BasePowerLoad = baseLoad;
            receiver.Load = baseLoad * specs.PowerMultiplier;
        }

        Dirty(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<IndustrialProcessorComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var ent = (uid, comp);

            if (!comp.IsWorking)
            {
                UpdateAppearance(ent);
                continue;
            }

            if (!IsPowered(ent))
            {
                UpdateAppearance(ent);
                continue;
            }

            comp.ProcessingAccumulator += frameTime * comp.ProcessingSpeedMultiplier;
            Dirty(uid, comp);

            if (comp.ProcessingAccumulator < comp.ProcessingTime)
                continue;

            if (!TryCompleteRecipe(ent))
            {
                comp.IsWorking = false;
                comp.ProcessingAccumulator = 0;
                comp.CurrentRecipe = null;
                Dirty(uid, comp);
            }

            UpdateAppearance(ent);
        }
    }

    protected override void TryStartProcessing(Entity<IndustrialProcessorComponent> ent)
    {
        if (ent.Comp.IsWorking)
            return;

        if (!IsPowered(ent))
            return;

        if (!TryMatchRecipe(ent, out var recipe) || recipe == null)
            return;

        if (!CanAcceptOutputs(ent, recipe))
        {
            _popup.PopupEntity(Loc.GetString("industrial-processor-output-full"), ent);
            UpdateAppearance(ent);
            return;
        }

        ent.Comp.CurrentRecipe = recipe.ID;
        ent.Comp.ProcessingTime = recipe.Time;
        ent.Comp.ProcessingAccumulator = 0;
        ent.Comp.IsWorking = true;
        Dirty(ent);

        _popup.PopupEntity(Loc.GetString("industrial-processor-started"), ent);
        UpdateAppearance(ent);
    }

    private bool TryCompleteRecipe(Entity<IndustrialProcessorComponent> ent)
    {
        if (ent.Comp.CurrentRecipe is not { } recipeId ||
            !PrototypeManager.TryIndex(recipeId, out IndustrialRecipePrototype? recipe))
        {
            return false;
        }

        if (!IsPowered(ent))
            return false;

        if (!TryMatchRecipe(ent, out var matched) || matched?.ID != recipe.ID)
            return false;

        if (!CanAcceptOutputs(ent, recipe))
        {
            _popup.PopupEntity(Loc.GetString("industrial-processor-output-full"), ent);
            return false;
        }

        if (!ConsumeInputs(ent, recipe))
            return false;

        if (!ProduceOutputs(ent, recipe))
            return false;

        ent.Comp.IsWorking = false;
        ent.Comp.ProcessingAccumulator = 0;
        ent.Comp.CurrentRecipe = null;
        Dirty(ent);

        _popup.PopupEntity(Loc.GetString("industrial-processor-finished"), ent);

        if (ent.Comp.AutoStart)
            TryStartProcessing(ent);

        return true;
    }

    private bool ConsumeInputs(Entity<IndustrialProcessorComponent> ent, IndustrialRecipePrototype recipe)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.InputContainerId, out var input))
            return false;

        foreach (var (protoId, amount) in recipe.Inputs)
        {
            EntityUid? target = null;

            foreach (var contained in input.ContainedEntities)
            {
                if (Prototype(contained)?.ID != protoId)
                    continue;

                if (GetAvailableCount(contained) >= amount)
                {
                    target = contained;
                    break;
                }
            }

            if (target == null)
                return false;

            if (TryComp<StackComponent>(target, out var stack))
            {
                if (stack.Count > amount)
                {
                    _stack.SetCount((target.Value, stack), stack.Count - amount);
                    continue;
                }
            }

            _container.Remove(target.Value, input);
            Del(target.Value);
        }

        return true;
    }

    private bool ProduceOutputs(Entity<IndustrialProcessorComponent> ent, IndustrialRecipePrototype recipe)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.OutputContainerId, out var output))
            return false;

        foreach (var (protoId, amount) in recipe.Outputs)
        {
            var merged = false;

            foreach (var existing in output.ContainedEntities)
            {
                if (Prototype(existing)?.ID != protoId || !TryComp<StackComponent>(existing, out var stack))
                    continue;

                var max = stack.MaxCountOverride ?? PrototypeManager.Index(stack.StackTypeId).MaxCount;
                if (stack.Count + amount > max)
                    continue;

                _stack.SetCount((existing, stack), stack.Count + amount);
                merged = true;
                break;
            }

            if (merged)
                continue;

            var spawned = Spawn(protoId, Transform(ent).Coordinates);

            if (TryComp<StackComponent>(spawned, out var spawnedStack))
                _stack.SetCount((spawned, spawnedStack), amount);

            if (!_container.Insert(spawned, output))
            {
                Del(spawned);
                return false;
            }
        }

        return true;
    }

  /// <summary>
  /// Transfers one stack unit from output buffer to another machine's input buffer via pipe network.
  /// TODO: Batch transfer for stacks.
  /// </summary>
    public bool TryPipeTransfer(Entity<IndustrialProcessorComponent> source, Entity<IndustrialProcessorComponent> sink, string protoId)
    {
        if (!CanAcceptInputItem(sink, protoId))
            return false;

        if (!_container.TryGetContainer(source, source.Comp.OutputContainerId, out var output))
            return false;

        if (!_container.TryGetContainer(sink, sink.Comp.InputContainerId, out var input))
            return false;

        EntityUid? outputItem = null;

        foreach (var contained in output.ContainedEntities)
        {
            if (Prototype(contained)?.ID == protoId && GetAvailableCount(contained) > 0)
            {
                outputItem = contained;
                break;
            }
        }

        if (outputItem == null)
            return false;

        if (!TryRemoveOneUnit(outputItem.Value, output))
            return false;

        if (!TryInsertOneUnit(sink, input, protoId))
        {
            RestoreOneUnitToOutput(output, protoId, source);
            return false;
        }

        if (sink.Comp.AutoStart)
            TryStartProcessing(sink);

        return true;
    }

    public bool TryGetFirstOutputProto(Entity<IndustrialProcessorComponent> ent, out string protoId)
    {
        protoId = string.Empty;

        if (!_container.TryGetContainer(ent, ent.Comp.OutputContainerId, out var output))
            return false;

        foreach (var contained in output.ContainedEntities)
        {
            var proto = Prototype(contained);
            if (proto == null || GetAvailableCount(contained) <= 0)
                continue;

            protoId = proto.ID;
            return true;
        }

        return false;
    }

    private bool TryInsertOneUnit(Entity<IndustrialProcessorComponent> ent, BaseContainer input, string protoId)
    {
        foreach (var existing in input.ContainedEntities)
        {
            if (Prototype(existing)?.ID != protoId || !TryComp<StackComponent>(existing, out var stack))
                continue;

            var max = stack.MaxCountOverride ?? PrototypeManager.Index(stack.StackTypeId).MaxCount;
            if (stack.Count + 1 > max)
                continue;

            _stack.SetCount((existing, stack), stack.Count + 1);
            return true;
        }

        if (input.Count >= ent.Comp.MaxInputSlots)
            return false;

        var spawned = Spawn(protoId, Transform(ent).Coordinates);

        if (TryComp<StackComponent>(spawned, out var spawnedStack))
            _stack.SetCount((spawned, spawnedStack), 1);

        if (!_container.Insert(spawned, input))
        {
            Del(spawned);
            return false;
        }

        return true;
    }

    private bool TryRemoveOneUnit(EntityUid item, BaseContainer container)
    {
        if (TryComp<StackComponent>(item, out var stack))
        {
            if (stack.Count > 1)
            {
                _stack.SetCount((item, stack), stack.Count - 1);
                return true;
            }
        }

        _container.Remove(item, container);
        Del(item);
        return true;
    }

    private void RestoreOneUnitToOutput(BaseContainer output, string protoId, EntityUid coordOwner)
    {
        foreach (var existing in output.ContainedEntities)
        {
            if (Prototype(existing)?.ID != protoId || !TryComp<StackComponent>(existing, out var stack))
                continue;

            var max = stack.MaxCountOverride ?? PrototypeManager.Index(stack.StackTypeId).MaxCount;
            if (stack.Count + 1 <= max)
            {
                _stack.SetCount((existing, stack), stack.Count + 1);
                return;
            }
        }

        var spawned = Spawn(protoId, Transform(coordOwner).Coordinates);
        if (TryComp<StackComponent>(spawned, out var spawnedStack))
            _stack.SetCount((spawned, spawnedStack), 1);

        if (!_container.Insert(spawned, output))
            Del(spawned);
    }

    protected override void EjectOutput(Entity<IndustrialProcessorComponent> ent, EntityUid item, EntityUid user)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.OutputContainerId, out var output))
            return;

        if (!output.Contains(item))
            return;

        _container.Remove(item, output);
        _hands.PickupOrDrop(user, item, checkActionBlocker: false);

        if (ent.Comp.AutoStart)
            TryStartProcessing(ent);

        UpdateAppearance(ent);
    }

    protected override void UpdateAppearance(Entity<IndustrialProcessorComponent> ent)
    {
        _appearance.SetData(ent, IndustrialProcessorVisuals.State, GetState(ent));
    }

    protected override void OnPortModeChanged(Entity<IndustrialProcessorComponent> ent)
    {
        _pipeNetwork.RebuildNetworksNearProcessor(ent);
    }
}

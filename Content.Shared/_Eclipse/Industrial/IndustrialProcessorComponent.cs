using Content.Shared._Eclipse.Industrial.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._Eclipse.Industrial;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedIndustrialProcessorSystem), Other = AccessPermissions.ReadWriteExecute)]
public sealed partial class IndustrialProcessorComponent : Component
{
    [DataField(required: true)]
    public IndustrialProcessorType ProcessorType;

    [DataField]
    public MachineTier Tier = MachineTier.Industrial;

    [DataField]
    public string InputContainerId = "industrial_input";

    [DataField]
    public string OutputContainerId = "industrial_output";

    [DataField, AutoNetworkedField]
    public ProtoId<IndustrialRecipePrototype>? CurrentRecipe;

    [DataField, AutoNetworkedField]
    public float ProcessingTime;

    [DataField, AutoNetworkedField]
    public float ProcessingAccumulator;

    [DataField, AutoNetworkedField]
    public bool IsWorking;

    [DataField]
    public float RequiredPower = 1000f;

    [DataField]
    public float ProcessingSpeedMultiplier = 1f;

    [DataField]
    public int MaxInputSlots = 4;

    [DataField]
    public int MaxOutputSlots = 4;

    [DataField]
    public int MaxAutoTransferPerSecond = 1;

    [DataField]
    public float BasePowerLoad = 0f;

    [DataField]
    public bool CanWorkWithoutPower;

    [DataField]
    public bool AutoStart = true;

    [DataField, AutoNetworkedField]
    public PortMode NorthPort = PortMode.Disabled;

    [DataField, AutoNetworkedField]
    public PortMode SouthPort = PortMode.Disabled;

    [DataField, AutoNetworkedField]
    public PortMode EastPort = PortMode.Disabled;

    [DataField, AutoNetworkedField]
    public PortMode WestPort = PortMode.Disabled;

    /// <summary>
    /// TODO: Add optional SolutionContainerId for washer water/reagent consumption.
    /// </summary>
    [DataField]
    public string? SolutionContainerId;

    public PortMode GetPortMode(Direction direction)
    {
        return direction switch
        {
            Direction.North => NorthPort,
            Direction.South => SouthPort,
            Direction.East => EastPort,
            Direction.West => WestPort,
            _ => PortMode.Disabled,
        };
    }

    public void SetPortMode(Direction direction, PortMode mode)
    {
        switch (direction)
        {
            case Direction.North:
                NorthPort = mode;
                break;
            case Direction.South:
                SouthPort = mode;
                break;
            case Direction.East:
                EastPort = mode;
                break;
            case Direction.West:
                WestPort = mode;
                break;
        }
    }

    public PortMode CyclePortMode(Direction direction)
    {
        var next = GetPortMode(direction) switch
        {
            PortMode.Disabled => PortMode.Input,
            PortMode.Input => PortMode.Output,
            _ => PortMode.Disabled,
        };

        SetPortMode(direction, next);
        return next;
    }
}

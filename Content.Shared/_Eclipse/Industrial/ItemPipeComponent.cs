using Robust.Shared.GameStates;

namespace Content.Shared._Eclipse.Industrial;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedItemPipeSystem), Other = AccessPermissions.ReadWrite)]
public sealed partial class ItemPipeComponent : Component
{
    [DataField]
    public PipeTier Tier = PipeTier.Basic;

    [DataField]
    public float ThroughputPerSecond = 1f;

    [DataField]
    public float TransferDelay = 1f;

    [DataField, AutoNetworkedField]
    public PipeTransferMode TransferMode = PipeTransferMode.Transit;

    [DataField, AutoNetworkedField]
    public int NetworkId = -1;
}

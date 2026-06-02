namespace Content.Server._Eclipse.ProtoCore.Components;

[RegisterComponent, Access(typeof(ProtoCoreSystem))]
public sealed partial class ProtoCoreConsoleComponent : Component
{
    [DataField]
    public float MaxCoreDistance = 4f;

    [DataField]
    public float HackTime = 12f;

    [DataField]
    public float StartTime = 8f;

    [DataField]
    public float StabilizeTime = 20f;

    [DataField]
    public float PhysicalOverrideTime = 10f;

    [DataField]
    public bool Authorized;

    [DataField]
    public bool DeviceConnected;
}

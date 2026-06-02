using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Eclipse.ProtoCore.Components;

[RegisterComponent, Access(typeof(ProtoCoreSystem))]
public sealed partial class ProtoCoreComponent : Component
{
    [DataField]
    public float MeltdownTime = 900f;

    [DataField]
    public float CriticalThreshold = 180f;

    [DataField]
    public float PowerLossPenalty = 120f;

    [DataField]
    public float EmergencyShuttleTime = 180f;

    [DataField]
    public float RemainingTime;

    [DataField]
    public ProtoCoreState State = ProtoCoreState.Idle;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextStatusAt;

    [DataField]
    public bool EmergencyShuttleCalled;
}

public enum ProtoCoreState : byte
{
    Idle,
    Hacked,
    Meltdown,
    Critical,
    Stabilized,
}

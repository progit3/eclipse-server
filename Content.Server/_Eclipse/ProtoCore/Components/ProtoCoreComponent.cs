using Content.Shared._Eclipse.ProtoCore;
using Robust.Shared.Audio;
using Content.Shared.Explosion;
using Robust.Shared.Prototypes;
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
    public float WarningThreshold = 240f;

    [DataField]
    public float PowerLossPenalty = 120f;

    [DataField]
    public float EmergencyShuttleTime = 180f;

    [DataField]
    public SoundSpecifier MeltdownMusic = new SoundCollectionSpecifier("NukeMusic");

    [DataField]
    public float MeltdownMusicDuration = 109.6f;

    [DataField]
    public ProtoId<ExplosionPrototype> ExplosionType = "Default";

    [DataField]
    public float ExplosionTotalIntensity = 200000f;

    [DataField]
    public float ExplosionIntensitySlope = 5f;

    [DataField]
    public float ExplosionMaxTileIntensity = 200f;

    [DataField]
    public float RoundEndDelay = 8f;

    [DataField]
    public float RemainingTime;

    [DataField]
    public ProtoCoreState State = ProtoCoreState.Idle;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextStatusAt;

    [DataField]
    public bool EmergencyShuttleCalled;

    [DataField]
    public bool WarningStageStarted;

    [DataField]
    public bool CriticalStageStarted;

    [DataField]
    public float LastStorageCharge;

    [DataField]
    public float LastStorageCapacity;

    [DataField]
    public bool HadChargedStorage;

    [DataField]
    public bool Exploded;
}

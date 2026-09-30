using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._White.Standing;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LayingDownComponent : Component
{
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public float StandingUpTime { get; set; } = 1.5f;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public float SpeedModify { get; set; } = .3f;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public bool AutoGetUp;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public bool IsSliding;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public float SlideSpeedModifier { get; set; } = 1.2f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan SlideCooldownEndsAt;
}
[Serializable, NetSerializable]
public sealed class ChangeLayingDownEvent : CancellableEntityEventArgs;

[Serializable, NetSerializable]
public sealed class CheckAutoGetUpEvent(NetEntity user) : CancellableEntityEventArgs
{
    public NetEntity User = user;
}

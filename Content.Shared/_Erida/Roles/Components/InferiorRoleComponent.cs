using Robust.Shared.GameStates;
using Content.Shared.Roles.Components;

namespace Content.Shared._Erida.Roles.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class InferiorRoleComponent : BaseMindRoleComponent
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public EntityUid Overlord;
}

using Content.Shared._White;
using Content.Shared._White.Standing;
using Content.Shared.CCVar;
using Content.Shared.Standing;
using Robust.Shared.Configuration;
using Robust.Shared.Physics.Components;

namespace Content.Server.Standing;

public sealed partial class LayingDownSystem : SharedLayingDownSystem
{
    [Dependency] private INetConfigurationManager _cfg = default!;
    [Dependency] private StandingStateSystem _standing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CheckAutoGetUpEvent>(OnCheckAutoGetUp);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<LayingDownComponent>();
        while (query.MoveNext(out var uid, out var layingDown))
        {
            if (!layingDown.IsSliding)
                continue;

            if (!_standing.IsDown(uid) ||
                !TryComp(uid, out PhysicsComponent? physics) ||
                physics.LinearVelocity.LengthSquared() <= 0.01f)
            {
                EndSlide(uid, layingDown);
            }
        }
    }

    private void OnCheckAutoGetUp(CheckAutoGetUpEvent ev, EntitySessionEventArgs args)
    {
        var uid = GetEntity(ev.User);

        if (!TryComp(uid, out LayingDownComponent? layingDown))
            return;

        layingDown.AutoGetUp = _cfg.GetClientCVar(args.SenderSession.Channel, CCVars.AutoGetUp);
        Dirty(uid, layingDown);
    }
}

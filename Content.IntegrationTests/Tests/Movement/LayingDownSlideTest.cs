#nullable enable
using System.Numerics;
using Content.Shared.Input;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Standing;
using Content.Shared._White.Standing;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests.Movement;

public sealed class LayingDownSlideTest : MovementTest
{
    [Test]
    public async Task SlideEndsWhenStoppedAndLeavesMobLying()
    {
        await PrepareRunningWithShift();
        await PressToggleStanding();

        var layingDown = Comp<LayingDownComponent>(Player);
        Assert.That(layingDown.IsSliding);
        Assert.That(Comp<StandingStateComponent>(Player).CurrentState, Is.EqualTo(StandingState.Lying));
        Assert.That(Comp<MovementSpeedModifierComponent>(Player).WalkSpeedModifier,
            Is.EqualTo(layingDown.SlideSpeedModifier));

        await StopMovement();
        await RunTicks(2);

        Assert.That(Comp<LayingDownComponent>(Player).IsSliding, Is.False);
        Assert.That(Comp<StandingStateComponent>(Player).CurrentState, Is.EqualTo(StandingState.Lying));
        Assert.That(Comp<MovementSpeedModifierComponent>(Player).WalkSpeedModifier,
            Is.EqualTo(Comp<LayingDownComponent>(Player).SpeedModify));
    }

    [Test]
    public async Task NormalCooldownPreventsSlideUntilThreeSecondsPass()
    {
        await PrepareRunningWithShift();
        await PressToggleStanding();
        Assert.That(Comp<LayingDownComponent>(Player).IsSliding);

        await StopMovement();
        await RunTicks(2);
        Assert.That(Comp<LayingDownComponent>(Player).IsSliding, Is.False);
        Assert.That(Comp<StandingStateComponent>(Player).CurrentState, Is.EqualTo(StandingState.Lying));

        await Server.WaitPost(() =>
        {
            var uid = SEntMan.GetEntity(Player);
            SEntMan.System<StandingStateSystem>().Stand(uid, force: true);
        });
        await RestartRunningInput();
        await PressToggleStanding();

        Assert.That(Comp<LayingDownComponent>(Player).IsSliding, Is.False);
        var remainingCooldown = Comp<LayingDownComponent>(Player).SlideCooldownEndsAt - STiming.CurTime;
        Assert.That(remainingCooldown.TotalSeconds, Is.GreaterThan(0));

        await StopMovement();
        await RunTicks(190);
        await Server.WaitPost(() =>
        {
            var uid = SEntMan.GetEntity(Player);
            SEntMan.System<StandingStateSystem>().Stand(uid, force: true);
        });
        await RestartRunningInput();
        await PressToggleStanding();

        Assert.That(Comp<LayingDownComponent>(Player).IsSliding);
    }

    [Test]
    public async Task PressingToggleDuringSlideStandsImmediatelyAndUsesFiveSecondCooldown()
    {
        await PrepareRunningWithShift();
        await PressToggleStanding();
        Assert.That(Comp<LayingDownComponent>(Player).IsSliding);

        await PressToggleStanding();

        Assert.That(Comp<LayingDownComponent>(Player).IsSliding, Is.False);
        Assert.That(Comp<StandingStateComponent>(Player).CurrentState, Is.EqualTo(StandingState.Standing));
        var remainingCooldown = Comp<LayingDownComponent>(Player).SlideCooldownEndsAt - STiming.CurTime;
        Assert.That(remainingCooldown.TotalSeconds, Is.GreaterThan(4.9));

        await PressToggleStanding();
        Assert.That(Comp<LayingDownComponent>(Player).IsSliding, Is.False);
    }

    [Test]
    public async Task StandingStillUsesNormalLieDownBehavior()
    {
        await SetKey(EngineKeyFunctions.Walk, BoundKeyState.Down);
        await PressToggleStanding();

        Assert.That(Comp<LayingDownComponent>(Player).IsSliding, Is.False);
        Assert.That(Comp<StandingStateComponent>(Player).CurrentState, Is.EqualTo(StandingState.Lying));
    }

    [Test]
    public async Task WalkingUsesNormalLieDownBehavior()
    {
        await SetKey(EngineKeyFunctions.Walk, BoundKeyState.Down);
        await SetKey(EngineKeyFunctions.MoveRight, BoundKeyState.Down);
        await RunTicks(1);
        await Server.WaitPost(() =>
        {
            SEntMan.System<SharedPhysicsSystem>().SetLinearVelocity(SEntMan.GetEntity(Player), new Vector2(2f, 0f));
        });

        Assert.That(Comp<PhysicsComponent>(Player).LinearVelocity.Length(), Is.GreaterThan(0.5f));
        await PressToggleStanding();

        Assert.That(Comp<LayingDownComponent>(Player).IsSliding, Is.False);
        Assert.That(Comp<StandingStateComponent>(Player).CurrentState, Is.EqualTo(StandingState.Lying));
    }

    private async Task PrepareRunningWithShift()
    {
        await Server.WaitPost(() =>
        {
            var uid = SEntMan.GetEntity(Player);
            SEntMan.System<MovementSpeedModifierSystem>().ChangeBaseSpeed(uid, 5f, 2.5f, 20f);
        });

        await RestartRunningInput();
        Assert.That(Comp<PhysicsComponent>(Player).LinearVelocity.Length(), Is.GreaterThan(0.5f));
    }

    private async Task RestartRunningInput()
    {
        await SetKey(EngineKeyFunctions.Walk, BoundKeyState.Down);
        await SetKey(EngineKeyFunctions.MoveRight, BoundKeyState.Down);
        await RunTicks(1);
        await Server.WaitPost(() =>
        {
            SEntMan.System<SharedPhysicsSystem>().SetLinearVelocity(SEntMan.GetEntity(Player), new Vector2(2f, 0f));
        });
    }

    private async Task StopMovement()
    {
        await SetKey(EngineKeyFunctions.MoveRight, BoundKeyState.Up);
        await Server.WaitPost(() =>
        {
            SEntMan.System<SharedPhysicsSystem>().SetLinearVelocity(SEntMan.GetEntity(Player), Vector2.Zero);
        });
    }

    private async Task PressToggleStanding()
    {
        await SetKey(ContentKeyFunctions.ToggleStanding, BoundKeyState.Down);
        await RunTicks(1);
        await SetKey(ContentKeyFunctions.ToggleStanding, BoundKeyState.Up);
        await RunTicks(1);
    }
}

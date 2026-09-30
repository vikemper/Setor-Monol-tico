using Content.Shared.DoAfter;
using Content.Shared.Gravity;
using Content.Shared.Input;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Traits.Assorted; //Mono: Wheelchair user check
using Robust.Shared.Input.Binding;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._White.Standing;

public abstract partial class SharedLayingDownSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedGravitySystem _gravity = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private IGameTiming _timing = default!;
    public override void Initialize()
    {
        CommandBinds.Builder
            .Bind(ContentKeyFunctions.ToggleStanding, InputCmdHandler.FromDelegate(ToggleStanding))
            .Register<SharedLayingDownSystem>();

        SubscribeNetworkEvent<ChangeLayingDownEvent>(OnChangeState);

        SubscribeLocalEvent<StandingStateComponent, StandingUpDoAfterEvent>(OnStandingUpDoAfter);
        SubscribeLocalEvent<LayingDownComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeed);
        SubscribeLocalEvent<LayingDownComponent, RefreshWeightlessModifiersEvent>(OnRefreshWeightlessModifier);
        SubscribeLocalEvent<LayingDownComponent, EntParentChangedMessage>(OnParentChanged);

    }

    public override void Shutdown()
    {
        base.Shutdown();

        CommandBinds.Unregister<SharedLayingDownSystem>();
    }

    private void ToggleStanding(ICommonSession? session)
    {
        if (session?.AttachedEntity == null ||
            !HasComp<LayingDownComponent>(session.AttachedEntity) ||
            !CanLieDown(session.AttachedEntity.Value)) // Mono
        {
            return;
        }

        RaiseNetworkEvent(new ChangeLayingDownEvent());
    }

    private void OnChangeState(ChangeLayingDownEvent ev, EntitySessionEventArgs args)
    {
        if (!args.SenderSession.AttachedEntity.HasValue)
            return;

        var uid = args.SenderSession.AttachedEntity.Value;

        if (HasComp<LegsParalyzedComponent>(uid)) //Mono: Stop wheelchair user trait from standing
            return;

        // TODO: Wizard
        //if (HasComp<FrozenComponent>(uid))
        //   return;

        if (!TryComp(uid, out StandingStateComponent? standing) ||
            !TryComp(uid, out LayingDownComponent? layingDown))
        {
            return;
        }

        RaiseNetworkEvent(new CheckAutoGetUpEvent(GetNetEntity(uid)));

        if (HasComp<KnockedDownComponent>(uid) || !_mobState.IsAlive(uid))
            return;

        if (layingDown.IsSliding && _standing.IsDown(uid, standing))
        {
            if (!_standing.Stand(uid, standing, force: true))
                return;

            StopSlide(uid, layingDown, TimeSpan.FromSeconds(5));
        }
        else if (_standing.IsDown(uid, standing))
            TryStandUp(uid, layingDown, standing);
        else if (TryLieDown(uid, layingDown, standing) &&
                 _timing.CurTime >= layingDown.SlideCooldownEndsAt &&
                 IsRunning(uid))
        {
            StartSlide(uid, layingDown);
        }
    }

    private void OnStandingUpDoAfter(EntityUid uid, StandingStateComponent component, StandingUpDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || HasComp<KnockedDownComponent>(uid) ||
            _mobState.IsIncapacitated(uid) || !_standing.Stand(uid))
        {
            component.CurrentState = StandingState.Lying;
            return;
        }

        component.CurrentState = StandingState.Standing;
    }

    private void OnRefreshMovementSpeed(EntityUid uid, LayingDownComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        if (_standing.IsDown(uid))
        {
            var modifier = component.IsSliding ? component.SlideSpeedModifier : component.SpeedModify;
            args.ModifySpeed(modifier, modifier);
        }
        else
            args.ModifySpeed(1f, 1f);
    }

    private bool IsRunning(EntityUid uid)
    {
        if (!TryComp(uid, out InputMoverComponent? mover) ||
            !TryComp(uid, out MovementSpeedModifierComponent? movementSpeed) ||
            !TryComp(uid, out PhysicsComponent? physics))
        {
            return false;
        }

        const MoveButtons movementButtons = MoveButtons.Up | MoveButtons.Down | MoveButtons.Left | MoveButtons.Right;
        if ((mover.HeldMoveButtons & movementButtons) == MoveButtons.None ||
            physics.LinearVelocity.LengthSquared() < 0.25f)
        {
            return false;
        }

        var walkSpeed = movementSpeed.CurrentWalkSpeed;
        var sprintSpeed = movementSpeed.CurrentSprintSpeed;
        return mover.Sprinting ? sprintSpeed > walkSpeed : walkSpeed > sprintSpeed;
    }

    private void StartSlide(EntityUid uid, LayingDownComponent component)
    {
        component.IsSliding = true;
        component.SlideCooldownEndsAt = _timing.CurTime + TimeSpan.FromSeconds(3);
        Dirty(uid, component);
        _movementSpeed.RefreshMovementSpeedModifiers(uid);
    }

    private void StopSlide(EntityUid uid, LayingDownComponent component, TimeSpan cooldown)
    {
        component.IsSliding = false;
        component.SlideCooldownEndsAt = _timing.CurTime + cooldown;
        Dirty(uid, component);
        _movementSpeed.RefreshMovementSpeedModifiers(uid);
    }

    protected void EndSlide(EntityUid uid, LayingDownComponent component)
    {
        if (!component.IsSliding)
            return;

        component.IsSliding = false;
        Dirty(uid, component);
        _movementSpeed.RefreshMovementSpeedModifiers(uid);
    }

    // Mono edit
    private void OnRefreshWeightlessModifier(EntityUid uid, LayingDownComponent component, ref RefreshWeightlessModifiersEvent args)
    {
        if (!_standing.IsDown(uid))
            return;
        args.ModifyAcceleration(1f, 0.10f);
    }

    private void OnParentChanged(EntityUid uid, LayingDownComponent component, EntParentChangedMessage args)
    {
        // If the entity is not on a grid, try to make it stand up to avoid issues
        if (!TryComp<StandingStateComponent>(uid, out var standingState)
            || standingState.CurrentState is StandingState.Standing
            || CanLieDown(uid) // Mono
            || HasComp<LegsParalyzedComponent>(uid)) // Mono
        {
            return;
        }

        _standing.Stand(uid, standingState);
    }

    public bool TryStandUp(EntityUid uid, LayingDownComponent? layingDown = null, StandingStateComponent? standingState = null)
    {
        if (!Resolve(uid, ref standingState, false) ||
            !Resolve(uid, ref layingDown, false) ||
            standingState.CurrentState is not StandingState.Lying ||
            !_mobState.IsAlive(uid) ||
            TerminatingOrDeleted(uid))
        {
            return false;
        }

        var args = new DoAfterArgs(EntityManager, uid, layingDown.StandingUpTime, new StandingUpDoAfterEvent(), uid)
        {
            BreakOnDamage = true,
            BreakOnHandChange = false,
            RequireCanInteract = false,
            MultiplyDelay = false, // Goobstation
        };

        if (!_doAfter.TryStartDoAfter(args))
            return false;

        standingState.CurrentState = StandingState.GettingUp;
        return true;
    }

    public bool TryLieDown(EntityUid uid, LayingDownComponent? layingDown = null, StandingStateComponent? standingState = null, DropHeldItemsBehavior behavior = DropHeldItemsBehavior.NoDrop)
    {
        if (!Resolve(uid, ref standingState, false) ||
            !Resolve(uid, ref layingDown, false) ||
            standingState.CurrentState is not StandingState.Standing ||
            !CanLieDown(uid)) // Mono
        {
            if (behavior == DropHeldItemsBehavior.AlwaysDrop)
            {
                var ev = new DropHandItemsEvent();
                RaiseLocalEvent(uid, ref ev, false);
            }
            return false;
        }

        _standing.Down(uid, true, behavior != DropHeldItemsBehavior.NoDrop, false, standingState);
        return true;
    }

    // Mono
    private bool CanLieDown(EntityUid uid)
    {
        return _gravity.EntityOnGravitySupportingGridOrMap(uid);
    }
}

[Serializable, NetSerializable]
public sealed partial class StandingUpDoAfterEvent : SimpleDoAfterEvent;

public enum DropHeldItemsBehavior : byte
{
    NoDrop,
    DropIfStanding,
    AlwaysDrop
}

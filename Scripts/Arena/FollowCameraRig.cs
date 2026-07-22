// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Arena/FollowCameraRig.cs
// Purpose: Arena gameplay/runtime support (3D world, pawns, VFX).
// -------------------------------------------------------------------------------------------------
using System;
using Godot;

namespace WastelandSurvivor.Game.Arena;

/// <summary>
/// Fixed RTS-ish follow camera. Locked to a target Node3D.
/// We keep the camera orientation fixed (no orbit) and simply translate.
/// </summary>
public partial class FollowCameraRig : Node3D
{
	// USER DIRECTION (2026-06-11): keep the high RTS / "toy cars" zoom — it's part of the game's
	// charm. Do NOT pull the camera closer for a third-person action feel.
	[Export] public Vector3 Offset = new(0f, 29f, 23f);
	[Export] public float FollowLerp = 10f;

	// Gentle combat framing bias toward the locked enemy (does not change zoom; just keeps the
	// engagement centered). Kept small so the player never feels off-center at RTS height.
	[Export] public float LookAheadFraction = 0.22f;
	[Export] public float LookAheadMaxMeters = 5.5f;

	private Node3D? _target;
	private Node3D? _lookAheadTarget;
	private Camera3D? _camera;

	// Impact shake (decaying positional noise; rotation stays fixed so readability never suffers).
	private float _shakeAmplitude;
	private float _shakeTime;

	public override void _Ready()
	{
		// Be explicit: ensure processing is enabled so the rig tracks as soon as it's added.
		SetProcess(true);
		_camera = GetNodeOrNull<Camera3D>("Camera3D");
		if (_camera != null) _camera.Current = true;
	}

	public void SetTarget(Node3D? target)
	{
		_target = target;

		// Snap immediately so the first rendered frame has a sane camera pose.
		// IMPORTANT: GlobalPosition access throws warnings if the target isn't in the scene tree yet.
		if (_target == null || !GodotObject.IsInstanceValid(_target))
			return;

		if (!_target.IsInsideTree())
		{
			CallDeferred(nameof(SnapToTarget));
			return;
		}

		SnapToTarget();
	}

	/// <summary>Combat lookahead: when set, the camera frames a point between pawn and enemy.</summary>
	public void SetLookAheadTarget(Node3D? target) => _lookAheadTarget = target;

	/// <summary>Kick the camera (explosions, heavy hits). Amplitude in meters; decays quickly.</summary>
	public void AddShake(float amplitude)
	{
		_shakeAmplitude = MathF.Min(0.9f, _shakeAmplitude + MathF.Max(0f, amplitude));
	}

	private Vector3 ComputeFocusPoint()
	{
		var focus = _target!.GlobalPosition;
		if (_lookAheadTarget != null && GodotObject.IsInstanceValid(_lookAheadTarget) && _lookAheadTarget.IsInsideTree())
		{
			var to = _lookAheadTarget.GlobalPosition - focus;
			to.Y = 0f;
			focus += to.LimitLength(LookAheadMaxMeters / MathF.Max(0.05f, LookAheadFraction)) * LookAheadFraction;
		}
		return focus;
	}

	private void SnapToTarget()
	{
		if (_target == null || !GodotObject.IsInstanceValid(_target)) return;
		if (!_target.IsInsideTree()) return;

		var focus = ComputeFocusPoint();
		GlobalPosition = focus + Offset;
		LookAt(focus, Vector3.Up);
	}

	public override void _Process(double delta)
	{
		if (_target == null || !GodotObject.IsInstanceValid(_target)) return;
		if (!_target.IsInsideTree()) return;

		var dt = (float)delta;
		var focus = ComputeFocusPoint();
		var desired = focus + Offset;
		var basePos = GlobalPosition;

		// Strip last frame's shake before lerping so it doesn't accumulate into the follow path.
		basePos -= _lastShakeOffset;
		basePos = basePos.Lerp(desired, 1f - Mathf.Exp(-FollowLerp * dt));

		_lastShakeOffset = Vector3.Zero;
		if (_shakeAmplitude > 0.002f)
		{
			_shakeTime += dt * 34f;
			_lastShakeOffset = new Vector3(
				(Mathf.Sin(_shakeTime * 1.3f) + Mathf.Sin(_shakeTime * 2.7f) * 0.5f) * _shakeAmplitude * 0.5f,
				0f,
				(Mathf.Cos(_shakeTime * 1.7f) + Mathf.Sin(_shakeTime * 2.1f) * 0.5f) * _shakeAmplitude * 0.5f);
			_shakeAmplitude = MathF.Max(0f, _shakeAmplitude - dt * 2.6f);
		}

		GlobalPosition = basePos + _lastShakeOffset;
		LookAt(focus + _lastShakeOffset, Vector3.Up);
	}

	private Vector3 _lastShakeOffset;
}

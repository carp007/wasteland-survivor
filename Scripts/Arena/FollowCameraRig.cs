using Godot;

namespace WastelandSurvivor.Game.Arena;

/// <summary>
/// Fixed RTS-ish follow camera. Locked to a target Node3D.
/// We keep the camera orientation fixed (no orbit) and simply translate.
/// </summary>
public partial class FollowCameraRig : Node3D
{
	[Export] public Vector3 Offset = new(0f, 29f, 23f);
	[Export] public float FollowLerp = 10f;

	private Node3D? _target;
	private Camera3D? _camera;

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
		if (_target != null && GodotObject.IsInstanceValid(_target))
		{
			GlobalPosition = _target.GlobalPosition + Offset;
			LookAt(_target.GlobalPosition, Vector3.Up);
		}
	}

	public override void _Process(double delta)
	{
		if (_target == null || !GodotObject.IsInstanceValid(_target)) return;
		var dt = (float)delta;
		var desired = _target.GlobalPosition + Offset;
		GlobalPosition = GlobalPosition.Lerp(desired, 1f - Mathf.Exp(-FollowLerp * dt));
		LookAt(_target.GlobalPosition, Vector3.Up);
	}
}

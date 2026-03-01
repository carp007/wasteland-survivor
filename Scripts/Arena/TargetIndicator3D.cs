// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Arena/TargetIndicator3D.cs
// Purpose: Arena gameplay/runtime support (3D world, pawns, VFX).
// -------------------------------------------------------------------------------------------------
using Godot;

namespace WastelandSurvivor.Game.Arena;

/// <summary>
/// Simple 3D target indicator for the currently selected target.
/// Minimal-risk: a glowing ring on the ground + a small arrow above the target.
/// </summary>
public partial class TargetIndicator3D : Node3D
{
	[Export] public Color RingColor = new(1.0f, 0.85f, 0.15f);
	[Export] public float RingRadius = 1.55f;
	[Export] public float RingThickness = 0.09f;
	[Export] public float GroundY = 0.02f;
	[Export] public float ArrowHeight = 1.35f;

	private Node3D? _target;
	private MeshInstance3D? _ring;
	private MeshInstance3D? _arrow;
	private float _spin = 0f;

	public override void _Ready()
	{
		Visible = false;
		BuildVisuals();
		SetProcess(true);
	}

	public void SetTarget(Node3D? target)
	{
		_target = (target != null && GodotObject.IsInstanceValid(target)) ? target : null;
		Visible = _target != null;
		if (_target != null)
			UpdateTransformImmediate();
	}

	public override void _Process(double delta)
	{
		if (_target == null || !GodotObject.IsInstanceValid(_target))
		{
			Visible = false;
			_target = null;
			return;
		}

		_spin += (float)delta * 1.3f;
		if (_ring != null)
			_ring.Rotation = new Vector3(0f, _spin, 0f);

		if (_arrow != null)
			_arrow.Rotation = new Vector3(0f, -_spin * 0.75f, 0f);

		UpdateTransformImmediate();
	}

	private void UpdateTransformImmediate()
	{
		if (_target == null) return;
		var p = _target.GlobalPosition;
		GlobalPosition = new Vector3(p.X, 0f, p.Z);
		if (_ring != null)
			_ring.GlobalPosition = new Vector3(p.X, GroundY, p.Z);
		if (_arrow != null)
			_arrow.GlobalPosition = new Vector3(p.X, ArrowHeight, p.Z);
	}

	private void BuildVisuals()
	{
		// NOTE: Godot's C# surface API differs across versions for some primitive meshes.
		// To keep this compile-safe across 4.6.x, build a thin ring mesh procedurally.

		var ringMat = new StandardMaterial3D
		{
			AlbedoColor = RingColor,
			EmissionEnabled = true,
			Emission = RingColor * 0.85f,
			Roughness = 0.15f,
			Metallic = 0.0f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled
		};

		// Ring
		_ring = new MeshInstance3D { Name = "Ring" };
		_ring.Mesh = BuildRingMesh(ringMat);
		AddChild(_ring);

		// Arrow (simple box "pip")
		_arrow = new MeshInstance3D { Name = "Arrow" };
		_arrow.Mesh = new BoxMesh { Size = new Vector3(0.20f, 0.18f, 0.20f) };
		var arrowMat = new StandardMaterial3D
		{
			AlbedoColor = RingColor,
			EmissionEnabled = true,
			Emission = RingColor * 0.95f,
			Roughness = 0.1f,
			Metallic = 0.0f,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
		};
		_arrow.SetSurfaceOverrideMaterial(0, arrowMat);
		AddChild(_arrow);
	}

	private Mesh BuildRingMesh(Material mat)
	{
		// Build a thin ring in the XZ plane at y=0.
		var segments = 48;
		var outer = RingRadius;
		var inner = Mathf.Max(0.05f, RingRadius - RingThickness);

		var im = new ImmediateMesh();
		im.SurfaceBegin(Mesh.PrimitiveType.Triangles, mat);

		for (var i = 0; i < segments; i++)
		{
			var a0 = (float)i / segments * Mathf.Tau;
			var a1 = (float)(i + 1) / segments * Mathf.Tau;
			var co0 = Mathf.Cos(a0);
			var si0 = Mathf.Sin(a0);
			var co1 = Mathf.Cos(a1);
			var si1 = Mathf.Sin(a1);

			var o0 = new Vector3(co0 * outer, 0f, si0 * outer);
			var o1 = new Vector3(co1 * outer, 0f, si1 * outer);
			var i0v = new Vector3(co0 * inner, 0f, si0 * inner);
			var i1v = new Vector3(co1 * inner, 0f, si1 * inner);

			// Two triangles per segment
			im.SurfaceSetNormal(Vector3.Up);
			im.SurfaceAddVertex(o0);
			im.SurfaceSetNormal(Vector3.Up);
			im.SurfaceAddVertex(o1);
			im.SurfaceSetNormal(Vector3.Up);
			im.SurfaceAddVertex(i1v);

			im.SurfaceSetNormal(Vector3.Up);
			im.SurfaceAddVertex(o0);
			im.SurfaceSetNormal(Vector3.Up);
			im.SurfaceAddVertex(i1v);
			im.SurfaceSetNormal(Vector3.Up);
			im.SurfaceAddVertex(i0v);
		}

		im.SurfaceEnd();
		return im;
	}
}

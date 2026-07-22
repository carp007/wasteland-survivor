// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Arena/TargetIndicator3D.cs
// Purpose: Arena gameplay/runtime support (3D world, pawns, VFX).
// -------------------------------------------------------------------------------------------------
using Godot;

namespace WastelandSurvivor.Game.Arena;

/// <summary>
/// 3D indicator for the currently locked target: a bright gold ground ring, four L-shaped corner
/// brackets that breathe in/out at the vehicle's footprint, and a billboarded chevron bobbing
/// above the target so it stays legible from the fixed RTS camera at ~37 m.
/// </summary>
public partial class TargetIndicator3D : Node3D
{
	[Export] public Color RingColor = new(1.0f, 0.85f, 0.15f);
	[Export] public float RingRadius = 1.55f;
	[Export] public float RingThickness = 0.14f;
	[Export] public float GroundY = 0.02f;
	[Export] public float ArrowHeight = 2.0f;
	[Export] public float BracketRadius = 2.05f;

	private Node3D? _target;
	private MeshInstance3D? _ring;
	private MeshInstance3D? _brackets;
	private MeshInstance3D? _chevron;
	private float _spin;
	private float _time;

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

		var dt = (float)delta;
		_spin += dt * 1.3f;
		_time += dt;

		if (_ring != null)
			_ring.Rotation = new Vector3(0f, _spin, 0f);

		if (_brackets != null)
		{
			// Gentle breathe: brackets ease outward/inward around the vehicle bounds.
			var breathe = 1f + 0.08f * Mathf.Sin(_time * 2.4f);
			_brackets.Scale = new Vector3(breathe, 1f, breathe);
		}

		UpdateTransformImmediate();
	}

	private void UpdateTransformImmediate()
	{
		if (_target == null) return;
		var p = _target.GlobalPosition;
		GlobalPosition = new Vector3(p.X, 0f, p.Z);
		if (_ring != null)
			_ring.GlobalPosition = new Vector3(p.X, GroundY, p.Z);
		if (_brackets != null)
			_brackets.GlobalPosition = new Vector3(p.X, GroundY + 0.015f, p.Z);
		if (_chevron != null)
		{
			var bob = Mathf.Sin(_time * 1.7f) * 0.16f;
			_chevron.GlobalPosition = new Vector3(p.X, ArrowHeight + bob, p.Z);
		}
	}

	private void BuildVisuals()
	{
		// NOTE: Godot's C# surface API differs across versions for some primitive meshes.
		// To keep this compile-safe across 4.6.x, all shapes are built procedurally.

		var ringMat = MakeIndicatorMaterial(emissionScale: 1.9f);

		// Ground ring (spins slowly).
		_ring = new MeshInstance3D { Name = "Ring" };
		_ring.Mesh = BuildRingMesh(ringMat);
		AddChild(_ring);

		// Four L-shaped corner brackets at the vehicle's footprint (breathe in/out, no spin).
		var bracketMat = MakeIndicatorMaterial(emissionScale: 2.1f);
		_brackets = new MeshInstance3D { Name = "Brackets" };
		_brackets.Mesh = BuildBracketsMesh(bracketMat);
		AddChild(_brackets);

		// Floating chevron above the vehicle. Billboarded so it always faces the camera; flat
		// triangle geometry (no box meshes for airborne markers).
		var chevronMat = MakeIndicatorMaterial(emissionScale: 2.2f);
		chevronMat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
		chevronMat.BillboardKeepScale = true;
		_chevron = new MeshInstance3D { Name = "Chevron" };
		_chevron.Mesh = BuildChevronMesh(chevronMat);
		AddChild(_chevron);
	}

	private StandardMaterial3D MakeIndicatorMaterial(float emissionScale)
		=> new()
		{
			AlbedoColor = RingColor,
			EmissionEnabled = true,
			Emission = RingColor * emissionScale,
			Roughness = 0.15f,
			Metallic = 0.0f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
		};

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

	/// <summary>Four flat L-brackets framing the vehicle bounds, arms pointing along ±X/±Z.</summary>
	private Mesh BuildBracketsMesh(Material mat)
	{
		var im = new ImmediateMesh();
		im.SurfaceBegin(Mesh.PrimitiveType.Triangles, mat);

		var r = Mathf.Max(0.6f, BracketRadius);
		var arm = Mathf.Max(0.35f, r * 0.5f);
		var width = 0.13f;

		foreach (var sx in new[] { -1f, 1f })
		{
			foreach (var sz in new[] { -1f, 1f })
			{
				var cornerX = sx * r;
				var cornerZ = sz * r;
				// Arm running along X toward the frame edge, then the arm along Z.
				AddQuadXZ(im, cornerX, cornerZ, cornerX - sx * arm, cornerZ - sz * width);
				AddQuadXZ(im, cornerX, cornerZ, cornerX - sx * width, cornerZ - sz * arm);
			}
		}

		im.SurfaceEnd();
		return im;
	}

	private static void AddQuadXZ(ImmediateMesh im, float xa, float za, float xb, float zb)
	{
		var x0 = Mathf.Min(xa, xb);
		var x1 = Mathf.Max(xa, xb);
		var z0 = Mathf.Min(za, zb);
		var z1 = Mathf.Max(za, zb);

		var a = new Vector3(x0, 0f, z0);
		var b = new Vector3(x1, 0f, z0);
		var c = new Vector3(x1, 0f, z1);
		var d = new Vector3(x0, 0f, z1);

		im.SurfaceSetNormal(Vector3.Up);
		im.SurfaceAddVertex(a);
		im.SurfaceSetNormal(Vector3.Up);
		im.SurfaceAddVertex(b);
		im.SurfaceSetNormal(Vector3.Up);
		im.SurfaceAddVertex(c);

		im.SurfaceSetNormal(Vector3.Up);
		im.SurfaceAddVertex(a);
		im.SurfaceSetNormal(Vector3.Up);
		im.SurfaceAddVertex(c);
		im.SurfaceSetNormal(Vector3.Up);
		im.SurfaceAddVertex(d);
	}

	/// <summary>Down-pointing chevron in the local XY plane (billboarded by its material).</summary>
	private static Mesh BuildChevronMesh(Material mat)
	{
		var im = new ImmediateMesh();
		im.SurfaceBegin(Mesh.PrimitiveType.Triangles, mat);

		const float halfW = 0.46f;
		const float height = 0.52f;
		var normal = new Vector3(0f, 0f, 1f);

		// Solid down-pointing triangle.
		im.SurfaceSetNormal(normal);
		im.SurfaceAddVertex(new Vector3(-halfW, height * 0.5f, 0f));
		im.SurfaceSetNormal(normal);
		im.SurfaceAddVertex(new Vector3(halfW, height * 0.5f, 0f));
		im.SurfaceSetNormal(normal);
		im.SurfaceAddVertex(new Vector3(0f, -height * 0.5f, 0f));

		// Short echo bar above the triangle for a double-chevron read.
		var barHalfW = halfW * 0.55f;
		var barY0 = height * 0.5f + 0.10f;
		var barY1 = barY0 + 0.09f;
		im.SurfaceSetNormal(normal);
		im.SurfaceAddVertex(new Vector3(-barHalfW, barY0, 0f));
		im.SurfaceSetNormal(normal);
		im.SurfaceAddVertex(new Vector3(barHalfW, barY0, 0f));
		im.SurfaceSetNormal(normal);
		im.SurfaceAddVertex(new Vector3(barHalfW, barY1, 0f));

		im.SurfaceSetNormal(normal);
		im.SurfaceAddVertex(new Vector3(-barHalfW, barY0, 0f));
		im.SurfaceSetNormal(normal);
		im.SurfaceAddVertex(new Vector3(barHalfW, barY1, 0f));
		im.SurfaceSetNormal(normal);
		im.SurfaceAddVertex(new Vector3(-barHalfW, barY1, 0f));

		im.SurfaceEnd();
		return im;
	}
}

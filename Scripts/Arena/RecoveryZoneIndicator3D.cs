// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Arena/RecoveryZoneIndicator3D.cs
// Purpose: Arena gameplay/runtime support (3D world, pawns, VFX).
// -------------------------------------------------------------------------------------------------
using Godot;

namespace WastelandSurvivor.Game.Arena;

/// <summary>
/// Highlights the player recovery/start box during the post-win salvage phase.
/// Uses simple emissive meshes so the area reads clearly from the fixed top-down camera
/// without depending on external assets.
/// </summary>
public partial class RecoveryZoneIndicator3D : Node3D
{
	private const float OutlineThickness = 0.32f;
	private const float OutlineHeight = 0.05f;
	private const float FillY = 0.03f;
	private const float BeaconBaseHeight = 1.45f;

	private Vector3 _center = Vector3.Zero;
	private Vector2 _size = new(30f, 24f);
	private bool _active = false;
	private bool _towingAttached = false;
	private bool _insideZone = false;
	private bool _autoFinishing = false;
	private float _pulse = 0f;

	private MeshInstance3D? _fill;
	private MeshInstance3D? _beacon;
	private MeshInstance3D? _northEdge;
	private MeshInstance3D? _southEdge;
	private MeshInstance3D? _eastEdge;
	private MeshInstance3D? _westEdge;
	private StandardMaterial3D? _fillMaterial;
	private StandardMaterial3D? _outlineMaterial;
	private StandardMaterial3D? _beaconMaterial;

	public override void _Ready()
	{
		BuildVisuals();
		ApplyLayout();
		Visible = false;
		SetProcess(true);
	}

	public override void _Process(double delta)
	{
		if (!_active || !Visible || _beacon == null || !GodotObject.IsInstanceValid(_beacon))
			return;

		_pulse += (float)delta;
		var speed = _autoFinishing ? 6.0f : (_insideZone ? 4.5f : 2.7f);
		var wave = 0.5f + (0.5f * Mathf.Sin(_pulse * speed));
		var height = BeaconBaseHeight * (0.90f + (wave * 0.30f));
		_beacon.Scale = new Vector3(1f, height / BeaconBaseHeight, 1f);
		_beacon.Position = new Vector3(0f, (height * 0.5f) + 0.12f, 0f);
	}

	public void Configure(Vector3 center, Vector2 size)
	{
		_center = center;
		_size = new Vector2(Mathf.Max(4f, size.X), Mathf.Max(4f, size.Y));
		ApplyLayout();
	}

	public void SetPresentation(bool active, bool towingAttached, bool insideZone, bool autoFinishing)
	{
		_active = active;
		_towingAttached = towingAttached;
		_insideZone = insideZone;
		_autoFinishing = autoFinishing;
		Visible = active;
		if (!active)
			return;

		var color = DetermineColor(towingAttached, insideZone, autoFinishing);
		var fillAlpha = autoFinishing ? 0.22f : (insideZone ? 0.18f : (towingAttached ? 0.13f : 0.08f));
		ApplyColor(color, fillAlpha);
	}

	private static Color DetermineColor(bool towingAttached, bool insideZone, bool autoFinishing)
	{
		if (autoFinishing)
			return new Color(0.36f, 1.0f, 0.55f, 1f);
		if (insideZone)
			return new Color(0.34f, 0.95f, 0.55f, 1f);
		if (towingAttached)
			return new Color(1.0f, 0.84f, 0.22f, 1f);
		return new Color(0.25f, 0.80f, 1.0f, 1f);
	}

	private void BuildVisuals()
	{
		_outlineMaterial = BuildMaterial(new Color(1f, 0.84f, 0.22f), alpha: 0.95f, emissionEnergy: 1.1f);
		_fillMaterial = BuildMaterial(new Color(1f, 0.84f, 0.22f), alpha: 0.12f, emissionEnergy: 0.55f);
		_beaconMaterial = BuildMaterial(new Color(1f, 0.84f, 0.22f), alpha: 0.30f, emissionEnergy: 1.3f);

		_fill = new MeshInstance3D
		{
			Name = "Fill",
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			Mesh = new QuadMesh { Size = _size }
		};
		_fill.Rotation = new Vector3(-Mathf.Pi * 0.5f, 0f, 0f);
		_fill.SetSurfaceOverrideMaterial(0, _fillMaterial);
		AddChild(_fill);

		_northEdge = CreateEdge("NorthEdge");
		_southEdge = CreateEdge("SouthEdge");
		_eastEdge = CreateEdge("EastEdge");
		_westEdge = CreateEdge("WestEdge");

		_beacon = new MeshInstance3D
		{
			Name = "Beacon",
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			Mesh = new BoxMesh { Size = new Vector3(0.55f, BeaconBaseHeight, 0.55f) }
		};
		_beacon.SetSurfaceOverrideMaterial(0, _beaconMaterial);
		AddChild(_beacon);
	}

	private MeshInstance3D CreateEdge(string name)
	{
		var edge = new MeshInstance3D
		{
			Name = name,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			Mesh = new BoxMesh()
		};
		edge.SetSurfaceOverrideMaterial(0, _outlineMaterial);
		AddChild(edge);
		return edge;
	}

	private StandardMaterial3D BuildMaterial(Color color, float alpha, float emissionEnergy)
	{
		return new StandardMaterial3D
		{
			AlbedoColor = new Color(color.R, color.G, color.B, alpha),
			EmissionEnabled = true,
			Emission = color * emissionEnergy,
			Roughness = 0.18f,
			Metallic = 0.0f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
			NoDepthTest = false
		};
	}

	private void ApplyLayout()
	{
		Position = new Vector3(_center.X, 0f, _center.Z);
		var halfWidth = _size.X * 0.5f;
		var halfDepth = _size.Y * 0.5f;

		if (_fill != null && GodotObject.IsInstanceValid(_fill))
		{
			if (_fill.Mesh is QuadMesh quad)
				quad.Size = _size;
			_fill.Position = new Vector3(0f, FillY, 0f);
		}

		ApplyEdge(_northEdge, new Vector3(_size.X, OutlineHeight, OutlineThickness), new Vector3(0f, FillY + 0.01f, -halfDepth));
		ApplyEdge(_southEdge, new Vector3(_size.X, OutlineHeight, OutlineThickness), new Vector3(0f, FillY + 0.01f, halfDepth));
		ApplyEdge(_eastEdge, new Vector3(OutlineThickness, OutlineHeight, _size.Y + OutlineThickness), new Vector3(halfWidth, FillY + 0.01f, 0f));
		ApplyEdge(_westEdge, new Vector3(OutlineThickness, OutlineHeight, _size.Y + OutlineThickness), new Vector3(-halfWidth, FillY + 0.01f, 0f));

		if (_beacon != null && GodotObject.IsInstanceValid(_beacon))
			_beacon.Position = new Vector3(0f, (BeaconBaseHeight * 0.5f) + 0.12f, 0f);
	}

	private static void ApplyEdge(MeshInstance3D? edge, Vector3 size, Vector3 position)
	{
		if (edge == null || !GodotObject.IsInstanceValid(edge))
			return;
		if (edge.Mesh is BoxMesh box)
			box.Size = size;
		edge.Position = position;
	}

	private void ApplyColor(Color color, float fillAlpha)
	{
		if (_outlineMaterial != null)
		{
			_outlineMaterial.AlbedoColor = new Color(color.R, color.G, color.B, 0.95f);
			_outlineMaterial.Emission = color * 1.1f;
		}
		if (_fillMaterial != null)
		{
			_fillMaterial.AlbedoColor = new Color(color.R, color.G, color.B, fillAlpha);
			_fillMaterial.Emission = color * 0.55f;
		}
		if (_beaconMaterial != null)
		{
			var beaconAlpha = _autoFinishing ? 0.40f : (_insideZone ? 0.34f : (_towingAttached ? 0.30f : 0.22f));
			_beaconMaterial.AlbedoColor = new Color(color.R, color.G, color.B, beaconAlpha);
			_beaconMaterial.Emission = color * (_autoFinishing ? 1.45f : 1.25f);
		}
	}
}

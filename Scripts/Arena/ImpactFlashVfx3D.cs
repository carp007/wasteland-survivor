// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Arena/ImpactFlashVfx3D.cs
// Purpose: Soft additive radial impact flash (billboard). Replaces the opaque emissive spheres that
//          read as featureless white discs from the fixed RTS camera (eval round 6 P0-1).
// -------------------------------------------------------------------------------------------------
using System;
using Godot;

namespace WastelandSurvivor.Game.Arena;

/// <summary>
/// Short-lived billboard flash with a procedurally generated radial-falloff texture (soft core,
/// faint streak noise) that scales up and fades out. Additive, so overlapping hits build brightness
/// instead of stacking into a paper cutout.
/// </summary>
public partial class ImpactFlashVfx3D : Node3D
{
    private static ImageTexture? _radialTex;

    private float _ttl;
    private float _age;
    private float _radius;
    private MeshInstance3D? _quad;
    private StandardMaterial3D? _mat;
    private float _startAlpha;
    private float _emissionEnergy = 1.6f;

    public static void Spawn(Node3D parent, Vector3 atWorld, Color color, float radius, float ttlSeconds = 0.16f, float alpha = 0.9f, float emissionEnergy = 1.6f)
    {
        if (parent == null || !GodotObject.IsInstanceValid(parent))
            return;

        var node = new ImpactFlashVfx3D
        {
            Name = "ImpactFlash",
            _ttl = MathF.Max(0.03f, ttlSeconds),
            _radius = MathF.Max(0.05f, radius),
            _startAlpha = Math.Clamp(alpha, 0.05f, 1f),
            _emissionEnergy = MathF.Max(0.1f, emissionEnergy),
        };
        parent.AddChild(node);
        node.GlobalPosition = atWorld;
        node.BuildQuad(color);
    }

    private void BuildQuad(Color color)
    {
        _mat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            AlbedoColor = new Color(color.R, color.G, color.B, _startAlpha),
            AlbedoTexture = GetRadialTexture(),
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            EmissionEnabled = true,
            Emission = color,
            EmissionEnergyMultiplier = _emissionEnergy,
            DisableReceiveShadows = true,
        };
        _quad = new MeshInstance3D
        {
            Name = "FlashQuad",
            Mesh = new QuadMesh { Size = new Vector2(1f, 1f) },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _quad.SetSurfaceOverrideMaterial(0, _mat);
        // Random roll so repeated hits don't share streak orientation.
        _quad.RotationDegrees = new Vector3(0f, 0f, (float)(Random.Shared.NextDouble() * 360.0));
        AddChild(_quad);
        ApplyScale(0f);
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        var t = Mathf.Clamp(_age / _ttl, 0f, 1f);
        ApplyScale(t);
        if (_mat != null)
        {
            var c = _mat.AlbedoColor;
            _mat.AlbedoColor = new Color(c.R, c.G, c.B, _startAlpha * (1f - t) * (1f - t));
            _mat.EmissionEnergyMultiplier = Mathf.Lerp(_emissionEnergy, 0.1f, t);
        }
        if (_age >= _ttl)
            QueueFree();
    }

    private void ApplyScale(float t)
    {
        if (_quad == null) return;
        // Fast pop to ~60% then ease to full footprint.
        var grow = 0.55f + 0.45f * MathF.Sqrt(t);
        var s = _radius * 2f * grow;
        _quad.Scale = new Vector3(s, s, s);
    }

    /// <summary>Radial falloff with faint angular streaks: white core, quadratic alpha falloff.</summary>
    private static ImageTexture GetRadialTexture()
    {
        if (_radialTex != null) return _radialTex;

        const int size = 96;
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        var half = size * 0.5f;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = (x + 0.5f - half) / half;
                var dy = (y + 0.5f - half) / half;
                var r = MathF.Sqrt(dx * dx + dy * dy);
                if (r >= 1f)
                {
                    img.SetPixel(x, y, new Color(1f, 1f, 1f, 0f));
                    continue;
                }
                // Soft quadratic falloff + hot core boost.
                var a = (1f - r) * (1f - r);
                a += MathF.Max(0f, 0.5f - r) * 0.9f;
                // Faint 6-arm streak modulation so the flash has structure at 4x zoom.
                var ang = MathF.Atan2(dy, dx);
                a *= 0.82f + 0.18f * MathF.Abs(MathF.Cos(ang * 3f));
                img.SetPixel(x, y, new Color(1f, 1f, 1f, Math.Clamp(a, 0f, 1f)));
            }
        }
        _radialTex = ImageTexture.CreateFromImage(img);
        return _radialTex;
    }
}

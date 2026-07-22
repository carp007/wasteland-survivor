using System;
using Godot;
using WastelandSurvivor.Core.Defs;
using WastelandSurvivor.Core.State;

namespace WastelandSurvivor.Game.UI;

/// <summary>
/// Deterministic top-down vehicle preview drawn directly into the exact HUD control that is visible on-screen.
/// The goal is not a full 3D feed, but a readable card that still feels like the player's current car.
/// </summary>
public partial class VehiclePreviewCanvas : Control
{
    private VehicleClass _vehicleClass = VehicleClass.Compact;
    // Default matches the player paint (yellow) — the old green default leaked into the HUD
    // whenever the typed HUD bind fell back and never pushed a real body color.
    private Color _bodyColor = new(0.93f, 0.76f, 0.12f);
    private int _frontWeapons;
    private int _rearWeapons;
    private int _leftWeapons;
    private int _rightWeapons;
    private int _topWeapons;
    private VehicleDamageSnapshot _damage = VehicleDamageSnapshot.Pristine;
    private VehicleDamageOverlay? _damageOverlay;
    private bool _hasTowAttached;
    private Texture2D? _snapshotTexture;
    private bool _embeddedPreviewActive;
    private bool _combatReadout;

    public bool HasSnapshotTexture => _snapshotTexture != null;

    private readonly struct VehicleBodyProfile
    {
        public VehicleBodyProfile(float widthRatio, float heightRatio, float nosePinch, float shoulderWidth, float cabinWidth, float cabinHeight, float wheelInset, float wheelFront, float wheelRear, float rearPinch, bool boxy)
        {
            WidthRatio = widthRatio;
            HeightRatio = heightRatio;
            NosePinch = nosePinch;
            ShoulderWidth = shoulderWidth;
            CabinWidth = cabinWidth;
            CabinHeight = cabinHeight;
            WheelInset = wheelInset;
            WheelFront = wheelFront;
            WheelRear = wheelRear;
            RearPinch = rearPinch;
            Boxy = boxy;
        }

        public float WidthRatio { get; }
        public float HeightRatio { get; }
        public float NosePinch { get; }
        public float ShoulderWidth { get; }
        public float CabinWidth { get; }
        public float CabinHeight { get; }
        public float WheelInset { get; }
        public float WheelFront { get; }
        public float WheelRear { get; }
        public float RearPinch { get; }
        public bool Boxy { get; }
    }

    public VehiclePreviewCanvas()
    {
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Ignore;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        CustomMinimumSize = new Vector2(120f, 140f);
    }

    public void SetVehicle(VehicleDefinition def, VehicleInstanceState inst, Color bodyColor)
    {
        var changed = _vehicleClass != def.Class;
        _vehicleClass = def.Class;

        if (!ColorsClose(_bodyColor, bodyColor))
        {
            _bodyColor = bodyColor;
            changed = true;
        }

        changed |= CountWeapons(inst);
        changed |= CaptureDamageState(def, inst);

        // Keep the embedded-preview damage overlay in sync with the exact same escalation data.
        if (_damageOverlay != null && GodotObject.IsInstanceValid(_damageOverlay))
        {
            _damageOverlay.SetBodyColor(_bodyColor);
            _damageOverlay.SetDamage(_damage);
        }

        // The HUD pushes state every dynamic tick; only repaint when something actually changed.
        if (changed)
            QueueRedraw();
    }

    public void SetLiveYaw(float yaw)
    {
        // No-op kept for API compat (HUD + garage cards call it). The orientation ring was cut in
        // the combat HUD diet — the embedded 3D preview already rotates with the vehicle — which
        // also drops the per-frame silhouette repaints yaw changes used to trigger.
    }

    public void SetLiveBodyColor(Color color)
    {
        if (ColorsClose(_bodyColor, color))
            return;

        _bodyColor = color;
        if (_damageOverlay != null && GodotObject.IsInstanceValid(_damageOverlay))
            _damageOverlay.SetBodyColor(color);
        QueueRedraw();
    }

    public void SetSnapshotTexture(Texture2D? texture)
    {
        if (_snapshotTexture == texture)
            return;

        _snapshotTexture = texture;
        QueueRedraw();
    }

    public void SetEmbeddedPreviewActive(bool active)
    {
        if (_embeddedPreviewActive == active)
            return;

        _embeddedPreviewActive = active;
        UpdateDamageOverlayVisibility();
        QueueRedraw();
    }

    /// <summary>
    /// Combat HUD mode: the overlay's facing ring (front/rear/left/right armor+structure arcs) and
    /// Top/Under pips become the primary locational read, replacing the old bar grids. Off by default
    /// so garage/showcase/snapshot consumers keep a clean vehicle card.
    /// </summary>
    public void SetCombatReadoutEnabled(bool enabled)
    {
        if (_combatReadout == enabled)
            return;

        _combatReadout = enabled;
        UpdateDamageOverlayVisibility();
    }

    /// <summary>
    /// The damage readout overlay (ZIndex 2) draws ABOVE the live 3D presenter (ZIndex 1). In combat
    /// it is always on (facing ring + pips read over both the silhouette and the 3D render); outside
    /// combat it only appears while an embedded preview covers the silhouette's own tints.
    /// </summary>
    private void UpdateDamageOverlayVisibility()
    {
        if (_embeddedPreviewActive || _combatReadout)
            EnsureDamageOverlay();

        if (_damageOverlay != null && GodotObject.IsInstanceValid(_damageOverlay))
        {
            _damageOverlay.Visible = _embeddedPreviewActive || _combatReadout;
            _damageOverlay.SetModes(_combatReadout, _embeddedPreviewActive);
        }
    }

    private void EnsureDamageOverlay()
    {
        if (_damageOverlay != null && GodotObject.IsInstanceValid(_damageOverlay))
            return;

        var overlay = new VehicleDamageOverlay
        {
            Name = "DamageReadoutOverlay",
            MouseFilter = MouseFilterEnum.Ignore,
            // Presenter TextureRect is installed at ZIndex 1; the damage readout must sit above it.
            ZIndex = 2,
            Visible = false,
        };
        overlay.AnchorLeft = 0f;
        overlay.AnchorTop = 0f;
        overlay.AnchorRight = 1f;
        overlay.AnchorBottom = 1f;
        // Match the presenter's 8px inset so strips hug the rendered preview, not the frame border.
        overlay.OffsetLeft = 8f;
        overlay.OffsetTop = 8f;
        overlay.OffsetRight = -8f;
        overlay.OffsetBottom = -8f;
        AddChild(overlay);

        _damageOverlay = overlay;
        overlay.SetBodyColor(_bodyColor);
        overlay.SetDamage(_damage);
        overlay.SetModes(_combatReadout, _embeddedPreviewActive);
    }

    public override void _Draw()
    {
        var rect = GetRect();
        var outer = new Rect2(new Vector2(1f, 1f), rect.Size - new Vector2(2f, 2f));
        if (outer.Size.X <= 12f || outer.Size.Y <= 12f)
            return;

        DrawFrame(outer);

        var inner = outer.GrowIndividual(-8f, -8f, -8f, -8f);
        if (inner.Size.X <= 8f || inner.Size.Y <= 8f)
            return;

        if (_snapshotTexture != null)
        {
            DrawSnapshot(inner, _snapshotTexture);
            return;
        }

        if (_embeddedPreviewActive)
        {
            DrawEmbeddedPreviewBackground(inner);
            return;
        }

        var profile = GetProfile(_vehicleClass);
        var bodyRect = new Rect2(
            new Vector2(inner.Position.X + inner.Size.X * 0.5f - inner.Size.X * profile.WidthRatio * 0.5f,
                inner.Position.Y + inner.Size.Y * 0.5f - inner.Size.Y * profile.HeightRatio * 0.5f),
            new Vector2(inner.Size.X * profile.WidthRatio, inner.Size.Y * profile.HeightRatio));

        var silhouette = BuildBodyPolygon(bodyRect, profile, 0f, 0f);
        var shadow = BuildBodyPolygon(bodyRect, profile, 3f, 4f);
        var center = bodyRect.GetCenter();

        DrawSoftGroundShadow(center, bodyRect.Size);
        DrawColoredPolygon(shadow, new Color(0f, 0f, 0f, 0.28f));
        DrawColoredPolygon(silhouette, _bodyColor);
        DrawOutline(silhouette, _bodyColor.Lightened(0.24f), 2f, true);

        DrawSectionDamage(bodyRect, profile);
        DrawCabin(bodyRect, profile);
        DrawBodyDetails(bodyRect, profile);
        DrawWheelSet(bodyRect, profile);
        DrawWeaponMounts(bodyRect, profile);
        DrawTowIndicator(bodyRect);
    }

    private void DrawSnapshot(Rect2 inner, Texture2D texture)
    {
        var texSize = texture.GetSize();
        if (texSize.X <= 0f || texSize.Y <= 0f)
            return;

        var scale = MathF.Min(inner.Size.X / texSize.X, inner.Size.Y / texSize.Y);
        var drawSize = texSize * scale;
        var drawRect = new Rect2(inner.GetCenter() - drawSize * 0.5f, drawSize);
        DrawTextureRect(texture, drawRect, false, Colors.White);
    }

    private void DrawEmbeddedPreviewBackground(Rect2 inner)
    {
        var bg = new StyleBoxFlat
        {
            BgColor = new Color(0.01f, 0.04f, 0.08f, 0.92f),
            BorderColor = new Color(0.05f, 0.72f, 0.95f, 0.65f),
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            CornerRadiusTopLeft = 14,
            CornerRadiusTopRight = 14,
            CornerRadiusBottomLeft = 14,
            CornerRadiusBottomRight = 14,
        };
        DrawStyleBox(bg, inner);
    }

    private void DrawFrame(Rect2 outer)
    {
        var shell = new StyleBoxFlat
        {
            BgColor = new Color(0.02f, 0.05f, 0.09f, 0.96f),
            BorderColor = new Color(0.05f, 0.72f, 0.95f, 0.85f),
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            CornerRadiusTopLeft = 16,
            CornerRadiusTopRight = 16,
            CornerRadiusBottomLeft = 16,
            CornerRadiusBottomRight = 16,
            ShadowColor = new Color(0f, 0f, 0f, 0.30f),
            ShadowSize = 4,
        };
        DrawStyleBox(shell, outer);
    }

    private void DrawSoftGroundShadow(Vector2 center, Vector2 bodySize)
    {
        var shadowColor = new Color(0f, 0f, 0f, 0.12f);
        DrawCircle(new Vector2(center.X, center.Y + bodySize.Y * 0.08f), bodySize.X * 0.42f, shadowColor);
    }

    private void DrawCabin(Rect2 bodyRect, VehicleBodyProfile profile)
    {
        var cabinWidth = bodyRect.Size.X * profile.CabinWidth;
        var cabinHeight = bodyRect.Size.Y * profile.CabinHeight;
        var cabinRect = new Rect2(
            new Vector2(bodyRect.GetCenter().X - cabinWidth * 0.5f, bodyRect.Position.Y + bodyRect.Size.Y * 0.18f),
            new Vector2(cabinWidth, cabinHeight));

        var cabinStyle = new StyleBoxFlat
        {
            BgColor = _bodyColor.Darkened(0.18f),
            CornerRadiusTopLeft = profile.Boxy ? 10 : 14,
            CornerRadiusTopRight = profile.Boxy ? 10 : 14,
            CornerRadiusBottomLeft = profile.Boxy ? 8 : 12,
            CornerRadiusBottomRight = profile.Boxy ? 8 : 12,
            BorderColor = _bodyColor.Lightened(0.08f),
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
        };
        DrawStyleBox(cabinStyle, cabinRect);

        var glassInset = new Vector2(Mathf.Max(3f, cabinRect.Size.X * 0.10f), Mathf.Max(3f, cabinRect.Size.Y * 0.08f));
        var glassRect = cabinRect.GrowIndividual(-glassInset.X, -glassInset.Y, -glassInset.X, -glassInset.Y);
        DrawStyleBox(new StyleBoxFlat
        {
            BgColor = new Color(0.60f, 0.82f, 1.0f, 0.72f),
            CornerRadiusTopLeft = profile.Boxy ? 8 : 12,
            CornerRadiusTopRight = profile.Boxy ? 8 : 12,
            CornerRadiusBottomLeft = profile.Boxy ? 6 : 10,
            CornerRadiusBottomRight = profile.Boxy ? 6 : 10,
        }, glassRect);

        var pillarColor = new Color(0.90f, 0.95f, 1f, 0.40f);
        var midY = glassRect.Position.Y + glassRect.Size.Y * 0.46f;
        DrawLine(new Vector2(glassRect.Position.X, midY), new Vector2(glassRect.End.X, midY), pillarColor, 1f);
        DrawLine(new Vector2(glassRect.GetCenter().X, glassRect.Position.Y), new Vector2(glassRect.GetCenter().X, glassRect.End.Y), pillarColor, 1f);
    }

    private void DrawBodyDetails(Rect2 bodyRect, VehicleBodyProfile profile)
    {
        var highlight = _bodyColor.Lightened(0.18f);
        var accent = _bodyColor.Darkened(0.22f);

        var hoodY = bodyRect.Position.Y + bodyRect.Size.Y * 0.18f;
        var trunkY = bodyRect.End.Y - bodyRect.Size.Y * 0.18f;
        DrawLine(new Vector2(bodyRect.Position.X + bodyRect.Size.X * 0.25f, hoodY), new Vector2(bodyRect.End.X - bodyRect.Size.X * 0.25f, hoodY), highlight, 1.25f);
        DrawLine(new Vector2(bodyRect.Position.X + bodyRect.Size.X * 0.22f, trunkY), new Vector2(bodyRect.End.X - bodyRect.Size.X * 0.22f, trunkY), accent, 1.25f);

        var centerX = bodyRect.GetCenter().X;
        DrawLine(new Vector2(centerX, bodyRect.Position.Y + bodyRect.Size.Y * 0.10f), new Vector2(centerX, bodyRect.End.Y - bodyRect.Size.Y * 0.10f), new Color(1f, 1f, 1f, 0.12f), 1f);

        var noseRect = new Rect2(
            new Vector2(centerX - bodyRect.Size.X * 0.18f, bodyRect.Position.Y + bodyRect.Size.Y * 0.04f),
            new Vector2(bodyRect.Size.X * 0.36f, bodyRect.Size.Y * 0.08f));
        DrawStyleBox(new StyleBoxFlat
        {
            BgColor = new Color(1f, 1f, 1f, 0.12f),
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
        }, noseRect);

        var rearBumper = new Rect2(
            new Vector2(centerX - bodyRect.Size.X * 0.20f, bodyRect.End.Y - bodyRect.Size.Y * 0.10f),
            new Vector2(bodyRect.Size.X * 0.40f, bodyRect.Size.Y * 0.05f));
        DrawRect(rearBumper, new Color(0.10f, 0.12f, 0.15f, 0.45f), true);
    }

    private void DrawWheelSet(Rect2 bodyRect, VehicleBodyProfile profile)
    {
        DrawWheel(bodyRect, profile, true, true, _damage.TireFl);
        DrawWheel(bodyRect, profile, false, true, _damage.TireFr);
        DrawWheel(bodyRect, profile, true, false, _damage.TireRl);
        DrawWheel(bodyRect, profile, false, false, _damage.TireRr);
    }

    private void DrawWheel(Rect2 bodyRect, VehicleBodyProfile profile, bool left, bool front, SectionDamage01 tire)
    {
        var wheelW = Mathf.Max(7f, bodyRect.Size.X * (profile.Boxy ? 0.18f : 0.16f));
        var wheelH = Mathf.Max(13f, bodyRect.Size.Y * 0.18f);
        var xInset = bodyRect.Size.X * profile.WheelInset;
        var x = left ? bodyRect.Position.X - wheelW * 0.48f + xInset : bodyRect.End.X - wheelW * 0.52f - xInset;
        var yRatio = front ? profile.WheelFront : profile.WheelRear;
        var y = bodyRect.Position.Y + bodyRect.Size.Y * yRatio - wheelH * 0.5f;

        var tireColor = tire.IsDestroyed
            ? DestroyedTireColor
            : DamageEscalationColor(TireRubberColor, tire);
        DrawStyleBox(new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.06f, 0.08f, 0.96f),
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
        }, new Rect2(x, y, wheelW, wheelH));

        DrawStyleBox(new StyleBoxFlat
        {
            BgColor = tireColor,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
        }, new Rect2(x + 1.5f, y + 2f, wheelW - 3f, wheelH - 4f));

        if (tire.IsDestroyed)
        {
            // Blown tire must read unmistakably: solid dark red pad + X.
            DrawLine(new Vector2(x + 2f, y + 2.5f), new Vector2(x + wheelW - 2f, y + wheelH - 2.5f), DestroyedTireMarkColor, 2f);
            DrawLine(new Vector2(x + wheelW - 2f, y + 2.5f), new Vector2(x + 2f, y + wheelH - 2.5f), DestroyedTireMarkColor, 2f);
            return;
        }

        DrawLine(new Vector2(x + 2f, y + wheelH * 0.5f), new Vector2(x + wheelW - 2f, y + wheelH * 0.5f), new Color(1f, 1f, 1f, 0.16f), 1f);
    }

    private void DrawWeaponMounts(Rect2 bodyRect, VehicleBodyProfile profile)
    {
        DrawFrontRearMounts(bodyRect, true, _frontWeapons);
        DrawFrontRearMounts(bodyRect, false, _rearWeapons);
        DrawSideMounts(bodyRect, true, _leftWeapons);
        DrawSideMounts(bodyRect, false, _rightWeapons);
        DrawTopMounts(bodyRect, _topWeapons);
    }

    private void DrawFrontRearMounts(Rect2 bodyRect, bool front, int count)
    {
        if (count <= 0)
            return;

        var shown = Mathf.Min(count, 3);
        var y = front ? bodyRect.Position.Y - 1f : bodyRect.End.Y - 3f;
        var barrelLen = 7f;
        var barrelW = 3f;
        var spacing = Mathf.Max(7f, bodyRect.Size.X * 0.18f);
        var center = bodyRect.GetCenter().X;
        var start = center - ((shown - 1) * spacing * 0.5f);
        var tint = new Color(0.18f, 0.18f, 0.20f, 0.95f);

        for (var i = 0; i < shown; i++)
        {
            var x = start + i * spacing - barrelW * 0.5f;
            var rect = front
                ? new Rect2(x, y - barrelLen, barrelW, barrelLen)
                : new Rect2(x, y, barrelW, barrelLen);
            DrawRect(rect, tint, true);
        }

        if (count > shown)
            DrawBadge(new Vector2(bodyRect.GetCenter().X + bodyRect.Size.X * 0.27f, front ? bodyRect.Position.Y + 4f : bodyRect.End.Y - 4f), count);
    }

    private void DrawSideMounts(Rect2 bodyRect, bool left, int count)
    {
        if (count <= 0)
            return;

        var shown = Mathf.Min(count, 2);
        var x = left ? bodyRect.Position.X - 6f : bodyRect.End.X + 2f;
        var podW = 6f;
        var podH = 4f;
        var spacing = Mathf.Max(12f, bodyRect.Size.Y * 0.24f);
        var startY = bodyRect.GetCenter().Y - ((shown - 1) * spacing * 0.5f);
        var tint = new Color(0.14f, 0.14f, 0.17f, 0.95f);

        for (var i = 0; i < shown; i++)
        {
            var y = startY + i * spacing - podH * 0.5f;
            DrawRect(new Rect2(x, y, podW, podH), tint, true);
        }

        if (count > shown)
            DrawBadge(new Vector2(left ? bodyRect.Position.X + 6f : bodyRect.End.X - 6f, bodyRect.GetCenter().Y), count);
    }

    private void DrawTopMounts(Rect2 bodyRect, int count)
    {
        if (count <= 0)
            return;

        var shown = Mathf.Min(count, 2);
        var spacing = Mathf.Max(12f, bodyRect.Size.X * 0.18f);
        var start = bodyRect.GetCenter().X - ((shown - 1) * spacing * 0.5f);
        var y = bodyRect.Position.Y + bodyRect.Size.Y * 0.46f;

        for (var i = 0; i < shown; i++)
        {
            var cx = start + i * spacing;
            DrawCircle(new Vector2(cx, y), 4.5f, new Color(0.98f, 0.82f, 0.22f, 0.90f));
            DrawCircle(new Vector2(cx, y), 2.2f, new Color(0.22f, 0.16f, 0.02f, 0.85f));
        }

        if (count > shown)
            DrawBadge(new Vector2(bodyRect.GetCenter().X + bodyRect.Size.X * 0.16f, y - 10f), count);
    }

    private void DrawTowIndicator(Rect2 bodyRect)
    {
        if (!_hasTowAttached)
            return;

        var rear = new Vector2(bodyRect.GetCenter().X, bodyRect.End.Y + 4f);
        DrawLine(rear, rear + new Vector2(0f, 10f), new Color(0.90f, 0.80f, 0.25f, 0.85f), 2f);
        DrawCircle(rear + new Vector2(0f, 12f), 3f, new Color(0.90f, 0.80f, 0.25f, 0.90f));
    }

    private void DrawSectionDamage(Rect2 bodyRect, VehicleBodyProfile profile)
    {
        DrawSectionTint(BuildFrontOverlay(bodyRect, profile), _damage.Front);
        DrawSectionTint(BuildRearOverlay(bodyRect, profile), _damage.Rear);
        DrawSectionTint(BuildLeftOverlay(bodyRect, profile), _damage.Left);
        DrawSectionTint(BuildRightOverlay(bodyRect, profile), _damage.Right);
        DrawSectionTint(BuildTopOverlay(bodyRect, profile), _damage.Top);

        if (_damage.Under.IsDamaged)
        {
            var underRect = new Rect2(
                new Vector2(bodyRect.Position.X + bodyRect.Size.X * 0.24f, bodyRect.End.Y - bodyRect.Size.Y * 0.16f),
                new Vector2(bodyRect.Size.X * 0.52f, bodyRect.Size.Y * 0.05f));
            var tint = DamageEscalationColor(_bodyColor, _damage.Under);
            tint.A = DamageEscalationAlpha(_damage.Under);
            DrawRect(underRect, tint, true);
        }
    }

    private void DrawSectionTint(Vector2[] polygon, SectionDamage01 damage)
    {
        if (!damage.IsDamaged)
            return;

        // Escalation: body color -> orange while armor strips, then deep red -> near-black as the
        // structure underneath goes critical/destroyed.
        var tint = DamageEscalationColor(_bodyColor, damage);
        tint.A = DamageEscalationAlpha(damage);
        DrawColoredPolygon(polygon, tint);

        if (damage.Hp01 < 0.55f)
        {
            for (var i = 0; i < polygon.Length; i += 2)
            {
                var a = polygon[i];
                var b = polygon[(i + 2) % polygon.Length];
                DrawLine(a, b, new Color(0.08f, 0.02f, 0.02f, 0.35f), 1.25f);
            }
        }
    }

    private void DrawBadge(Vector2 center, int value)
    {
        if (value <= 1)
            return;

        DrawCircle(center, 6f, new Color(0.98f, 0.82f, 0.22f, 0.92f));
        var font = GetThemeDefaultFont();
        var fontSize = 10;
        var text = value.ToString();
        var textSize = font.GetStringSize(text, HorizontalAlignment.Left, -1, fontSize);
        DrawString(font, center - new Vector2(textSize.X * 0.5f, -textSize.Y * 0.28f), text, HorizontalAlignment.Left, -1, fontSize, new Color(0.10f, 0.08f, 0.02f, 0.95f));
    }

    private void DrawOutline(Vector2[] points, Color color, float width, bool closed)
    {
        if (points.Length < 2)
            return;

        if (closed)
        {
            for (var i = 0; i < points.Length; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Length];
                DrawLine(a, b, color, width);
            }
            return;
        }

        for (var i = 0; i < points.Length - 1; i++)
            DrawLine(points[i], points[i + 1], color, width);
    }

    private bool CountWeapons(VehicleInstanceState inst)
    {
        var front = 0;
        var rear = 0;
        var left = 0;
        var right = 0;
        var top = 0;

        foreach (var mountId in inst.InstalledWeaponsByMountId.Keys)
        {
            if (string.IsNullOrWhiteSpace(mountId))
                continue;

            var key = mountId.Trim().ToUpperInvariant();
            if (key.StartsWith("F")) front++;
            else if (key.StartsWith("B") || key.StartsWith("REAR")) rear++;
            else if (key.StartsWith("L")) left++;
            else if (key.StartsWith("R")) right++;
            else if (key.StartsWith("T") || key.StartsWith("U")) top++;
        }

        var changed = front != _frontWeapons || rear != _rearWeapons || left != _leftWeapons
            || right != _rightWeapons || top != _topWeapons;
        _frontWeapons = front;
        _rearWeapons = rear;
        _leftWeapons = left;
        _rightWeapons = right;
        _topWeapons = top;
        return changed;
    }

    private bool CaptureDamageState(VehicleDefinition def, VehicleInstanceState inst)
    {
        var next = new VehicleDamageSnapshot(
            ReadSectionDamage(def, inst, ArmorSection.Front),
            ReadSectionDamage(def, inst, ArmorSection.Rear),
            ReadSectionDamage(def, inst, ArmorSection.Left),
            ReadSectionDamage(def, inst, ArmorSection.Right),
            ReadSectionDamage(def, inst, ArmorSection.Top),
            ReadSectionDamage(def, inst, ArmorSection.Undercarriage),
            ReadTireDamage(def, inst, 0),
            ReadTireDamage(def, inst, 1),
            ReadTireDamage(def, inst, 2),
            ReadTireDamage(def, inst, 3));
        var towAttached = inst.Towing.TotalTowedMassKgCached > 0.5f || inst.Towing.AttachedTowTargetInstanceIds.Count > 0;

        var changed = !next.Equals(_damage) || towAttached != _hasTowAttached;
        _damage = next;
        _hasTowAttached = towAttached;
        return changed;
    }

    private static SectionDamage01 ReadSectionDamage(VehicleDefinition def, VehicleInstanceState inst, ArmorSection section)
    {
        def.BaseHpBySection.TryGetValue(section, out var maxHp);
        def.BaseArmorBySection.TryGetValue(section, out var baseAp);
        inst.CurrentHpBySection.TryGetValue(section, out var hp);
        inst.CurrentArmorBySection.TryGetValue(section, out var ap);
        var maxAp = Math.Max(0, baseAp + Game.Systems.VehicleMassMath.GetPlatingArmorBonus(inst.ArmorPlatingLevel));
        var curHp = Math.Clamp(hp, 0, Math.Max(0, maxHp));
        var curAp = Math.Clamp(ap, 0, maxAp);
        return new SectionDamage01(
            maxHp > 0 ? (float)curHp / maxHp : 1f,
            maxAp > 0 ? (float)curAp / maxAp : 1f,
            Mathf.Clamp((float)(curHp + curAp) / Math.Max(1, maxHp + maxAp), 0f, 1f));
    }

    private static SectionDamage01 ReadTireDamage(VehicleDefinition def, VehicleInstanceState inst, int idx)
    {
        var maxHp = Math.Max(1, def.BaseTireHp);
        var maxAp = Math.Max(1, def.BaseTireArmor + Game.Systems.VehicleMassMath.GetPlatingArmorBonus(inst.TirePlatingLevel));
        var hp = inst.CurrentTireHp is { Length: > 0 } && idx < inst.CurrentTireHp.Length ? inst.CurrentTireHp[idx] : 0;
        var ap = inst.CurrentTireArmor is { Length: > 0 } && idx < inst.CurrentTireArmor.Length ? inst.CurrentTireArmor[idx] : 0;
        var curHp = Math.Clamp(hp, 0, maxHp);
        var curAp = Math.Clamp(ap, 0, maxAp);
        return new SectionDamage01(
            (float)curHp / maxHp,
            (float)curAp / maxAp,
            Mathf.Clamp((float)(curHp + curAp) / (maxHp + maxAp), 0f, 1f));
    }

    private static readonly Color ArmorStrippedOrange = new(0.96f, 0.55f, 0.10f);
    private static readonly Color StructureDeepRed = new(0.52f, 0.05f, 0.04f);
    private static readonly Color StructureDestroyed = new(0.14f, 0.02f, 0.02f);
    internal static readonly Color DestroyedTireColor = new(0.45f, 0.04f, 0.04f);
    internal static readonly Color DestroyedTireMarkColor = new(1f, 0.84f, 0.78f, 0.95f);
    internal static readonly Color TireRubberColor = new(0.13f, 0.13f, 0.15f);

    /// <summary>
    /// Locational-damage escalation shared by the silhouette fallback and the live-preview overlay:
    /// base color -> orange as armor strips, then deep red -> near-black as structure fails.
    /// </summary>
    internal static Color DamageEscalationColor(Color baseColor, SectionDamage01 damage)
    {
        if (damage.IsDestroyed)
            return StructureDestroyed;

        var armorLoss = Mathf.Clamp(1f - damage.Armor01, 0f, 1f);
        var hpLoss = Mathf.Clamp(1f - damage.Hp01, 0f, 1f);

        var tint = baseColor.Lerp(ArmorStrippedOrange, armorLoss);
        // Gentle pull toward deep red while structure holds; hard ramp once HP goes critical (<35%).
        var structural = hpLoss <= 0.65f
            ? hpLoss * 0.7f
            : Mathf.Lerp(0.455f, 1f, (hpLoss - 0.65f) / 0.35f);
        return tint.Lerp(StructureDeepRed, Mathf.Clamp(structural, 0f, 1f));
    }

    /// <summary>Tint strength: invisible while pristine, unmistakable once structure is failing.
    /// Ramp strengthened for the combat HUD diet — the tint is now the primary section read.</summary>
    internal static float DamageEscalationAlpha(SectionDamage01 damage)
    {
        if (damage.IsDestroyed)
            return 0.95f;

        var armorLoss = Mathf.Clamp(1f - damage.Armor01, 0f, 1f);
        var hpLoss = Mathf.Clamp(1f - damage.Hp01, 0f, 1f);
        return Mathf.Clamp(MathF.Max(armorLoss * 0.65f, hpLoss * 0.95f), 0f, 0.95f);
    }

    private static readonly Color RingHealthy = new(0.30f, 0.85f, 0.40f);
    private static readonly Color RingWorn = new(0.95f, 0.75f, 0.20f);
    private static readonly Color RingCritical = new(0.95f, 0.14f, 0.10f);

    /// <summary>Facing-ring ramp: combined armor+structure fraction, green -> amber -> red.</summary>
    internal static Color FacingRingColor(SectionDamage01 damage)
    {
        var c = Mathf.Clamp(damage.Combined01, 0f, 1f);
        return c >= 0.5f
            ? RingWorn.Lerp(RingHealthy, (c - 0.5f) * 2f)
            : RingCritical.Lerp(RingWorn, c * 2f);
    }

    private static bool ColorsClose(Color a, Color b)
    {
        return Mathf.Abs(a.R - b.R) < 0.001f
            && Mathf.Abs(a.G - b.G) < 0.001f
            && Mathf.Abs(a.B - b.B) < 0.001f
            && Mathf.Abs(a.A - b.A) < 0.001f;
    }

    private static VehicleBodyProfile GetProfile(VehicleClass vehicleClass)
    {
        return vehicleClass switch
        {
            VehicleClass.Sports => new VehicleBodyProfile(0.50f, 0.84f, 0.18f, 0.82f, 0.56f, 0.34f, 0.01f, 0.28f, 0.75f, 0.22f, false),
            VehicleClass.Sedan => new VehicleBodyProfile(0.54f, 0.88f, 0.22f, 0.84f, 0.60f, 0.38f, 0.01f, 0.30f, 0.77f, 0.18f, false),
            VehicleClass.LightTruck => new VehicleBodyProfile(0.58f, 0.92f, 0.10f, 0.92f, 0.66f, 0.38f, 0.02f, 0.26f, 0.78f, 0.08f, true),
            VehicleClass.Suv => new VehicleBodyProfile(0.57f, 0.90f, 0.12f, 0.90f, 0.64f, 0.40f, 0.02f, 0.27f, 0.80f, 0.10f, true),
            VehicleClass.HeavyTruck => new VehicleBodyProfile(0.62f, 0.96f, 0.06f, 0.96f, 0.70f, 0.36f, 0.02f, 0.24f, 0.80f, 0.04f, true),
            VehicleClass.SemiTruck => new VehicleBodyProfile(0.62f, 0.96f, 0.09f, 0.96f, 0.70f, 0.36f, 0.02f, 0.24f, 0.80f, 0.04f, true),
            _ => new VehicleBodyProfile(0.52f, 0.86f, 0.16f, 0.84f, 0.58f, 0.36f, 0.01f, 0.29f, 0.76f, 0.16f, false),
        };
    }

    private static Vector2[] BuildBodyPolygon(Rect2 bodyRect, VehicleBodyProfile profile, float xOffset, float yOffset)
    {
        var center = bodyRect.GetCenter() + new Vector2(xOffset, yOffset);
        var w = bodyRect.Size.X;
        var h = bodyRect.Size.Y;
        var halfW = w * 0.5f;
        var top = bodyRect.Position.Y + yOffset;
        var bottom = bodyRect.End.Y + yOffset;
        var noseY = top + h * 0.10f;
        var shoulderY = top + h * 0.24f;
        var waistY = top + h * 0.60f;
        var rearY = top + h * 0.86f;
        var noseHalf = halfW * profile.NosePinch;
        var shoulderHalf = halfW * profile.ShoulderWidth;
        var waistHalf = halfW * (profile.Boxy ? 0.96f : 0.90f);
        var rearHalf = halfW * (1f - profile.RearPinch);

        return new[]
        {
            new Vector2(center.X, top),
            new Vector2(center.X + noseHalf, noseY),
            new Vector2(center.X + shoulderHalf, shoulderY),
            new Vector2(center.X + waistHalf, waistY),
            new Vector2(center.X + rearHalf, rearY),
            new Vector2(center.X + halfW * 0.26f, bottom),
            new Vector2(center.X - halfW * 0.26f, bottom),
            new Vector2(center.X - rearHalf, rearY),
            new Vector2(center.X - waistHalf, waistY),
            new Vector2(center.X - shoulderHalf, shoulderY),
            new Vector2(center.X - noseHalf, noseY),
        };
    }

    private static Vector2[] BuildFrontOverlay(Rect2 bodyRect, VehicleBodyProfile profile)
    {
        var center = bodyRect.GetCenter().X;
        var y0 = bodyRect.Position.Y + bodyRect.Size.Y * 0.02f;
        var y1 = bodyRect.Position.Y + bodyRect.Size.Y * 0.20f;
        var y2 = bodyRect.Position.Y + bodyRect.Size.Y * 0.30f;
        return new[]
        {
            new Vector2(center, y0),
            new Vector2(center + bodyRect.Size.X * 0.18f, y1),
            new Vector2(center + bodyRect.Size.X * 0.24f, y2),
            new Vector2(center - bodyRect.Size.X * 0.24f, y2),
            new Vector2(center - bodyRect.Size.X * 0.18f, y1),
        };
    }

    private static Vector2[] BuildRearOverlay(Rect2 bodyRect, VehicleBodyProfile profile)
    {
        var center = bodyRect.GetCenter().X;
        var y0 = bodyRect.End.Y - bodyRect.Size.Y * 0.02f;
        var y1 = bodyRect.End.Y - bodyRect.Size.Y * 0.18f;
        var y2 = bodyRect.End.Y - bodyRect.Size.Y * 0.28f;
        return new[]
        {
            new Vector2(center + bodyRect.Size.X * 0.22f, y0),
            new Vector2(center + bodyRect.Size.X * 0.34f, y1),
            new Vector2(center + bodyRect.Size.X * 0.28f, y2),
            new Vector2(center - bodyRect.Size.X * 0.28f, y2),
            new Vector2(center - bodyRect.Size.X * 0.34f, y1),
            new Vector2(center - bodyRect.Size.X * 0.22f, y0),
        };
    }

    private static Vector2[] BuildLeftOverlay(Rect2 bodyRect, VehicleBodyProfile profile)
    {
        return new[]
        {
            new Vector2(bodyRect.Position.X - bodyRect.Size.X * 0.02f, bodyRect.Position.Y + bodyRect.Size.Y * 0.22f),
            new Vector2(bodyRect.Position.X + bodyRect.Size.X * 0.15f, bodyRect.Position.Y + bodyRect.Size.Y * 0.18f),
            new Vector2(bodyRect.Position.X + bodyRect.Size.X * 0.12f, bodyRect.End.Y - bodyRect.Size.Y * 0.18f),
            new Vector2(bodyRect.Position.X - bodyRect.Size.X * 0.01f, bodyRect.End.Y - bodyRect.Size.Y * 0.22f),
        };
    }

    private static Vector2[] BuildRightOverlay(Rect2 bodyRect, VehicleBodyProfile profile)
    {
        return new[]
        {
            new Vector2(bodyRect.End.X + bodyRect.Size.X * 0.02f, bodyRect.Position.Y + bodyRect.Size.Y * 0.22f),
            new Vector2(bodyRect.End.X - bodyRect.Size.X * 0.15f, bodyRect.Position.Y + bodyRect.Size.Y * 0.18f),
            new Vector2(bodyRect.End.X - bodyRect.Size.X * 0.12f, bodyRect.End.Y - bodyRect.Size.Y * 0.18f),
            new Vector2(bodyRect.End.X + bodyRect.Size.X * 0.01f, bodyRect.End.Y - bodyRect.Size.Y * 0.22f),
        };
    }

    private static Vector2[] BuildTopOverlay(Rect2 bodyRect, VehicleBodyProfile profile)
    {
        var cabinWidth = bodyRect.Size.X * profile.CabinWidth;
        var cabinHeight = bodyRect.Size.Y * profile.CabinHeight;
        var cabinRect = new Rect2(
            new Vector2(bodyRect.GetCenter().X - cabinWidth * 0.5f, bodyRect.Position.Y + bodyRect.Size.Y * 0.18f),
            new Vector2(cabinWidth, cabinHeight));

        return new[]
        {
            cabinRect.Position,
            new Vector2(cabinRect.End.X, cabinRect.Position.Y),
            cabinRect.End,
            new Vector2(cabinRect.Position.X, cabinRect.End.Y),
        };
    }

    // ---------------------------------------------------------------------------------------------
    // Damage data + embedded-preview overlay (nested so this file keeps one editor-attachable class)
    // ---------------------------------------------------------------------------------------------

    /// <summary>Per-section damage fractions (structure HP, armor, weighted combined), each 0..1.</summary>
    internal readonly struct SectionDamage01 : IEquatable<SectionDamage01>
    {
        public static readonly SectionDamage01 Pristine = new(1f, 1f, 1f);

        public SectionDamage01(float hp01, float armor01, float combined01)
        {
            Hp01 = hp01;
            Armor01 = armor01;
            Combined01 = combined01;
        }

        public float Hp01 { get; }
        public float Armor01 { get; }
        public float Combined01 { get; }

        public bool IsDamaged => Hp01 < 0.995f || Armor01 < 0.995f;
        public bool IsDestroyed => Hp01 <= 0.001f;

        public bool Equals(SectionDamage01 other)
            => Hp01.Equals(other.Hp01) && Armor01.Equals(other.Armor01) && Combined01.Equals(other.Combined01);

        public override bool Equals(object? obj) => obj is SectionDamage01 other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Hp01, Armor01, Combined01);
    }

    /// <summary>Immutable whole-vehicle damage snapshot; value equality drives redraw-on-change.</summary>
    internal readonly struct VehicleDamageSnapshot : IEquatable<VehicleDamageSnapshot>
    {
        public static readonly VehicleDamageSnapshot Pristine = new(
            SectionDamage01.Pristine, SectionDamage01.Pristine, SectionDamage01.Pristine, SectionDamage01.Pristine,
            SectionDamage01.Pristine, SectionDamage01.Pristine, SectionDamage01.Pristine, SectionDamage01.Pristine,
            SectionDamage01.Pristine, SectionDamage01.Pristine);

        public VehicleDamageSnapshot(
            SectionDamage01 front, SectionDamage01 rear, SectionDamage01 left, SectionDamage01 right,
            SectionDamage01 top, SectionDamage01 under,
            SectionDamage01 tireFl, SectionDamage01 tireFr, SectionDamage01 tireRl, SectionDamage01 tireRr)
        {
            Front = front;
            Rear = rear;
            Left = left;
            Right = right;
            Top = top;
            Under = under;
            TireFl = tireFl;
            TireFr = tireFr;
            TireRl = tireRl;
            TireRr = tireRr;
        }

        public SectionDamage01 Front { get; }
        public SectionDamage01 Rear { get; }
        public SectionDamage01 Left { get; }
        public SectionDamage01 Right { get; }
        public SectionDamage01 Top { get; }
        public SectionDamage01 Under { get; }
        public SectionDamage01 TireFl { get; }
        public SectionDamage01 TireFr { get; }
        public SectionDamage01 TireRl { get; }
        public SectionDamage01 TireRr { get; }

        public bool Equals(VehicleDamageSnapshot other)
            => Front.Equals(other.Front) && Rear.Equals(other.Rear) && Left.Equals(other.Left) && Right.Equals(other.Right)
                && Top.Equals(other.Top) && Under.Equals(other.Under)
                && TireFl.Equals(other.TireFl) && TireFr.Equals(other.TireFr)
                && TireRl.Equals(other.TireRl) && TireRr.Equals(other.TireRr);

        public override bool Equals(object? obj) => obj is VehicleDamageSnapshot other && Equals(other);

        public override int GetHashCode()
            => HashCode.Combine(
                HashCode.Combine(Front, Rear, Left, Right, Top, Under),
                HashCode.Combine(TireFl, TireFr, TireRl, TireRr));
    }

    /// <summary>
    /// Damage readout drawn ABOVE the embedded live 3D preview presenter (TextureRect at ZIndex 1).
    /// Combat mode: a thin per-facing ring (front/rear/left/right arcs colored by combined
    /// armor+structure) plus T(op)/U(nder) pips in the lower ring gaps — this replaces the old HUD
    /// bar grids. Embedded mode adds 4 tire pads (condition-colored, X when blown) since the 3D
    /// render covers the silhouette's own wheels. Code-only child — never attached in the editor.
    /// </summary>
    internal partial class VehicleDamageOverlay : Control
    {
        private VehicleDamageSnapshot _damage = VehicleDamageSnapshot.Pristine;
        private Color _bodyColor = new(0.93f, 0.76f, 0.12f);
        private bool _combatMode;
        private bool _embeddedMode;

        public VehicleDamageOverlay()
        {
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public void SetBodyColor(Color color)
        {
            if (ColorsClose(_bodyColor, color))
                return;

            _bodyColor = color;
        }

        public void SetDamage(VehicleDamageSnapshot damage)
        {
            if (damage.Equals(_damage))
                return;

            _damage = damage;
            QueueRedraw();
        }

        public void SetModes(bool combatMode, bool embeddedMode)
        {
            if (_combatMode == combatMode && _embeddedMode == embeddedMode)
                return;

            _combatMode = combatMode;
            _embeddedMode = embeddedMode;
            QueueRedraw();
        }

        public override void _Draw()
        {
            var size = Size;
            if (size.X < 24f || size.Y < 24f)
                return;

            if (_combatMode)
            {
                DrawFacingRing(size);
                DrawTopUnderPips(size);
            }

            if (!_embeddedMode)
                return; // silhouette mode draws its own tire pads

            // Tire pads sit at the wheelbase heights, inside the ring.
            var radius = Mathf.Clamp(Mathf.Min(size.X, size.Y) * 0.055f, 4f, 8f);
            var xLeft = radius + 9f;
            var xRight = size.X - radius - 9f;
            var yFront = size.Y * 0.30f;
            var yRear = size.Y * 0.72f;
            DrawTireDot(new Vector2(xLeft, yFront), radius, _damage.TireFl);
            DrawTireDot(new Vector2(xRight, yFront), radius, _damage.TireFr);
            DrawTireDot(new Vector2(xLeft, yRear), radius, _damage.TireRl);
            DrawTireDot(new Vector2(xRight, yRear), radius, _damage.TireRr);
        }

        // The preview renders the car nose-up, so the front arc maps to the top of the ring.
        private void DrawFacingRing(Vector2 size)
        {
            var center = size * 0.5f;
            var radius = Mathf.Min(size.X, size.Y) * 0.5f - 4f;
            if (radius < 14f)
                return;

            DrawArc(center, radius, 0f, Mathf.Tau, 48, new Color(1f, 1f, 1f, 0.07f), 2f, true);

            const float frontHalfSpan = 0.62f;
            const float sideHalfSpan = 0.50f;
            DrawFacingArc(center, radius, -Mathf.Pi * 0.5f, frontHalfSpan, _damage.Front);
            DrawFacingArc(center, radius, Mathf.Pi * 0.5f, frontHalfSpan, _damage.Rear);
            DrawFacingArc(center, radius, Mathf.Pi, sideHalfSpan, _damage.Left);
            DrawFacingArc(center, radius, 0f, sideHalfSpan, _damage.Right);
        }

        private void DrawFacingArc(Vector2 center, float radius, float centerAngle, float halfSpan, SectionDamage01 damage)
        {
            var width = damage.IsDestroyed ? 6.5f : 4.5f;
            DrawArc(center, radius, centerAngle - halfSpan, centerAngle + halfSpan, 16, FacingRingColor(damage), width, true);
        }

        private void DrawTopUnderPips(Vector2 size)
        {
            var center = size * 0.5f;
            var radius = Mathf.Min(size.X, size.Y) * 0.5f - 4f;
            if (radius < 14f)
                return;

            // Park the pips on the ring's lower diagonal gaps (between rear and side arcs).
            const float diag = 0.7071f;
            DrawStatusPip(center + new Vector2(-radius, radius) * diag, "T", _damage.Top);
            DrawStatusPip(center + new Vector2(radius, radius) * diag, "U", _damage.Under);
        }

        private void DrawStatusPip(Vector2 pos, string letter, SectionDamage01 damage)
        {
            const float r = 6.5f;
            DrawCircle(pos, r + 1.5f, new Color(0f, 0f, 0f, 0.55f));
            var col = FacingRingColor(damage);
            if (!damage.IsDamaged)
                col.A = 0.45f; // pristine stays quiet
            DrawCircle(pos, r, col);

            var font = GetThemeDefaultFont();
            const int fontSize = 9;
            var textSize = font.GetStringSize(letter, HorizontalAlignment.Left, -1, fontSize);
            DrawString(font, pos + new Vector2(-textSize.X * 0.5f, textSize.Y * 0.30f), letter,
                HorizontalAlignment.Left, -1, fontSize, new Color(0.05f, 0.05f, 0.05f, 0.95f));
        }

        private void DrawTireDot(Vector2 center, float radius, SectionDamage01 tire)
        {
            if (tire.IsDestroyed)
            {
                // Blown tire: solid dark red dot + unmistakable X.
                DrawCircle(center, radius + 1.5f, new Color(0f, 0f, 0f, 0.55f));
                DrawCircle(center, radius, DestroyedTireColor);
                var arm = radius * 0.72f;
                DrawLine(center + new Vector2(-arm, -arm), center + new Vector2(arm, arm), DestroyedTireMarkColor, 2f);
                DrawLine(center + new Vector2(arm, -arm), center + new Vector2(-arm, arm), DestroyedTireMarkColor, 2f);
                return;
            }

            // Always drawn in embedded mode: quiet dark rubber while healthy, escalation ramp as it wears.
            var tint = tire.IsDamaged ? DamageEscalationColor(TireRubberColor, tire) : TireRubberColor;
            tint.A = tire.IsDamaged ? MathF.Min(0.95f, DamageEscalationAlpha(tire) + 0.15f) : 0.55f;
            DrawCircle(center, radius + 1.5f, new Color(0f, 0f, 0f, 0.35f));
            DrawCircle(center, radius, tint);
        }
    }
}

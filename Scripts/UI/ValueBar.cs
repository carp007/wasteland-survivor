using System;
using Godot;

namespace WastelandSurvivor.Game.UI;

/// <summary>
/// Lightweight bar for HUD: supports horizontal or vertical fill with centered value text.
/// </summary>
public partial class ValueBar : Control
{
    private bool _vertical = false;

    [Export]
    public bool Vertical
    {
        get => _vertical;
        set
        {
            _vertical = value;
            if (IsNodeReady())
                ApplyLabelRotation();
        }
    }

    private ColorRect _bg = null!;
    private ColorRect _fill = null!;
    private Label _lbl = null!;

    public override void _Ready()
    {
        _bg = GetNode<ColorRect>("Bg");
        _fill = GetNode<ColorRect>("Fill");
        _lbl = GetNode<Label>("Lbl");

        // Slightly smaller text inside compact HUD bars.
        _lbl.AddThemeFontSizeOverride("font_size", 10);

        ApplyLabelRotation();
    }

    public override void _Notification(int what)
    {
        // Keep rotation/pivot correct after layout.
        if (what == NotificationResized)
            ApplyLabelRotation();
    }

    private void ApplyLabelRotation()
    {
        if (_lbl == null)
            return;

        if (Vertical)
            _lbl.Rotation = -Mathf.Pi / 2f;
        else
            _lbl.Rotation = 0f;

        // Rotate around center.
        _lbl.PivotOffset = _lbl.Size / 2f;
    }

    public void SetValues(int cur, int max, Color fillColor, Color? textColor = null)
    {
        max = Math.Max(1, max);
        cur = Math.Clamp(cur, 0, max);
        _lbl.Text = $"{cur}/{max}";
        _fill.Color = fillColor;

        if (textColor.HasValue)
            _lbl.AddThemeColorOverride("font_color", textColor.Value);

        var pct = (double)cur / max;
        pct = double.IsFinite(pct) ? Math.Clamp(pct, 0.0, 1.0) : 0.0;
        UpdateFill((float)pct);
    }

    public void SetValues(float cur, float max, Color fillColor, Color? textColor = null, string format = "0.0")
    {
        max = Math.Max(0.01f, max);
        cur = Mathf.Clamp(cur, 0f, max);
        _lbl.Text = $"{cur.ToString(format)}/{max.ToString(format)}";
        _fill.Color = fillColor;

        if (textColor.HasValue)
            _lbl.AddThemeColorOverride("font_color", textColor.Value);

        var pct = (double)cur / max;
        pct = double.IsFinite(pct) ? Math.Clamp(pct, 0.0, 1.0) : 0.0;
        UpdateFill((float)pct);
    }

    private void UpdateFill(float pct)
    {
        pct = Mathf.Clamp(pct, 0f, 1f);

        // Reset offsets.
        _fill.OffsetLeft = 0;
        _fill.OffsetTop = 0;
        _fill.OffsetRight = 0;
        _fill.OffsetBottom = 0;

        if (!Vertical)
        {
            _fill.AnchorLeft = 0f;
            _fill.AnchorTop = 0f;
            _fill.AnchorBottom = 1f;
            _fill.AnchorRight = pct;
        }
        else
        {
            _fill.AnchorLeft = 0f;
            _fill.AnchorRight = 1f;
            _fill.AnchorBottom = 1f;
            _fill.AnchorTop = 1f - pct;
        }
    }
}

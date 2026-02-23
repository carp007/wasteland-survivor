using System;
using Godot;

namespace WastelandSurvivor.Game.UI;

/// <summary>
/// Simple top-right HUD for player status: HP + Armor Points (AP).
/// Implemented as a small, reusable UI control.
/// </summary>
public partial class PlayerStatusHud : PanelContainer
{
	private ProgressBar _pbHp = null!;
	private Label _lblHpInBar = null!;
	private ProgressBar _pbAp = null!;
	private Label _lblApInBar = null!;

	private StyleBoxFlat _hpFill = null!;
	private StyleBoxFlat _apFill = null!;

	public override void _Ready()
	{
		_pbHp = GetNode<ProgressBar>("Center/HBox/HpRow/PbHp");
		_lblHpInBar = GetNode<Label>("Center/HBox/HpRow/PbHp/LblHpInBar");
		_pbAp = GetNode<ProgressBar>("Center/HBox/ApRow/PbAp");
		_lblApInBar = GetNode<Label>("Center/HBox/ApRow/PbAp/LblApInBar");

		// Slightly smaller in-bar text for the compact HUD.
		_lblHpInBar.AddThemeFontSizeOverride("font_size", 12);
		_lblApInBar.AddThemeFontSizeOverride("font_size", 12);

		// Background style for both bars.
		var bg = new StyleBoxFlat
		{
			BgColor = new Color(0, 0, 0, 0.45f),
			CornerRadiusTopLeft = 2,
			CornerRadiusTopRight = 2,
			CornerRadiusBottomLeft = 2,
			CornerRadiusBottomRight = 2,
		};

		_hpFill = new StyleBoxFlat { BgColor = new Color(0.35f, 0.80f, 0.35f) };
		_hpFill.CornerRadiusTopLeft = 2;
		_hpFill.CornerRadiusTopRight = 2;
		_hpFill.CornerRadiusBottomLeft = 2;
		_hpFill.CornerRadiusBottomRight = 2;

		_apFill = new StyleBoxFlat { BgColor = new Color(0.35f, 0.65f, 0.90f) };
		_apFill.CornerRadiusTopLeft = 2;
		_apFill.CornerRadiusTopRight = 2;
		_apFill.CornerRadiusBottomLeft = 2;
		_apFill.CornerRadiusBottomRight = 2;

		_pbHp.AddThemeStyleboxOverride("background", bg);
		_pbHp.AddThemeStyleboxOverride("fill", _hpFill);
		_pbAp.AddThemeStyleboxOverride("background", bg);
		_pbAp.AddThemeStyleboxOverride("fill", _apFill);
	}

	public void SetValues(int hpCur, int hpMax, int apCur, int apMax)
	{
		hpMax = Math.Max(1, hpMax);
		apMax = Math.Max(1, apMax);
		hpCur = Math.Clamp(hpCur, 0, hpMax);
		apCur = Math.Clamp(apCur, 0, apMax);

		_pbHp.MaxValue = hpMax;
		_pbHp.Value = hpCur;
		_lblHpInBar.Text = $"{hpCur}/{hpMax}";

		_pbAp.MaxValue = apMax;
		_pbAp.Value = apCur;
		_lblApInBar.Text = $"{apCur}/{apMax}";

		UpdateHpColor((double)hpCur / hpMax);
		UpdateArmorColor((double)apCur / apMax);
	}

	private void UpdateHpColor(double pct)
	{
		pct = double.IsFinite(pct) ? Math.Clamp(pct, 0.0, 1.0) : 0.0;

		// Full should be light green, then dark green, yellow, dark red, bright red.
		Color c;
		if (pct >= 0.999)
			c = new Color(0.35f, 0.80f, 0.35f); // full green (slightly darker)
		else if (pct >= 0.70)
			c = new Color(0.10f, 0.55f, 0.10f); // dark green
		else if (pct >= 0.30)
			c = new Color(1.00f, 0.90f, 0.20f); // yellow
		else if (pct >= 0.10)
			c = new Color(0.55f, 0.05f, 0.05f); // dark red
		else
			c = new Color(1.00f, 0.15f, 0.15f); // bright red

		_hpFill.BgColor = c;
	}

	private void UpdateArmorColor(double pct)
	{
		pct = double.IsFinite(pct) ? Math.Clamp(pct, 0.0, 1.0) : 0.0;

		// Full = light blue; darker as percent decreases.
		var light = new Color(0.35f, 0.65f, 0.90f);
		var dark = new Color(0.05f, 0.20f, 0.45f);
		_apFill.BgColor = dark.Lerp(light, (float)pct);
	}
}

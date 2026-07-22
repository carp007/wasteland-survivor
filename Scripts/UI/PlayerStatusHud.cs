// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/UI/PlayerStatusHud.cs
// Purpose: UI view/controller code for scenes under Scenes/UI.
// -------------------------------------------------------------------------------------------------
using System;
using Godot;
using GameUiKit.SceneBinding;

namespace WastelandSurvivor.Game.UI;

/// <summary>
/// Simple top-right HUD for player status: HP + Armor Points (AP).
/// Implemented as a small, reusable UI control.
/// </summary>
public partial class PlayerStatusHud : PanelContainer
{
	private bool _bound;

	[Bind("Center/HBox/HpRow/PbHp")]
	private ProgressBar _pbHp = null!;

	[Bind("Center/HBox/HpRow/PbHp/LblHpInBar")]
	private Label _lblHpInBar = null!;

	[Bind("Center/HBox/ApRow/PbAp")]
	private ProgressBar _pbAp = null!;

	[Bind("Center/HBox/ApRow/PbAp/LblApInBar")]
	private Label _lblApInBar = null!;

	private StyleBoxFlat _hpFill = null!;
	private StyleBoxFlat _apFill = null!;

	private void EnsureBound()
	{
		if (_bound) return;
		SceneAutoBinder.Apply(this, nameof(PlayerStatusHud));
		_bound = true;
	}

	public override void _Ready()
	{
		GameUiTheme.ApplyToTree(this);
		EnsureBound();

		// Slightly smaller in-bar text for the compact HUD, with shadows for contrast (matches ValueBar).
		foreach (var lbl in new[] { _lblHpInBar, _lblApInBar })
		{
			lbl.AddThemeFontSizeOverride("font_size", 12);
			lbl.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.70f));
			lbl.AddThemeConstantOverride("shadow_offset_y", 1);
		}

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
		EnsureBound();

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

	// --- On-foot personal weapon row (Docs/ONFOOT_COMBAT_PLAN.md stage 1) -------------------------
	// Built lazily under the same VBox column as the bars; visible only while the driver is on foot
	// (the vehicle HUD hides then, so the top-right column has the room).
	private Label? _lblPersonalWeapon;

	/// <summary>
	/// Show/refresh the on-foot weapon line ("M9 SIDEARM · 42 rds"), or hide it (weaponName null)
	/// when the player is back in a vehicle.
	/// </summary>
	public void SetPersonalWeapon(string? weaponName, int ammo, int ammoCap)
	{
		EnsureBound();

		if (string.IsNullOrEmpty(weaponName))
		{
			if (_lblPersonalWeapon != null && GodotObject.IsInstanceValid(_lblPersonalWeapon))
				_lblPersonalWeapon.Visible = false;
			return;
		}

		if (_lblPersonalWeapon == null || !GodotObject.IsInstanceValid(_lblPersonalWeapon))
		{
			_lblPersonalWeapon = new Label
			{
				Name = "LblPersonalWeapon",
				HorizontalAlignment = HorizontalAlignment.Right,
				MouseFilter = MouseFilterEnum.Ignore,
			};
			if (GameUiTheme.DisplayFont is { } df)
				_lblPersonalWeapon.AddThemeFontOverride("font", df);
			_lblPersonalWeapon.AddThemeFontSizeOverride("font_size", 12);
			_lblPersonalWeapon.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.70f));
			_lblPersonalWeapon.AddThemeConstantOverride("shadow_offset_y", 1);
			// Sibling of the HUD panel, pinned just under its rect: the HUD panel is manually
			// anchored by the arena view (not container-driven), so a sibling keeps its manual
			// position — parenting INTO this PanelContainer would get layout-stomped.
			if (GetParent() is Control parent)
				parent.AddChild(_lblPersonalWeapon);
			else
				AddChild(_lblPersonalWeapon);
		}

		// Pin under the panel's current rect, right-aligned to its right edge.
		if (_lblPersonalWeapon.GetParent() == GetParent())
		{
			_lblPersonalWeapon.Position = Position + new Vector2(Size.X - 260f, Size.Y + 3f);
			_lblPersonalWeapon.Size = new Vector2(260f, 18f);
		}

		_lblPersonalWeapon.Visible = true;
		var low = ammoCap > 0 && ammo <= Math.Max(2, ammoCap / 8);
		_lblPersonalWeapon.AddThemeColorOverride("font_color",
			ammo <= 0 ? new Color(1f, 0.25f, 0.2f)
			: low ? new Color(1f, 0.72f, 0.25f)
			: GameUiTheme.TextColor);
		_lblPersonalWeapon.Text = ammo <= 0
			? $"{weaponName.ToUpperInvariant()} · DRY"
			: $"{weaponName.ToUpperInvariant()} · {ammo} rds";
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

// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/UI/TargetStatusHud.cs
// Purpose: UI view/controller code for scenes under Scenes/UI.
// -------------------------------------------------------------------------------------------------
using System;
using Godot;
using GameUiKit.SceneBinding;

namespace WastelandSurvivor.Game.UI;

/// <summary>
/// Upper-left HUD showing the currently targeted enemy and their driver HP/AP.
/// One-line, centered layout with in-bar values.
/// </summary>
public partial class TargetStatusHud : PanelContainer
{
	private bool _bound;

	[Bind("Center/HBox/LblTargetName")]
	private Label _lblTargetName = null!;

	[Bind("Center/HBox/HpBar")]
	private ValueBar _hpBar = null!;

	[Bind("Center/HBox/ApBar")]
	private ValueBar _apBar = null!;

	[Bind("Center/HBox/Spacer1")]
	private CanvasItem _spacer1 = null!;

	[Bind("Center/HBox/LblHp")]
	private CanvasItem _lblHp = null!;

	[Bind("Center/HBox/Spacer2")]
	private CanvasItem _spacer2 = null!;

	[Bind("Center/HBox/LblAp")]
	private CanvasItem _lblAp = null!;

	// Everything except the "Target:" prefix and name.
	private CanvasItem[] _hpApItems = Array.Empty<CanvasItem>();

	// Runtime-added hull readout: aggregate vehicle section condition. Without it the target HUD
	// only showed driver pools, and an entire fight of section damage read as "my bullets do nothing".
	private Label? _lblHull;
	private ValueBar? _hullBar;

	private void EnsureBound()
	{
		if (_bound) return;
		SceneAutoBinder.Apply(this, nameof(TargetStatusHud));

		if (_hullBar == null && _apBar != null && GodotObject.IsInstanceValid(_apBar) && _apBar.GetParent() is Node row)
		{
			var spacer = new Control { Name = "Spacer3", CustomMinimumSize = new Vector2(10f, 0f) };
			_lblHull = new Label { Name = "LblHull", Text = "HULL" };
			_lblHull.AddThemeFontSizeOverride("font_size", GameUiTheme.BaseFontSize - 2);
			_lblHull.AddThemeColorOverride("font_color", GameUiTheme.TextMutedColor);
			_hullBar = new ValueBar { Name = "HullBar", CustomMinimumSize = _apBar.CustomMinimumSize };
			row.AddChild(spacer);
			row.AddChild(_lblHull);
			row.AddChild(_hullBar);
		}

		_hpApItems = _hullBar is { } hullBar && _lblHull is { } lblHull
			? new CanvasItem[] { _spacer1, _lblHp, _hpBar, _spacer2, _lblAp, _apBar, lblHull, hullBar }
			: new CanvasItem[] { _spacer1, _lblHp, _hpBar, _spacer2, _lblAp, _apBar };
		_bound = true;
	}

	/// <summary>Aggregate section-structure condition of the locked vehicle (0..1).</summary>
	public void SetHullCondition(float fraction01)
	{
		EnsureBound();
		if (_hullBar == null) return;
		var pct = Mathf.Clamp(fraction01, 0f, 1f);
		_hullBar.SetValues((int)MathF.Round(pct * 100f), 100, HullColor(pct));
	}

	private static Color HullColor(float pct)
	{
		if (pct >= 0.66f) return new Color(0.80f, 0.66f, 0.20f);
		if (pct >= 0.33f) return new Color(0.95f, 0.55f, 0.15f);
		return new Color(0.90f, 0.20f, 0.12f);
	}

	public override void _Ready()
	{
		GameUiTheme.ApplyToTree(this);
		EnsureBound();
		// Display-font target name so the lock readout matches the HUD hierarchy.
		GameUiTheme.StyleHeading(_lblTargetName, GameUiTheme.BaseFontSize + 1, GameUiTheme.AccentGoldColor);
	}

	public void SetTarget(string? targetName, int hpCur, int hpMax, int apCur, int apMax)
	{
		EnsureBound();

		var hasTarget = !string.IsNullOrWhiteSpace(targetName) && targetName != "none";
		_lblTargetName.Text = hasTarget ? targetName! : "none";

		foreach (var it in _hpApItems)
			it.Visible = hasTarget;

		if (!hasTarget)
			return;

		hpMax = Math.Max(1, hpMax);
		apMax = Math.Max(1, apMax);
		hpCur = Math.Clamp(hpCur, 0, hpMax);
		apCur = Math.Clamp(apCur, 0, apMax);

		_hpBar.SetValues(hpCur, hpMax, HpColor(hpCur, hpMax));
		_apBar.SetValues(apCur, apMax, ArmorColor(apCur, apMax));
	}

	private static Color HpColor(int cur, int max)
	{
		max = Math.Max(1, max);
		var pct = (double)Math.Clamp(cur, 0, max) / max;

		if (pct >= 0.999)
			return new Color(0.35f, 0.80f, 0.35f);
		if (pct >= 0.70)
			return new Color(0.10f, 0.55f, 0.10f);
		if (pct >= 0.30)
			return new Color(1.00f, 0.90f, 0.20f);
		if (pct >= 0.10)
			return new Color(0.55f, 0.05f, 0.05f);
		return new Color(1.00f, 0.15f, 0.15f);
	}

	private static Color ArmorColor(int cur, int max)
	{
		max = Math.Max(1, max);
		var pct = (double)Math.Clamp(cur, 0, max) / max;
		var light = new Color(0.35f, 0.65f, 0.90f);
		var dark = new Color(0.05f, 0.20f, 0.45f);
		return dark.Lerp(light, (float)pct);
	}
}

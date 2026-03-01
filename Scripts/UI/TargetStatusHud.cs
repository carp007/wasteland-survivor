// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/UI/TargetStatusHud.cs
// Purpose: UI view/controller code for scenes under Scenes/UI.
// -------------------------------------------------------------------------------------------------
using System;
using Godot;
using WastelandSurvivor.Framework.SceneBinding;

namespace WastelandSurvivor.Game.UI;

/// <summary>
/// Upper-left HUD showing the currently targeted enemy and their driver HP/AP.
/// One-line, centered layout with in-bar values.
/// </summary>
public partial class TargetStatusHud : PanelContainer
{
	private Label _lblTargetName = null!;
	private ValueBar _hpBar = null!;
	private ValueBar _apBar = null!;

	// Everything except the "Target:" prefix and name.
	private CanvasItem[] _hpApItems = Array.Empty<CanvasItem>();

	private void EnsureBound()
	{
		var b = new SceneBinder(this, nameof(TargetStatusHud));
		_lblTargetName = b.Req<Label>("Center/HBox/LblTargetName");
		_hpBar = b.Req<ValueBar>("Center/HBox/HpBar");
		_apBar = b.Req<ValueBar>("Center/HBox/ApBar");

		_hpApItems = new CanvasItem[]
		{
			b.Req<CanvasItem>("Center/HBox/Spacer1"),
			b.Req<CanvasItem>("Center/HBox/LblHp"),
			b.Req<CanvasItem>("Center/HBox/HpBar"),
			b.Req<CanvasItem>("Center/HBox/Spacer2"),
			b.Req<CanvasItem>("Center/HBox/LblAp"),
			b.Req<CanvasItem>("Center/HBox/ApBar"),
		};
	}

	public override void _Ready()
	{

		GameUiTheme.ApplyToTree(this);
		EnsureBound();
	}

	public void SetTarget(string? targetName, int hpCur, int hpMax, int apCur, int apMax)
	{
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

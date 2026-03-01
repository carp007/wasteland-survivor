// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/UI/ActionPromptOverlay.cs
// Purpose: Small in-world action prompt that follows a 3D anchor and fades in/out cleanly.
// -------------------------------------------------------------------------------------------------
using System;
using Godot;
using WastelandSurvivor.Framework.SceneBinding;

namespace WastelandSurvivor.Game.UI;

/// <summary>
/// Small styled prompt that anchors to a 3D world position (typically an interactable entity)
/// and fades in/out cleanly as the player enters/leaves interaction range.
///
/// This is intentionally lightweight and generic so we can reuse it for future interactions
/// (loot, tires, salvage, tow points, etc.).
/// </summary>
public partial class ActionPromptOverlay : Control
{
	private const float FadeSpeed = 12f;
	private const float ScreenMargin = 10f;
	private const float PixelLift = 14f;

	private Camera3D? _camera;
	private Node3D? _target;
	private Vector3 _worldAnchor;
	private bool _hasWorldAnchor;

	private PanelContainer _panel = null!;
	private Label _lblKey = null!;
	private Label _lblAction = null!;

	private float _alpha = 0f;
	private float _alphaTarget = 0f;

	private void EnsureBound()
	{
		var b = new SceneBinder(this, nameof(ActionPromptOverlay));
		_panel = b.Req<PanelContainer>("PromptPanel");
		_lblKey = b.Req<Label>("PromptPanel/Margin/HBox/KeyBadge/KeyMargin/KeyLabel");
		_lblAction = b.Req<Label>("PromptPanel/Margin/HBox/ActionLabel");
	}

	public override void _Ready()
	{
		EnsureBound();

		MouseFilter = MouseFilterEnum.Ignore;
		_panel.MouseFilter = MouseFilterEnum.Ignore;

		_panel.Visible = false;
		_alpha = 0f;
		_alphaTarget = 0f;
		ApplyAlpha();
	}

	public void SetCamera(Camera3D? camera)
	{
		_camera = camera;
	}

	/// <summary>
	/// Show a prompt anchored to an entity. If the entity has a child Node3D named
	/// "InteractPromptAnchor" we use that; otherwise we fall back to a reasonable
	/// offset above the entity origin.
	/// </summary>
	public void ShowFor(Node3D target, string actionText, string keyText)
	{
		if (!GodotObject.IsInstanceValid(target))
		{
			Hide();
			return;
		}

		_target = target;
		_hasWorldAnchor = false;

		_lblKey.Text = keyText;
		// Keep copy short and clean; the key badge already implies "Press".
		_lblAction.Text = actionText;

		_panel.Visible = true;
		_alphaTarget = 1f;
	}

	/// <summary>
	/// Show a prompt anchored to an explicit world position.
	/// </summary>
	public void ShowAt(Vector3 worldAnchor, string actionText, string keyText)
	{
		_target = null;
		_worldAnchor = worldAnchor;
		_hasWorldAnchor = true;

		_lblKey.Text = keyText;
		_lblAction.Text = actionText;

		_panel.Visible = true;
		_alphaTarget = 1f;
	}

	public new void Hide()
	{
		_alphaTarget = 0f;
	}

	public override void _Process(double delta)
	{
		// Fade first.
		var dt = (float)delta;
		_alpha = Mathf.Lerp(_alpha, _alphaTarget, 1f - Mathf.Exp(-FadeSpeed * dt));
		ApplyAlpha();

		if (_alphaTarget <= 0.001f && _alpha <= 0.02f)
		{
			_panel.Visible = false;
			_target = null;
			_hasWorldAnchor = false;
			return;
		}

		if (_camera == null || !GodotObject.IsInstanceValid(_camera))
			return; // can't project

		// Resolve anchor.
		var anchor = ResolveAnchorWorldPos();
		if (anchor == null)
		{
			Hide();
			return;
		}

		var worldPos = anchor.Value;
		if (_camera.IsPositionBehind(worldPos))
		{
			// If camera can't see it, just fade it out.
			Hide();
			return;
		}

		var screen = _camera.UnprojectPosition(worldPos);
		PositionPanel(screen);
	}

	private void ApplyAlpha()
	{
		var c = _panel.Modulate;
		c.A = Mathf.Clamp(_alpha, 0f, 1f);
		_panel.Modulate = c;
	}

	private Vector3? ResolveAnchorWorldPos()
	{
		if (_hasWorldAnchor)
			return _worldAnchor;

		if (_target == null || !GodotObject.IsInstanceValid(_target))
			return null;

		// Prefer explicit anchor node if present.
		var anchorNode = _target.GetNodeOrNull<Node3D>("InteractPromptAnchor");
		if (anchorNode != null && GodotObject.IsInstanceValid(anchorNode))
			return anchorNode.GlobalPosition;

		// Fallback: a simple lift above the entity origin.
		return _target.GlobalPosition + new Vector3(0f, 1.65f, 0f);
	}

	private void PositionPanel(Vector2 screen)
	{
		// Use size if available, otherwise minimum size.
		var size = _panel.Size;
		if (size.X <= 0.1f || size.Y <= 0.1f)
			size = _panel.GetCombinedMinimumSize();

		// Center horizontally and lift above the anchor.
		var pos = new Vector2(screen.X - size.X * 0.5f, screen.Y - size.Y - PixelLift);

		// Clamp to viewport.
		var vp = GetViewportRect().Size;
		pos.X = Mathf.Clamp(pos.X, ScreenMargin, Math.Max(ScreenMargin, vp.X - size.X - ScreenMargin));
		pos.Y = Mathf.Clamp(pos.Y, ScreenMargin, Math.Max(ScreenMargin, vp.Y - size.Y - ScreenMargin));

		_panel.Position = pos;
	}
}

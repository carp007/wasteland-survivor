// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/UI/ActionPromptOverlay.cs
// Purpose: Small in-world action prompt that follows a 3D anchor and fades in/out cleanly.
// -------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using Godot;
using GameUiKit.SceneBinding;

namespace WastelandSurvivor.Game.UI;

/// <summary>
/// Small styled prompt that anchors to a 3D world position (typically an interactable entity)
/// and fades in/out cleanly as the player enters/leaves interaction range.
///
/// When several actions apply to the same target (for example enter + tow), the overlay shows
/// a compact multi-action list so one option does not hide the others.
/// </summary>
public partial class ActionPromptOverlay : Control
{
	private const float FadeSpeed = 12f;
	private const float ScreenMargin = 10f;
	private const float PixelLift = 14f;
	private const float AnchorGroupTolerance = 0.15f;

	private static readonly StyleBoxFlat KeyBadgeStyle = new()
	{
		BgColor = new Color(0.12f, 0.12f, 0.12f, 0.9f),
		BorderWidthLeft = 1,
		BorderWidthTop = 1,
		BorderWidthRight = 1,
		BorderWidthBottom = 1,
		BorderColor = new Color(0.85f, 0.72f, 0.25f, 1f),
		CornerRadiusTopLeft = 4,
		CornerRadiusTopRight = 4,
		CornerRadiusBottomRight = 4,
		CornerRadiusBottomLeft = 4,
	};

	private Camera3D? _camera;
	private Node3D? _target;
	private Vector3 _worldAnchor;
	private bool _hasWorldAnchor;
	private readonly List<ActionPromptCandidate> _displayedCandidates = new();

	[Bind("PromptPanel")]
	private PanelContainer _panel = null!;
	[Bind("PromptPanel/Margin/Options")]
	private VBoxContainer _options = null!;

	private float _alpha = 0f;
	private float _alphaTarget = 0f;

	/// <summary>Fires when the selected prompt target changes (useful for optional highlighting).</summary>
	public event Action<Node3D?>? ActiveTargetChanged;

	/// <summary>The currently selected prompt target (null when using an explicit world anchor).</summary>
	public Node3D? ActiveTarget => _hasWorldAnchor ? null : _target;

	/// <summary>The currently displayed candidate list for the focused anchor/target.</summary>
	public IReadOnlyList<ActionPromptCandidate> DisplayedCandidates => _displayedCandidates;

	public override void _Ready()
	{
		SceneAutoBinder.Apply(this, nameof(ActionPromptOverlay));

		MouseFilter = MouseFilterEnum.Ignore;
		_panel.MouseFilter = MouseFilterEnum.Ignore;
		_options.MouseFilter = MouseFilterEnum.Ignore;

		_panel.Visible = false;
		_alpha = 0f;
		_alphaTarget = 0f;
		ApplyAlpha();
	}

	/// <summary>
	/// Provide the list of prompt candidates for the current frame. The overlay selects a focused
	/// anchor by best priority/distance, then shows all candidates that apply to that same anchor.
	/// </summary>
	public void SetCandidates(IReadOnlyList<ActionPromptCandidate> candidates)
	{
		if (candidates == null || candidates.Count == 0)
		{
			ClearCandidate();
			return;
		}

		ActionPromptCandidate? best = null;
		for (var i = 0; i < candidates.Count; i++)
		{
			var c = candidates[i];
			if (string.IsNullOrWhiteSpace(c.ActionText) || string.IsNullOrWhiteSpace(c.KeyText))
				continue;
			if (c.Target == null && c.WorldAnchor == null)
				continue;

			if (best == null)
			{
				best = c;
				continue;
			}

			var b = best.Value;
			if (c.Priority > b.Priority)
				best = c;
			else if (c.Priority == b.Priority && c.Distance < b.Distance)
				best = c;
		}

		if (best == null)
		{
			ClearCandidate();
			return;
		}

		var focused = BuildFocusedCandidateGroup(candidates, best.Value);
		if (focused.Count == 0)
		{
			ClearCandidate();
			return;
		}

		ApplyCandidates(focused);
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
		if (!GodotObject.IsInstanceValid(target)) { ClearCandidate(); return; }
		SetCandidates(new[]
		{
			ActionPromptCandidate.ForTarget(target, actionText, keyText, ActionPromptAction.None, priority: 0, distance: 0f)
		});
	}

	/// <summary>
	/// Show a prompt anchored to an explicit world position.
	/// </summary>
	public void ShowAt(Vector3 worldAnchor, string actionText, string keyText)
	{
		SetCandidates(new[]
		{
			ActionPromptCandidate.ForWorld(worldAnchor, actionText, keyText, ActionPromptAction.None, priority: 0, distance: 0f)
		});
	}

	public new void Hide()
	{
		ClearCandidate();
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
			ClearTargetInternal();
			return;
		}

		if (_camera == null || !GodotObject.IsInstanceValid(_camera))
			return; // can't project

		// Resolve anchor.
		var anchor = ResolveAnchorWorldPos();
		if (anchor == null)
		{
			ClearCandidate();
			return;
		}

		var worldPos = anchor.Value;
		if (_camera.IsPositionBehind(worldPos))
		{
			// If camera can't see it, just fade it out.
			ClearCandidate();
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

	private static List<ActionPromptCandidate> BuildFocusedCandidateGroup(IReadOnlyList<ActionPromptCandidate> candidates, in ActionPromptCandidate best)
	{
		var focused = new List<ActionPromptCandidate>();
		for (var i = 0; i < candidates.Count; i++)
		{
			var c = candidates[i];
			if (!SharesAnchor(c, best))
				continue;
			focused.Add(c);
		}

		focused.Sort(static (a, b) =>
		{
			var priority = b.Priority.CompareTo(a.Priority);
			if (priority != 0) return priority;
			var distance = a.Distance.CompareTo(b.Distance);
			if (distance != 0) return distance;
			return string.Compare(a.KeyText, b.KeyText, StringComparison.OrdinalIgnoreCase);
		});
		return focused;
	}

	private static bool SharesAnchor(in ActionPromptCandidate a, in ActionPromptCandidate b)
	{
		if (a.Target != null || b.Target != null)
			return ReferenceEquals(a.Target, b.Target);

		if (a.WorldAnchor is { } aWorld && b.WorldAnchor is { } bWorld)
			return aWorld.DistanceTo(bWorld) <= AnchorGroupTolerance;

		return false;
	}

	private void ApplyCandidates(List<ActionPromptCandidate> nextCandidates)
	{
		var prevActive = ActiveTarget;
		var best = nextCandidates[0];
		var needsRebuild = !MatchesDisplayedCandidates(nextCandidates);

		_target = best.Target;
		_worldAnchor = best.WorldAnchor ?? default;
		_hasWorldAnchor = best.WorldAnchor != null;

		if (needsRebuild)
		{
			_displayedCandidates.Clear();
			_displayedCandidates.AddRange(nextCandidates);
			RebuildOptionRows();
		}

		_panel.Visible = true;
		_alphaTarget = 1f;

		var nextActive = ActiveTarget;
		if (!ReferenceEquals(prevActive, nextActive))
			ActiveTargetChanged?.Invoke(nextActive);
	}

	private bool MatchesDisplayedCandidates(IReadOnlyList<ActionPromptCandidate> nextCandidates)
	{
		if (_displayedCandidates.Count != nextCandidates.Count)
			return false;

		for (var i = 0; i < nextCandidates.Count; i++)
		{
			var cur = _displayedCandidates[i];
			var nxt = nextCandidates[i];
			if (!ReferenceEquals(cur.Target, nxt.Target))
				return false;
			if (cur.WorldAnchor != nxt.WorldAnchor)
				return false;
			if (!string.Equals(cur.ActionText, nxt.ActionText, StringComparison.Ordinal))
				return false;
			if (!string.Equals(cur.KeyText, nxt.KeyText, StringComparison.Ordinal))
				return false;
			if (cur.Action != nxt.Action || cur.ActionData != nxt.ActionData)
				return false;
		}

		return true;
	}

	private void RebuildOptionRows()
	{
		if (_options == null || !GodotObject.IsInstanceValid(_options))
			return;

		foreach (var child in _options.GetChildren())
			child.QueueFree();

		foreach (var candidate in _displayedCandidates)
			_options.AddChild(BuildOptionRow(candidate));
	}

	private Control BuildOptionRow(in ActionPromptCandidate candidate)
	{
		var row = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Begin,
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = SizeFlags.Fill,
		};
		row.AddThemeConstantOverride("separation", 10);

		var keyBadge = new PanelContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
		};
		keyBadge.AddThemeStyleboxOverride("panel", KeyBadgeStyle.Duplicate() as StyleBox ?? KeyBadgeStyle);

		var keyMargin = new MarginContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
		};
		keyMargin.AddThemeConstantOverride("margin_left", 10);
		keyMargin.AddThemeConstantOverride("margin_right", 10);
		keyMargin.AddThemeConstantOverride("margin_top", 4);
		keyMargin.AddThemeConstantOverride("margin_bottom", 4);

		var keyLabel = new Label
		{
			Text = candidate.KeyText,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		GameUiTheme.StyleHeading(keyLabel, 16, GameUiTheme.AccentGoldColor);
		keyLabel.AddThemeConstantOverride("outline_size", 1);

		var actionLabel = new Label
		{
			Text = candidate.ActionText,
			MouseFilter = MouseFilterEnum.Ignore,
			HorizontalAlignment = HorizontalAlignment.Left,
			SizeFlagsHorizontal = SizeFlags.Fill,
		};
		actionLabel.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.8f));
		actionLabel.AddThemeConstantOverride("shadow_offset_y", 1);
		actionLabel.AddThemeFontSizeOverride("font_size", 16);
		actionLabel.AddThemeConstantOverride("outline_size", 1);

		keyMargin.AddChild(keyLabel);
		keyBadge.AddChild(keyMargin);
		row.AddChild(keyBadge);
		row.AddChild(actionLabel);
		return row;
	}

	private void ClearCandidate()
	{
		_alphaTarget = 0f;
		var prev = ActiveTarget;
		ClearTargetInternal();
		_displayedCandidates.Clear();
		RebuildOptionRows();
		// Only fire after internal state is cleared.
		if (prev != null)
			ActiveTargetChanged?.Invoke(null);
	}

	private void ClearTargetInternal()
	{
		_target = null;
		_hasWorldAnchor = false;
	}
}

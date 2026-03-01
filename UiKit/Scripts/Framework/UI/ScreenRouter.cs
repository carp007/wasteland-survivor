// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Framework/UI/ScreenRouter.cs
// Purpose: Small reusable UI navigation helper. Centralizes screen instantiation and disposal so menu flows
//          are consistent and easier to evolve (future: push/pop, overlays, transitions).
// -------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using Godot;

namespace WastelandSurvivor.Framework.UI;

/// <summary>
/// Centralizes UI navigation for Control-based “screens” under a single host (typically a CanvasLayer).
/// 
/// Initial adoption goal: replace ad-hoc patterns like:
///   parent.AddChild(scene.Instantiate()); current.QueueFree();
/// with a single call that provides consistent logging and a predictable disposal policy.
/// 
/// This class is intentionally small and copyable so it can be reused in future Godot/C# projects.
/// </summary>
public sealed class ScreenRouter
{
	private readonly Node _host;
	private readonly List<Node> _stack = new();

	public ScreenRouter(Node host)
	{
		_host = host ?? throw new ArgumentNullException(nameof(host));
	}

	/// <summary>Current (top) screen managed by the router, or null if none.</summary>
	public Node? Current => _stack.Count == 0 ? null : _stack[^1];

	/// <summary>
	/// Replace the current stack with a single new screen.
	/// This is the safest “behavior-neutral” pattern while we refactor (equivalent to ChangeScreen).
	/// </summary>
	public bool TryReplace(string scenePath)
	{
		if (!TryInstantiate(scenePath, out var node))
			return false;

		Clear();
		_host.AddChild(node);
		_stack.Add(node);
		return true;
	}

	/// <summary>
	/// Push a new screen on top of the current one.
	/// Optional: hides the previous screen if it's a CanvasItem.
	/// </summary>
	public bool TryPush(string scenePath, bool hidePrevious = true)
	{
		if (!TryInstantiate(scenePath, out var node))
			return false;

		if (hidePrevious && Current != null)
			SetCanvasItemVisible(Current, false);

		_host.AddChild(node);
		_stack.Add(node);
		return true;
	}

	/// <summary>
	/// Pop the current screen and reveal the previous one (if any).
	/// </summary>
	public bool TryPop(bool freePopped = true)
	{
		if (_stack.Count == 0)
			return false;

		var top = _stack[^1];
		_stack.RemoveAt(_stack.Count - 1);

		if (freePopped)
			top.QueueFree();
		else
			_host.RemoveChild(top);

		if (Current != null)
			SetCanvasItemVisible(Current, true);

		return true;
	}

	/// <summary>
	/// Free all managed screens and clear the stack.
	/// </summary>
	public void Clear()
	{
		for (var i = _stack.Count - 1; i >= 0; i--)
		{
			try
			{
				_stack[i].QueueFree();
			}
			catch
			{
				// Ignore disposal exceptions; failing to free UI should not crash the whole game.
			}
		}

		_stack.Clear();
	}

	private static void SetCanvasItemVisible(Node node, bool visible)
	{
		if (node is CanvasItem ci)
			ci.Visible = visible;
	}

	private static bool TryInstantiate(string scenePath, out Node node)
	{
		node = null!;

		if (string.IsNullOrWhiteSpace(scenePath))
		{
			GD.PrintErr("[ScreenRouter] Missing scenePath.");
			return false;
		}

		if (!ResourceLoader.Exists(scenePath))
		{
			GD.PrintErr($"[ScreenRouter] Scene not found: {scenePath}");
			return false;
		}

		var scene = GD.Load<PackedScene>(scenePath);
		if (scene == null)
		{
			GD.PrintErr($"[ScreenRouter] Failed to load PackedScene: {scenePath}");
			return false;
		}

		node = scene.Instantiate();
		return true;
	}
}

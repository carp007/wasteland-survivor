// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Framework/SceneBinding/SceneBinder.cs
// Purpose: Typed node binding helper for Godot scenes. Centralizes GetNode/GetNodeOrNull patterns and
//          produces consistent, high-signal error messages when a scene tree doesn't match code.
//
// Why this exists
// - Binding failures are one of the highest-friction iteration bugs in Godot projects.
// - GetNode() exceptions often lack enough context to quickly determine the missing/renamed path.
// - Typed bindings (GetNode<T>) can silently fail when the node exists but the type doesn't match.
//
// Design goals
// - Zero gameplay behavior changes: it's just a nicer way to bind nodes.
// - Reusable: intended to be copy/paste friendly for future Godot C# projects.
// -------------------------------------------------------------------------------------------------
using System;
using Godot;

namespace WastelandSurvivor.Framework.SceneBinding;

/// <summary>
/// Typed helper for resolving nodes by path with consistent error reporting.
/// </summary>
public sealed class SceneBinder
{
	private readonly Node _root;
	private readonly string _context;

	public SceneBinder(Node root, string? context = null)
	{
		_root = root ?? throw new ArgumentNullException(nameof(root));
		_context = string.IsNullOrWhiteSpace(context) ? root.GetType().Name : context!;
	}

	/// <summary>
	/// Get a required node by path. Throws with a high-signal error message if missing/mismatched.
	/// </summary>
	public T Req<T>(string nodePath) where T : Node
	{
		if (string.IsNullOrWhiteSpace(nodePath))
			throw new ArgumentException("Node path cannot be null/empty.", nameof(nodePath));

		// Prefer typed lookup (fast path).
		var typed = _root.GetNodeOrNull<T>(nodePath);
		if (typed != null) return typed;

		// If the node exists but isn't the expected type, include that in the error.
		var raw = _root.GetNodeOrNull<Node>(nodePath);
		var rawType = raw?.GetType().FullName ?? "<missing>";
		var msg = $"{Prefix()} Missing or type-mismatched node at path '{nodePath}'. Expected '{typeof(T).FullName}', got '{rawType}'.";
		GD.PrintErr(msg);
		throw new InvalidOperationException(msg);
	}

	/// <summary>
	/// Get an optional node by path. Returns null if missing (or type mismatch).
	/// </summary>
	public T? Opt<T>(string nodePath) where T : Node
	{
		if (string.IsNullOrWhiteSpace(nodePath)) return null;
		return _root.GetNodeOrNull<T>(nodePath);
	}

	/// <summary>
	/// Get a required node using a primary path and a fallback path.
	/// Useful when scenes are in-flight and the node tree has multiple supported shapes.
	/// </summary>
	public T ReqFallback<T>(string primaryPath, string fallbackPath) where T : Node
	{
		var primary = _root.GetNodeOrNull<T>(primaryPath);
		if (primary != null) return primary;
		return Req<T>(fallbackPath);
	}

	private string Prefix()
	{
		// Node.GetPath() can throw if the node isn't in the scene tree yet.
		string rootPath;
		try
		{
			rootPath = _root.IsInsideTree() ? _root.GetPath().ToString() : _root.Name.ToString();
		}
		catch
		{
			rootPath = _root.Name.ToString();
		}

		return $"[SceneBinder:{_context}] Root='{rootPath}'";
	}
}

// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Framework/UI/UiNav.cs
// Purpose: Small navigation helper that prefers ScreenRouter (when available) and falls back to the
//          legacy parent-swap pattern. This keeps call sites small and consistent while refactoring.
// -------------------------------------------------------------------------------------------------
using Godot;

namespace WastelandSurvivor.Framework.UI;

/// <summary>
/// Tiny navigation helper used by UI controllers.
/// 
/// - Prefer <see cref="ScreenRouter"/> when one is supplied by the caller (router-first).
/// - Fall back to the pre-router pattern (load PackedScene, add to parent, QueueFree current).
/// 
/// This helper is intentionally small/copyable for reuse in other Godot/C# projects.
/// </summary>
public static class UiNav
{
	/// <summary>
	/// Replace the current screen with <paramref name="scenePath"/>.
	/// 
	/// If a <see cref="ScreenRouter"/> is available, it performs the replace.
	/// Otherwise it instantiates the scene under the current node's parent and frees the current node.
	/// </summary>
	public static bool Replace(Node current, string scenePath, ScreenRouter? router = null)
	{
		if (current == null)
			return false;

		// Preferred path: ScreenRouter (centralized navigation) if supplied by the caller.
		if (router != null)
		{
			var ok = router.TryReplace(scenePath);
			// If the caller isn't the router-managed current screen, free it so behavior matches legacy flows.
			if (ok && router.Current != current && GodotObject.IsInstanceValid(current))
				current.QueueFree();
			return ok;
		}

		// Fallback path: legacy parent-swap.
		var parent = current.GetParent();
		if (parent == null)
		{
			GD.PrintErr($"[UiNav] Cannot navigate to '{scenePath}' because '{current.Name}' has no parent.");
			return false;
		}

		if (string.IsNullOrWhiteSpace(scenePath))
		{
			GD.PrintErr("[UiNav] Missing scenePath.");
			return false;
		}

		if (!ResourceLoader.Exists(scenePath))
		{
			GD.PrintErr($"[UiNav] Scene not found: {scenePath}");
			return false;
		}

		var scene = GD.Load<PackedScene>(scenePath);
		if (scene == null)
		{
			GD.PrintErr($"[UiNav] Failed to load PackedScene: {scenePath}");
			return false;
		}

		var ui = scene.Instantiate();
		parent.AddChild(ui);
		current.QueueFree();
		return true;
	}

// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: UiKit/Scripts/Framework/UI/FullscreenTextureRectUtil.cs
// Purpose: Shared helpers for full-screen texture presentation without aspect-ratio skew.
// -------------------------------------------------------------------------------------------------
using Godot;

namespace GameUiKit.UI;

/// <summary>
/// Centralizes the texture-rect presentation rules we want for full-screen background / splash art.
/// Uses keep-aspect-covered behavior so images fill the viewport without skewing.
/// </summary>
public static class FullscreenTextureRectUtil
{
	private const int ExpandModeIgnoreSize = 1;
	private const int StretchModeKeepAspectCovered = 6;

	public static void ConfigureCover(TextureRect? rect)
	{
		if (rect == null || !GodotObject.IsInstanceValid(rect))
			return;

		rect.Set("expand_mode", ExpandModeIgnoreSize);
		rect.Set("stretch_mode", StretchModeKeepAspectCovered);
	}
}

// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: UiKit/Scripts/Framework/UI/OptionalBackgroundPresenter.cs
// Purpose: Reusable helper for optional full-screen menu background images.
// -------------------------------------------------------------------------------------------------
using Godot;

namespace GameUiKit.UI;

/// <summary>
/// Small helper that applies an optional background image to a screen while keeping a fallback solid color.
/// Intended for menu-like views where Assets/ images may or may not exist on disk.
/// </summary>
public static class OptionalBackgroundPresenter
{
	public static Texture2D? Apply(
		ColorRect? fallbackBackground,
		TextureRect? imageRect,
		string? texturePath,
		Color fallbackColor,
		Texture2D? cachedTexture = null)
	{
		if (fallbackBackground != null && GodotObject.IsInstanceValid(fallbackBackground))
			fallbackBackground.Color = fallbackColor;

		if (imageRect == null || !GodotObject.IsInstanceValid(imageRect))
			return cachedTexture;

		FullscreenTextureRectUtil.ConfigureCover(imageRect);

		if (string.IsNullOrWhiteSpace(texturePath) || !ResourceLoader.Exists(texturePath))
		{
			imageRect.Texture = null;
			imageRect.Visible = false;
			return null;
		}

		cachedTexture ??= GD.Load<Texture2D>(texturePath);
		imageRect.Texture = cachedTexture;
		imageRect.Visible = cachedTexture != null;
		return cachedTexture;
	}

	public static void Clear(TextureRect? imageRect)
	{
		if (imageRect == null || !GodotObject.IsInstanceValid(imageRect))
			return;

		FullscreenTextureRectUtil.ConfigureCover(imageRect);
		imageRect.Texture = null;
		imageRect.Visible = false;
	}
}

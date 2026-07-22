// -------------------------------------------------------------------------------------------------
// Wasteland Survivor (GameUiKit)
// File: UiKit/Scripts/Framework/UI/CoverArtControl.cs
// Purpose: Full-bleed art control that cover-crops its texture with configurable crop anchoring.
// -------------------------------------------------------------------------------------------------
using Godot;

namespace GameUiKit.UI;

/// <summary>
/// Draws a texture "cover" style (fill the control, preserve aspect, crop the overflow) with a
/// configurable crop anchor. Godot's built-in <c>TextureRect</c> KeepAspectCovered mode always
/// center-crops, which truncates art whose subject sits near an edge (e.g. a title treatment at
/// the top of a 3:2 splash image shown on a 16:9 screen). Anchor 0 keeps the top/left edge,
/// 0.5 matches the built-in centering, 1 keeps the bottom/right edge.
/// </summary>
public partial class CoverArtControl : Control
{
	private Texture2D? _texture;
	private float _verticalAnchor01 = 0.5f;
	private float _horizontalAnchor01 = 0.5f;
	private float _zoom = 1f;

	[Export]
	public Texture2D? Texture
	{
		get => _texture;
		set { _texture = value; QueueRedraw(); }
	}

	/// <summary>0 = keep top edge (crop bottom), 0.5 = center crop, 1 = keep bottom edge.</summary>
	[Export(PropertyHint.Range, "0,1,0.01")]
	public float VerticalAnchor01
	{
		get => _verticalAnchor01;
		set { _verticalAnchor01 = Mathf.Clamp(value, 0f, 1f); QueueRedraw(); }
	}

	/// <summary>0 = keep left edge (crop right), 0.5 = center crop, 1 = keep right edge.</summary>
	[Export(PropertyHint.Range, "0,1,0.01")]
	public float HorizontalAnchor01
	{
		get => _horizontalAnchor01;
		set { _horizontalAnchor01 = Mathf.Clamp(value, 0f, 1f); QueueRedraw(); }
	}

	/// <summary>
	/// Extra magnification on top of the cover fit (1 = none). Tween this slowly for a Ken Burns
	/// drift on title/backdrop art. The crop window shrinks around the same anchors, so an
	/// edge-pinned subject (e.g. top-anchored title lettering) stays pinned while zooming.
	/// </summary>
	[Export(PropertyHint.Range, "1,2,0.01")]
	public float Zoom
	{
		get => _zoom;
		set { _zoom = Mathf.Clamp(value, 1f, 2f); QueueRedraw(); }
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
			QueueRedraw();
	}

	public override void _Draw()
	{
		if (_texture == null)
			return;

		var size = Size;
		var texSize = _texture.GetSize();
		if (size.X <= 1f || size.Y <= 1f || texSize.X <= 1f || texSize.Y <= 1f)
			return;

		var destAspect = size.X / size.Y;
		var srcAspect = texSize.X / texSize.Y;

		Rect2 src;
		if (srcAspect > destAspect)
		{
			// Texture is wider than the control: full height, crop left/right.
			var w = texSize.Y * destAspect;
			src = new Rect2((texSize.X - w) * _horizontalAnchor01, 0f, w, texSize.Y);
		}
		else
		{
			// Texture is taller than the control: full width, crop top/bottom.
			var h = texSize.X / destAspect;
			src = new Rect2(0f, (texSize.Y - h) * _verticalAnchor01, texSize.X, h);
		}

		if (_zoom > 1.001f)
		{
			var zoomedSize = src.Size / _zoom;
			src = new Rect2(
				src.Position.X + (src.Size.X - zoomedSize.X) * _horizontalAnchor01,
				src.Position.Y + (src.Size.Y - zoomedSize.Y) * _verticalAnchor01,
				zoomedSize.X,
				zoomedSize.Y);
		}

		DrawTextureRectRegion(_texture, new Rect2(Vector2.Zero, size), src);
	}
}

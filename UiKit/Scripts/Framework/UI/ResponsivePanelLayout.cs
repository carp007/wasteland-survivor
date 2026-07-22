// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: UiKit/Scripts/Framework/UI/ResponsivePanelLayout.cs
// Purpose: Reusable viewport-aware panel sizing/positioning for menu screens.
// -------------------------------------------------------------------------------------------------
using Godot;

namespace GameUiKit.UI;

/// <summary>
/// Component that keeps a target Control inside the current viewport while preserving the authored panel feel
/// on larger screens. The intended pattern follows Godot's UI guidance:
/// use anchors/offsets for the outer screen-level panel, then use Container nodes for the interior layout.
/// </summary>
public partial class ResponsivePanelLayout : Node
{
	public enum HorizontalPlacementMode
	{
		Left,
		Center,
		Right
	}

	public enum VerticalPlacementMode
	{
		Top,
		Center,
		Bottom
	}

	[Export] public NodePath TargetPath { get; set; } = new("");
	[Export] public Vector2 MinSize { get; set; } = new(460, 520);
	[Export(PropertyHint.Range, "0.20,1.00,0.01")] public float MaxWidthPercent { get; set; } = 0.52f;
	[Export(PropertyHint.Range, "0.20,1.00,0.01")] public float MaxHeightPercent { get; set; } = 0.84f;
	[Export(PropertyHint.Range, "0.50,1.00,0.01")] public float VisualScale { get; set; } = 1.0f;
	[Export] public bool GrowWidthToMaxPercent { get; set; } = false;
	[Export] public bool GrowHeightToMaxPercent { get; set; } = false;
	[Export] public Vector2 Margin { get; set; } = new(24, 24);
	[Export] public bool StretchHeightToBottomMargin { get; set; } = false;
	[Export] public HorizontalPlacementMode HorizontalPlacement { get; set; } = HorizontalPlacementMode.Left;
	[Export] public VerticalPlacementMode VerticalPlacement { get; set; } = VerticalPlacementMode.Top;
	[Export(PropertyHint.Range, "0.00,0.40,0.01")] public float TopBiasPercent { get; set; } = 0.08f;

	private Control? _target;
	private Vector2 _authoredSize;

	public override void _Ready()
	{
		_target = GetNodeOrNull<Control>(TargetPath);
		if (_target == null)
		{
			GD.PrintErr($"[ResponsivePanelLayout] Missing target at '{TargetPath}'.");
			return;
		}

		_authoredSize = _target.Size;
		if (_authoredSize.X <= 1f || _authoredSize.Y <= 1f)
			_authoredSize = _target.GetRect().Size;
		if (_authoredSize.X <= 1f || _authoredSize.Y <= 1f)
			_authoredSize = MinSize;

		var viewport = GetViewport();
		if (viewport != null)
			viewport.SizeChanged += ApplyLayout;

		ApplyInitialLayoutAsync();
	}

	private async void ApplyInitialLayoutAsync()
	{
		if (!IsInsideTree())
			return;

		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		ApplyLayout();
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		ApplyLayout();
	}

	public override void _ExitTree()
	{
		var viewport = GetViewport();
		if (viewport != null)
			viewport.SizeChanged -= ApplyLayout;
	}

	private void ApplyLayout()
	{
		if (_target == null || !GodotObject.IsInstanceValid(_target))
			return;

		var viewport = GetViewport();
		if (viewport == null)
			return;

		var visibleSize = viewport.GetVisibleRect().Size;
		var window = GetWindow();
		var fallbackSize = window?.Size ?? DisplayServer.WindowGetSize();
		var viewSize = new Vector2(
			visibleSize.X > 1f ? visibleSize.X : fallbackSize.X,
			visibleSize.Y > 1f ? visibleSize.Y : fallbackSize.Y);

		var topMargin = Mathf.Max(Margin.Y, viewSize.Y * TopBiasPercent);
		var bottomMargin = Margin.Y;
		var leftMargin = Margin.X;
		var rightMargin = Margin.X;

		var availableWidth = Mathf.Max(64f, viewSize.X - leftMargin - rightMargin);
		var availableHeight = Mathf.Max(64f, viewSize.Y - topMargin - bottomMargin);

		var minWidth = Mathf.Min(MinSize.X, availableWidth);
		var minHeight = Mathf.Min(MinSize.Y, availableHeight);
		var maxWidth = Mathf.Min(availableWidth, Mathf.Max(minWidth, viewSize.X * MaxWidthPercent));
		var maxHeight = Mathf.Min(availableHeight, Mathf.Max(minHeight, viewSize.Y * MaxHeightPercent));

		var width = GrowWidthToMaxPercent
			? maxWidth
			: Mathf.Clamp(_authoredSize.X, minWidth, maxWidth);

		var shouldStretchHeight = StretchHeightToBottomMargin && VerticalPlacement == VerticalPlacementMode.Top;
		var height = shouldStretchHeight
			? availableHeight
			: GrowHeightToMaxPercent
				? maxHeight
				: Mathf.Clamp(_authoredSize.Y, minHeight, maxHeight);

		var layoutScale = Mathf.Clamp(VisualScale, 0.5f, 1.0f);
		if (!shouldStretchHeight)
			height *= layoutScale;
		width *= layoutScale;

		var x = HorizontalPlacement switch
		{
			HorizontalPlacementMode.Center => (viewSize.X - width) * 0.5f,
			HorizontalPlacementMode.Right => viewSize.X - width - rightMargin,
			_ => leftMargin,
		};

		var y = VerticalPlacement switch
		{
			VerticalPlacementMode.Center => (viewSize.Y - height) * 0.5f,
			VerticalPlacementMode.Bottom => viewSize.Y - height - bottomMargin,
			_ => topMargin,
		};

		var finalX = Mathf.Round(x);
		var finalY = Mathf.Round(y);
		var finalWidth = Mathf.Round(width);
		var finalHeight = Mathf.Round(height);

		if (shouldStretchHeight)
		{
			_target.AnchorLeft = 0.5f;
			_target.AnchorRight = 0.5f;
			_target.OffsetLeft = finalX - (viewSize.X * 0.5f);
			_target.OffsetRight = _target.OffsetLeft + finalWidth;

			_target.AnchorTop = 0f;
			_target.AnchorBottom = 1f;
			_target.OffsetTop = finalY;
			_target.OffsetBottom = -bottomMargin;
		}
		else
		{
			_target.AnchorLeft = 0f;
			_target.AnchorTop = 0f;
			_target.AnchorRight = 0f;
			_target.AnchorBottom = 0f;
			_target.OffsetLeft = finalX;
			_target.OffsetTop = finalY;
			_target.OffsetRight = finalX + finalWidth;
			_target.OffsetBottom = finalY + finalHeight;
			_target.Position = new Vector2(finalX, finalY);
			_target.Size = new Vector2(finalWidth, finalHeight);
		}

		ResetVisualTransform(finalWidth, finalHeight);
	}

	private void ResetVisualTransform(float width, float height)
	{
		if (_target == null || !GodotObject.IsInstanceValid(_target))
			return;

		// Scaling the actual Control transform proved unreliable for these premium menu shells.
		// Keep the control at identity scale and apply VisualScale through the layout bounds instead
		// so the visible shell really shrinks and reveals more of the background scene.
		_target.PivotOffset = new Vector2(width * 0.5f, height * 0.5f);
		_target.Scale = Vector2.One;
	}
}

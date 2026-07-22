// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/UI/TravelJourneyOverlay.cs
// Purpose: Skippable road-trip interstitial played between committing a travel leg and presenting
//          its outcome (arrival / ambush / bounty / merchant / scavenge modals). Turns "click city,
//          instant modal" into a beat of actual travel: scrolling highway, km ticker, fuel needle,
//          toll/station receipts, and a hard interrupt flash when the road event takes over.
// -------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using Godot;

namespace WastelandSurvivor.Game.UI;

/// <summary>
/// Full-screen presentational overlay. All game state is already committed by
/// <c>SessionWorld.TryTravelTo</c> before this shows — the overlay only dramatizes the leg, then
/// hands control to the caller's arrival continuation (which runs the pre-existing outcome modals).
/// Any key or click skips straight to arrival.
/// </summary>
public partial class TravelJourneyOverlay : Control
{
	public sealed record JourneyInfo
	{
		public string FromName { get; init; } = "";
		public string ToName { get; init; } = "";
		public float DistanceKm { get; init; }
		public float FuelUnitsBurned { get; init; }
		public float ArrivalFuelUnits { get; init; }
		public float FuelCapacityUnits { get; init; }
		public int TollUsd { get; init; }
		public bool StationStop { get; init; }
		public int StationCostUsd { get; init; }
		public int FreightPayout { get; init; }
		/// <summary>none | ambush | bounty | merchant | salvage | scavenge</summary>
		public string InterruptKind { get; init; } = "none";
		public Texture2D? VehicleIcon { get; init; }
	}

	private JourneyInfo _info = new();
	private Action? _onArrive;
	private bool _finished;

	private float _progress01;
	private float _elapsed;
	private float _duration = 7.0f;
	private bool _interruptFired;
	private float _interruptHold;
	private bool _stationStopFired;
	private float _stationStopHold;

	private RoadBand? _road;
	private TextureRect? _vehicleChip;
	private FallbackVehicleChip? _fallbackChip;
	private Label? _kmLabel;
	private Label? _flavorLabel;
	private Label? _interruptLabel;
	private ColorRect? _interruptFlash;
	private ProgressBar? _progressBar;
	private ProgressBar? _fuelBar;
	private int _flavorIndex = -1;

	private static readonly string[] FlavorPool =
	{
		"Burned-out tanker rusting on the median.",
		"Road sign shot full of holes — 40 years ago, by the look of it.",
		"A dust devil crosses three lanes and dies on the shoulder.",
		"Chained wrecks mark somebody's territory line.",
		"Crows lift off a guardrail as the engine closes.",
		"Faded billboard: DETROIT MOTOR SHOW '87.",
		"Skid marks veer off the asphalt and just... stop.",
		"An old toll plaza, booths stripped to the frames.",
		"Heat shimmer swallows the road a kilometer out.",
		"Somebody stacked tires into a warning cairn.",
	};

	private const float InterruptAt01 = 0.64f;

	/// <summary>Builds, parents, and runs the overlay. <paramref name="onArrive"/> always runs exactly once.</summary>
	public static TravelJourneyOverlay Show(Control host, JourneyInfo info, Action onArrive)
	{
		var overlay = new TravelJourneyOverlay
		{
			Name = "TravelJourneyOverlay",
			_info = info,
			_onArrive = onArrive,
			MouseFilter = MouseFilterEnum.Stop,
			FocusMode = FocusModeEnum.All,
		};
		host.AddChild(overlay);
		overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		overlay.CallDeferred(Control.MethodName.GrabFocus);
		return overlay;
	}

	public override void _Ready()
	{
		GameUiTheme.ApplyToTree(this);
		// Interrupt legs cut to the event at ~2/3 distance; quiet legs ride the whole way.
		_duration = HasInterrupt ? 5.2f : 7.0f;
		BuildUi();
	}

	private bool HasInterrupt => !string.IsNullOrEmpty(_info.InterruptKind)
		&& !string.Equals(_info.InterruptKind, "none", StringComparison.OrdinalIgnoreCase);

	private void BuildUi()
	{
		// Dusk-sky backdrop: near-black with a low horizon glow band above the road.
		var bg = new ColorRect { Name = "Bg", Color = new Color(0.02f, 0.025f, 0.04f), MouseFilter = MouseFilterEnum.Ignore };
		AddChild(bg);
		bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		var glowGradient = new Gradient();
		glowGradient.SetColor(0, new Color(0.30f, 0.16f, 0.07f, 0.0f));
		glowGradient.SetColor(1, new Color(0.42f, 0.22f, 0.09f, 0.55f));
		var glowTex = new GradientTexture2D
		{
			Gradient = glowGradient,
			FillFrom = new Vector2(0.5f, 0f),
			FillTo = new Vector2(0.5f, 1f),
			Width = 8,
			Height = 128,
		};
		var horizon = new TextureRect
		{
			Name = "HorizonGlow",
			Texture = glowTex,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		AddChild(horizon);
		horizon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		horizon.AnchorTop = 0.18f;
		horizon.AnchorBottom = 0.42f;

		// Scrolling road band across the middle.
		_road = new RoadBand { Name = "Road", MouseFilter = MouseFilterEnum.Ignore };
		AddChild(_road);
		_road.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_road.AnchorTop = 0.40f;
		_road.AnchorBottom = 0.78f;

		// Player vehicle chip riding the band: frozen showroom thumbnail when available, otherwise
		// a drawn silhouette so the road never reads empty on a first-ever trip.
		Control chip;
		if (_info.VehicleIcon != null)
		{
			_vehicleChip = new TextureRect
			{
				Name = "VehicleChip",
				Texture = _info.VehicleIcon,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				MouseFilter = MouseFilterEnum.Ignore,
			};
			chip = _vehicleChip;
		}
		else
		{
			chip = new FallbackVehicleChip { Name = "VehicleChipFallback", MouseFilter = MouseFilterEnum.Ignore };
			_fallbackChip = (FallbackVehicleChip)chip;
		}
		chip.CustomMinimumSize = new Vector2(210f, 132f);
		AddChild(chip);
		chip.SetAnchorsPreset(LayoutPreset.CenterLeft);
		chip.AnchorTop = 0.52f;
		chip.AnchorBottom = 0.52f;
		chip.OffsetLeft = 180f;
		chip.OffsetRight = 390f;
		chip.OffsetTop = -66f;
		chip.OffsetBottom = 66f;

		// Header: FROM → TO in the display font.
		var header = new Label
		{
			Name = "Header",
			Text = $"{_info.FromName.ToUpperInvariant()}   →   {_info.ToName.ToUpperInvariant()}",
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		if (GameUiTheme.DisplayBoldFont != null)
			header.AddThemeFontOverride("font", GameUiTheme.DisplayBoldFont);
		header.AddThemeFontSizeOverride("font_size", 34);
		header.AddThemeColorOverride("font_color", GameUiTheme.TextColor);
		AddChild(header);
		header.SetAnchorsPreset(LayoutPreset.CenterTop);
		header.OffsetTop = 64f;
		header.OffsetLeft = -560f;
		header.OffsetRight = 560f;

		_kmLabel = new Label
		{
			Name = "KmLabel",
			Text = BuildKmText(0f),
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		if (GameUiTheme.DisplayFont != null)
			_kmLabel.AddThemeFontOverride("font", GameUiTheme.DisplayFont);
		_kmLabel.AddThemeFontSizeOverride("font_size", 17);
		_kmLabel.AddThemeColorOverride("font_color", GameUiTheme.AccentGoldColor);
		AddChild(_kmLabel);
		_kmLabel.SetAnchorsPreset(LayoutPreset.CenterTop);
		_kmLabel.OffsetTop = 112f;
		_kmLabel.OffsetLeft = -300f;
		_kmLabel.OffsetRight = 300f;

		_progressBar = new ProgressBar
		{
			Name = "LegProgress",
			MinValue = 0,
			MaxValue = 1,
			Value = 0,
			ShowPercentage = false,
			CustomMinimumSize = new Vector2(0f, 8f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		AddChild(_progressBar);
		_progressBar.SetAnchorsPreset(LayoutPreset.CenterTop);
		_progressBar.OffsetTop = 146f;
		_progressBar.OffsetLeft = -320f;
		_progressBar.OffsetRight = 320f;
		_progressBar.OffsetBottom = 154f;

		// Receipts (toll / station / freight): lower-left stack, appearing as the leg progresses.
		var receipts = new VBoxContainer { Name = "Receipts", MouseFilter = MouseFilterEnum.Ignore };
		receipts.AddThemeConstantOverride("separation", 4);
		AddChild(receipts);
		receipts.SetAnchorsPreset(LayoutPreset.BottomLeft);
		receipts.OffsetLeft = 26f;
		receipts.OffsetTop = -170f;
		receipts.OffsetRight = 480f;
		receipts.OffsetBottom = -60f;
		AddReceiptLine(receipts, $"ROAD TOLL  -${_info.TollUsd:N0}", GameUiTheme.TextMutedColor);
		AddReceiptLine(receipts, $"FUEL BURN  {_info.FuelUnitsBurned:0.0} u", GameUiTheme.TextMutedColor);
		if (_info.StationStop)
			AddReceiptLine(receipts, $"STATION TOP-UP  -${_info.StationCostUsd:N0}", GameUiTheme.AccentCyanColor);
		if (_info.FreightPayout > 0)
			AddReceiptLine(receipts, $"FREIGHT ON DELIVERY  +${_info.FreightPayout:N0}", GameUiTheme.SuccessColor);

		// Fuel needle: arrival level animates down from departure as the leg runs.
		var fuelRow = new HBoxContainer { Name = "FuelRow", MouseFilter = MouseFilterEnum.Ignore };
		fuelRow.AddThemeConstantOverride("separation", 8);
		AddChild(fuelRow);
		fuelRow.SetAnchorsPreset(LayoutPreset.BottomRight);
		fuelRow.OffsetLeft = -360f;
		fuelRow.OffsetTop = -96f;
		fuelRow.OffsetRight = -26f;
		fuelRow.OffsetBottom = -66f;
		var fuelLabel = new Label { Text = "FUEL", MouseFilter = MouseFilterEnum.Ignore };
		if (GameUiTheme.DisplayFont != null)
			fuelLabel.AddThemeFontOverride("font", GameUiTheme.DisplayFont);
		fuelLabel.AddThemeFontSizeOverride("font_size", 14);
		fuelLabel.AddThemeColorOverride("font_color", GameUiTheme.TextMutedColor);
		fuelRow.AddChild(fuelLabel);
		_fuelBar = new ProgressBar
		{
			MinValue = 0,
			MaxValue = Math.Max(1f, _info.FuelCapacityUnits),
			Value = Math.Min(_info.FuelCapacityUnits, _info.ArrivalFuelUnits + _info.FuelUnitsBurned),
			ShowPercentage = false,
			CustomMinimumSize = new Vector2(220f, 10f),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		fuelRow.AddChild(_fuelBar);

		// Rolling road-flavor line.
		_flavorLabel = new Label
		{
			Name = "Flavor",
			Text = "",
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_flavorLabel.AddThemeFontSizeOverride("font_size", 15);
		_flavorLabel.AddThemeColorOverride("font_color", GameUiTheme.TextMutedColor);
		AddChild(_flavorLabel);
		_flavorLabel.SetAnchorsPreset(LayoutPreset.CenterBottom);
		_flavorLabel.OffsetTop = -150f;
		_flavorLabel.OffsetBottom = -122f;
		_flavorLabel.OffsetLeft = -560f;
		_flavorLabel.OffsetRight = 560f;

		// Skip hint.
		var skipHint = new Label
		{
			Name = "SkipHint",
			Text = "any key to skip",
			HorizontalAlignment = HorizontalAlignment.Right,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		skipHint.AddThemeFontSizeOverride("font_size", 13);
		skipHint.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0.55f));
		AddChild(skipHint);
		skipHint.SetAnchorsPreset(LayoutPreset.BottomRight);
		skipHint.OffsetLeft = -240f;
		skipHint.OffsetTop = -34f;
		skipHint.OffsetRight = -26f;
		skipHint.OffsetBottom = -14f;

		// Interrupt dressing (hidden until fired).
		_interruptFlash = new ColorRect
		{
			Name = "InterruptFlash",
			Color = new Color(0.9f, 0.15f, 0.08f, 0f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		AddChild(_interruptFlash);
		_interruptFlash.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		_interruptLabel = new Label
		{
			Name = "InterruptLabel",
			Text = "",
			HorizontalAlignment = HorizontalAlignment.Center,
			Visible = false,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		if (GameUiTheme.DisplayBoldFont != null)
			_interruptLabel.AddThemeFontOverride("font", GameUiTheme.DisplayBoldFont);
		_interruptLabel.AddThemeFontSizeOverride("font_size", 46);
		_interruptLabel.AddThemeColorOverride("font_color", new Color(1f, 0.36f, 0.22f));
		_interruptLabel.AddThemeConstantOverride("outline_size", 8);
		_interruptLabel.AddThemeColorOverride("font_outline_color", new Color(0.05f, 0.01f, 0.01f));
		AddChild(_interruptLabel);
		_interruptLabel.SetAnchorsPreset(LayoutPreset.Center);
		_interruptLabel.OffsetLeft = -560f;
		_interruptLabel.OffsetRight = 560f;
		_interruptLabel.OffsetTop = -40f;
		_interruptLabel.OffsetBottom = 40f;
	}

	private static void AddReceiptLine(VBoxContainer parent, string text, Color color)
	{
		var l = new Label { Text = text, MouseFilter = MouseFilterEnum.Ignore };
		l.AddThemeFontSizeOverride("font_size", 14);
		l.AddThemeColorOverride("font_color", color);
		parent.AddChild(l);
	}

	private string BuildKmText(float progress01)
	{
		var total = MathF.Max(1f, _info.DistanceKm);
		var at = Mathf.Clamp(progress01, 0f, 1f) * total;
		return $"KM {at:0} / {total:0}";
	}

	public override void _Process(double delta)
	{
		if (_finished) return;
		var dt = (float)delta;
		_elapsed += dt;

		// Interrupt hold: freeze the ride under the event banner, then hand over.
		if (_interruptFired)
		{
			_interruptHold -= dt;
			if (_interruptFlash != null)
			{
				var c = _interruptFlash.Color;
				c.A = MathF.Max(0f, c.A - dt * 0.9f);
				_interruptFlash.Color = c;
			}
			if (_interruptHold <= 0f)
				Finish();
			return;
		}

		// Station-stop beat: the mid-route top-up the leg already paid for gets its two seconds —
		// the road freezes at the pumps and the receipt line is the star (travel Stage 3 texture).
		if (_stationStopHold > 0f)
		{
			_stationStopHold -= dt;
			_elapsed -= dt; // freeze effective journey time so progress doesn't jump after the stop
			if (_road != null) _road.ScrollSpeed = 0f;
			if (_stationStopHold <= 0f && _interruptLabel != null)
				_interruptLabel.Visible = false;
			return;
		}
		if (_info.StationStop && !_stationStopFired && _progress01 >= 0.40f)
		{
			_stationStopFired = true;
			_stationStopHold = 1.8f;
			if (_interruptLabel != null)
			{
				_interruptLabel.Text = $"FUEL STOP  —  TOP-UP -${_info.StationCostUsd:N0}";
				_interruptLabel.AddThemeColorOverride("font_color", GameUiTheme.AccentCyanColor);
				_interruptLabel.AddThemeFontSizeOverride("font_size", 30);
				_interruptLabel.Visible = true;
			}
			return;
		}

		var target = HasInterrupt ? InterruptAt01 : 1.0f;
		_progress01 = MathF.Min(target, _elapsed / _duration * (HasInterrupt ? InterruptAt01 / 0.72f : 1f));

		if (_road != null)
		{
			_road.ScrollSpeed = 720f;
			// Rear-wheel screen x for the dust trail (chip anchored CenterLeft, offsets 180..390).
			_road.ChipScreenX = 235f;
		}
		if (_kmLabel != null)
			_kmLabel.Text = BuildKmText(_progress01);
		if (_progressBar != null)
			_progressBar.Value = _progress01;
		if (_fuelBar != null)
		{
			var departure = Math.Min(_info.FuelCapacityUnits, _info.ArrivalFuelUnits + _info.FuelUnitsBurned);
			_fuelBar.Value = Mathf.Lerp(departure, _info.ArrivalFuelUnits, _progress01);
		}
		var bobChip = (Control?)_vehicleChip ?? _fallbackChip;
		if (bobChip != null)
		{
			var bob = MathF.Sin(_elapsed * 9f) * 1.6f + MathF.Sin(_elapsed * 23f) * 0.7f;
			bobChip.OffsetTop = -66f + bob;
			bobChip.OffsetBottom = 66f + bob;
		}

		// Flavor beats roughly every 1.7s, never repeating back-to-back.
		var beat = (int)(_elapsed / 1.7f);
		if (beat != _flavorIndex && _flavorLabel != null)
		{
			_flavorIndex = beat;
			_flavorLabel.Text = FlavorPool[(int)((uint)(_info.FromName.GetHashCode() + beat * 7) % FlavorPool.Length)];
		}

		if (HasInterrupt && _progress01 >= InterruptAt01 - 0.0001f)
			FireInterrupt();
		else if (!HasInterrupt && _progress01 >= 1f)
			Finish();
	}

	private void FireInterrupt()
	{
		_interruptFired = true;
		_interruptHold = 1.15f;

		var (title, hostile) = _info.InterruptKind.ToLowerInvariant() switch
		{
			"ambush" => ("RAIDERS INBOUND", true),
			"bounty" => ("BOUNTY SIGHTED", true),
			"salvage" => ("RIVAL CREW ON THE ROAD", true),
			"merchant" => ("MERCHANT CONVOY AHEAD", false),
			"scavenge" => ("SIDE ROAD — SOMETHING GLINTS", false),
			_ => ("CONTACT", true),
		};

		if (_interruptLabel != null)
		{
			_interruptLabel.Text = title;
			_interruptLabel.Visible = true;
			_interruptLabel.AddThemeFontSizeOverride("font_size", 46);
			// Explicit both ways — the label is shared with the fuel-stop beat (cyan), so relying
			// on the constructor default here would leave a later RAIDERS banner cyan.
			_interruptLabel.AddThemeColorOverride("font_color", hostile
				? new Color(1f, 0.36f, 0.22f)
				: GameUiTheme.AccentGoldColor);
		}
		if (_interruptFlash != null)
			_interruptFlash.Color = hostile
				? new Color(0.9f, 0.15f, 0.08f, 0.34f)
				: new Color(0.9f, 0.65f, 0.15f, 0.22f);
		// (The danger stinger stays with the outcome modal in CityShell — playing it here too
		// would double-trigger it a second later.)
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		var skip = (@event is InputEventKey k && k.Pressed && !k.Echo)
			|| (@event is InputEventMouseButton mb && mb.Pressed);
		if (!skip) return;
		GetViewport().SetInputAsHandled();
		Finish();
	}

	private void Finish()
	{
		if (_finished) return;
		_finished = true;
		var arrive = _onArrive;
		_onArrive = null;
		QueueFree();
		arrive?.Invoke();
	}

	/// <summary>
	/// Drawn stand-in for the vehicle chip when no showroom thumbnail is cached yet: a simple
	/// side-view gold rig silhouette with wheels, good enough to carry the "that's me" read.
	/// </summary>
	private partial class FallbackVehicleChip : Control
	{
		public override void _Draw()
		{
			var s = Size;
			if (s.X < 20f || s.Y < 20f) return;
			var body = new Color(0.84f, 0.66f, 0.14f);
			var dark = new Color(0.10f, 0.10f, 0.12f);
			var glass = new Color(0.16f, 0.22f, 0.28f);
			var w = s.X * 0.72f;
			var h = s.Y * 0.30f;
			var x = (s.X - w) * 0.5f;
			var y = s.Y * 0.52f;
			// Lower hull + cabin block + glass slit.
			DrawRect(new Rect2(x, y, w, h), body);
			DrawRect(new Rect2(x + w * 0.18f, y - h * 0.62f, w * 0.44f, h * 0.62f), body);
			DrawRect(new Rect2(x + w * 0.22f, y - h * 0.50f, w * 0.20f, h * 0.42f), glass);
			// Wheels.
			var wy = y + h + 2f;
			DrawCircle(new Vector2(x + w * 0.22f, wy), s.Y * 0.085f, dark);
			DrawCircle(new Vector2(x + w * 0.78f, wy), s.Y * 0.085f, dark);
			DrawCircle(new Vector2(x + w * 0.22f, wy), s.Y * 0.038f, new Color(0.35f, 0.35f, 0.38f));
			DrawCircle(new Vector2(x + w * 0.78f, wy), s.Y * 0.038f, new Color(0.35f, 0.35f, 0.38f));
		}
	}

	// ---------------------------------------------------------------------------------------------
	// Road band: asphalt strip with scrolling dashed centerline, shoulder lines, two parallax
	// silhouette bands (judge round: the first cut's ~16px lumps read as a loading screen), and
	// dust kicked up behind the vehicle chip. Pure _Draw — no assets, theme-consistent.
	// ---------------------------------------------------------------------------------------------
	private partial class RoadBand : Control
	{
		public float ScrollSpeed;
		/// <summary>Screen-x of the vehicle chip's rear wheel, for the dust trail.</summary>
		public float ChipScreenX = 300f;

		private float _scroll;
		private float _dustTimer;
		private readonly List<(float x, float y, float age, float seed)> _dust = new();

		public override void _Process(double delta)
		{
			if (ScrollSpeed <= 0f) return;
			var dt = (float)delta;
			_scroll += dt * ScrollSpeed;

			// Dust trail behind the chip: spawn small, drift left, grow + fade.
			_dustTimer -= dt;
			if (_dustTimer <= 0f)
			{
				_dustTimer = 0.11f;
				var seed = (_scroll % 97f) / 97f;
				_dust.Add((ChipScreenX - 52f + seed * 18f, Size.Y * 0.62f + (seed - 0.5f) * 16f, 0f, seed));
			}
			for (var i = _dust.Count - 1; i >= 0; i--)
			{
				var d = _dust[i];
				d.age += dt;
				d.x -= dt * ScrollSpeed * 0.55f;
				if (d.age > 0.9f) _dust.RemoveAt(i);
				else _dust[i] = d;
			}

			QueueRedraw();
		}

		public override void _Draw()
		{
			var size = Size;
			if (size.X < 4f || size.Y < 4f) return;

			// --- Horizon silhouettes (slow parallax, drawn ABOVE the band; big enough to be scenery).
			var dark = new Color(0.028f, 0.030f, 0.038f);
			var slow = -(_scroll * 0.30f);
			const float horizonPeriod = 470f;
			for (var i = 0; i < 9; i++)
			{
				var px = ((slow + i * horizonPeriod) % (size.X + horizonPeriod));
				if (px < 0) px += size.X + horizonPeriod;
				px -= horizonPeriod * 0.5f;
				if (px < -160f || px > size.X + 160f) continue;
				switch (i % 4)
				{
					case 0:
						// Water tower: legs + tank + cap.
						DrawRect(new Rect2(px + 6f, -132f, 5f, 132f), dark);
						DrawRect(new Rect2(px + 46f, -132f, 5f, 132f), dark);
						DrawRect(new Rect2(px + 20f, -110f, 5f, 60f), dark);
						DrawRect(new Rect2(px + 32f, -110f, 5f, 60f), dark);
						DrawRect(new Rect2(px - 4f, -176f, 66f, 48f), dark);
						DrawRect(new Rect2(px + 12f, -188f, 34f, 14f), dark);
						break;
					case 1:
						// Power pole with sagging line stubs.
						DrawRect(new Rect2(px, -150f, 6f, 152f), dark);
						DrawRect(new Rect2(px - 26f, -140f, 58f, 5f), dark);
						DrawRect(new Rect2(px - 18f, -122f, 42f, 4f), dark);
						break;
					case 2:
						// Stacked wreck pile.
						DrawRect(new Rect2(px - 30f, -34f, 96f, 36f), dark);
						DrawRect(new Rect2(px - 8f, -62f, 66f, 30f), dark);
						DrawRect(new Rect2(px + 12f, -80f, 34f, 20f), dark);
						break;
					default:
						// Dead tree: trunk + two branch strokes.
						DrawRect(new Rect2(px, -96f, 6f, 98f), dark);
						DrawRect(new Rect2(px - 22f, -78f, 26f, 4f), dark);
						DrawRect(new Rect2(px + 5f, -60f, 30f, 4f), dark);
						DrawRect(new Rect2(px - 12f, -110f, 4f, 20f), dark);
						break;
				}
			}

			// --- Asphalt slab with soft vertical tone shift (bright enough to read as a surface).
			DrawRect(new Rect2(0, 0, size.X, size.Y), new Color(0.115f, 0.118f, 0.128f));
			DrawRect(new Rect2(0, size.Y * 0.5f, size.X, size.Y * 0.5f), new Color(0.095f, 0.098f, 0.108f));

			// Shoulder lines.
			var shoulder = new Color(0.55f, 0.52f, 0.42f, 0.55f);
			DrawRect(new Rect2(0, size.Y * 0.06f, size.X, 3f), shoulder);
			DrawRect(new Rect2(0, size.Y * 0.94f - 3f, size.X, 3f), shoulder);

			// Dashed centerline scrolling right-to-left.
			const float dashLen = 64f;
			const float gapLen = 46f;
			var period = dashLen + gapLen;
			var offset = -(_scroll % period);
			var y = size.Y * 0.5f - 3f;
			for (var x = offset; x < size.X; x += period)
				DrawRect(new Rect2(x, y, dashLen, 6f), new Color(0.85f, 0.74f, 0.35f, 0.8f));

			// --- Near-shoulder wrecks (full parallax, big, occasional) parked below the road band.
			var near = -(_scroll * 0.85f);
			const float nearPeriod = 1050f;
			for (var i = 0; i < 4; i++)
			{
				var px = ((near + i * nearPeriod) % (size.X + nearPeriod));
				if (px < 0) px += size.X + nearPeriod;
				px -= nearPeriod * 0.5f;
				if (px < -200f || px > size.X + 200f) continue;
				var tone = new Color(0.045f, 0.045f, 0.055f);
				var wy = size.Y * 0.97f;
				// Burned-out car profile: body, cabin, wheel stumps.
				DrawRect(new Rect2(px, wy, 150f, 34f), tone);
				DrawRect(new Rect2(px + 34f, wy - 24f, 66f, 26f), tone);
				DrawCircle(new Vector2(px + 34f, wy + 36f), 13f, tone);
				DrawCircle(new Vector2(px + 116f, wy + 36f), 13f, tone);
			}

			// --- Dust trail behind the chip.
			foreach (var d in _dust)
			{
				var t = d.age / 0.9f;
				var r = 5f + t * 13f;
				var a = 0.26f * (1f - t);
				DrawCircle(new Vector2(d.x, d.y), r, new Color(0.42f, 0.38f, 0.30f, a));
			}
		}
	}
}

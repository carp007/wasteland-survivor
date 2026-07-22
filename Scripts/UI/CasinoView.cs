// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/UI/CasinoView.cs
// Purpose: "The Rusty Jackpot" — diegetic gambling den in select cities (CityDefinition.HasCasino;
//          master spec income sources: "later: casino in some cities"). Two quick games: the
//          Wasteland Wheel (weighted spin drawn as a custom Control, eased ~2s spin that lands
//          deterministically on the already-settled wedge) and Highway Dice (2d6 vs the house with
//          drawn pips). All money flows through SessionWorld casino methods — this view only
//          animates, narrates, and tracks per-visit flavor (house take + pit-boss mercy comp).
// -------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using Godot;
using GameUiKit.SceneBinding;
using GameUiKit.UI;
using WastelandSurvivor.Core.Defs;
using WastelandSurvivor.Core.IO;
using WastelandSurvivor.Game.Audio;
using WastelandSurvivor.Game.Navigation;
using WastelandSurvivor.Game.Session;

namespace WastelandSurvivor.Game.UI;

public partial class CasinoView : Control
{
	[Bind("Panel")]
	private PanelContainer _panel = null!;

	[Bind("Bg")]
	private ColorRect _bg = null!;

	[Bind("BgImage")]
	private TextureRect _bgImage = null!;

	[Bind("Panel/VBox/LblTitle")]
	private Label _lblTitle = null!;

	[Bind("Panel/VBox/LblSubtitle")]
	private Label _lblSubtitle = null!;

	[Bind("Panel/VBox/LblWallet")]
	private Label _lblWallet = null!;

	[Bind("Panel/VBox/LblHouse")]
	private Label _lblHouse = null!;

	[Bind("Panel/VBox/BodyScroll/Body")]
	private VBoxContainer _body = null!;

	[Bind("Panel/VBox/BottomRow/LblStatus")]
	private Label _lblStatus = null!;

	[Bind("Panel/VBox/BottomRow/BtnBack")]
	private Button _btnBack = null!;

	private static MenuDrawerSpec CasinoDrawerSpec => new(1000f, 660f, 0.55f, 24f, 84f, 24f, 24f);

	private Texture2D? _menuBackground;
	private bool _casinoOpen;

	// Wheel runtime state (built in code, same pattern as the store rows).
	private WheelControl _wheel = null!;
	private Button _btnSpin = null!;
	private Label _lblWheelResult = null!;
	private Label _lblComp = null!;
	private readonly List<Button> _stakeButtons = new();
	private int _selectedStakeUsd = 100;
	private bool _spinning;
	private int _pendingStakeUsd;
	private bool _pendingComped;
	private int _pendingMultiplier;
	private int _pendingPayoutUsd;

	// Dice runtime state.
	private DicePairControl _dice = null!;
	private Button _btnDice = null!;
	private Label _lblDiceResult = null!;

	// Per-visit flavor (deliberately not saved): running house take + pit-boss mercy timer.
	private int _houseTakeUsd;
	private int _consecutiveBusts;
	private bool _compAvailable;

	public override void _Ready()
	{
		GameUiTheme.ApplyToTree(this);
		SceneAutoBinder.Apply(this, nameof(CasinoView));

		GameUiTheme.StyleHeading(_lblTitle, GameUiTheme.TitleFontSize + 12);
		_lblSubtitle.AddThemeColorOverride("font_color", GameUiTheme.TextMutedColor);
		_lblSubtitle.AddThemeFontSizeOverride("font_size", GameUiTheme.BaseFontSize - 1);
		_lblSubtitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_lblWallet.AddThemeColorOverride("font_color", GameUiTheme.AccentGoldColor);
		_lblHouse.AddThemeColorOverride("font_color", GameUiTheme.AccentCyanColor);
		_lblHouse.AddThemeFontSizeOverride("font_size", GameUiTheme.BaseFontSize - 1);
		_lblStatus.AddThemeColorOverride("font_color", GameUiTheme.TextMutedColor);

		_panel.AddThemeStyleboxOverride("panel", GeneratedUiArt.CreatePremiumShellStyle());
		_btnBack.Pressed += () => Nav()?.ToCityShell(this);

		BuildProceduralBackdrop();

		// Same treatment as StoreView/DriverStoreView, dimmed warmer: the den runs on neon,
		// cigarette haze, and bad decisions.
		_menuBackground = ResolveCityBackdrop();
		_bg.Color = GameUiTheme.BackgroundColor;
		FullscreenTextureRectUtil.ConfigureCover(_bgImage);
		_bgImage.Texture = _menuBackground;
		_bgImage.Visible = _menuBackground != null;
		_bgImage.SelfModulate = new Color(0.26f, 0.21f, 0.20f);

		ApplyDrawer();
		GetViewport().SizeChanged += ApplyDrawer;

		var city = ResolveCurrentCity();
		_casinoOpen = city is { HasCasino: true };
		_lblSubtitle.Text = _casinoOpen
			? $"Back-room tables off the {city!.DisplayName} strip. Two games, house rules, no credit."
			: "No gambling den in this city. Chicago's lakefront and the Erie road stop both run tables.";

		_body.AddThemeConstantOverride("separation", 12);
		BuildWheelSection();
		BuildDiceSection();

		// Tail spacer so the last section scrolls clear of the footer instead of clipping under it.
		_body.AddChild(new Control
		{
			CustomMinimumSize = new Vector2(0, 72),
			MouseFilter = MouseFilterEnum.Ignore,
		});

		RefreshMoneyLines();
		UpdateWheelControls();
		UpdateDiceControls();
	}

	public override void _ExitTree()
	{
		var viewport = GetViewport();
		if (viewport != null)
			viewport.SizeChanged -= ApplyDrawer;
	}

	private void ApplyDrawer() => MenuDrawerLayout.Apply(_panel, this, CasinoDrawerSpec);

	// --- Section builders ---------------------------------------------------------------------

	private (PanelContainer panel, VBoxContainer vbox) AddSectionScaffold(string title, string note)
	{
		var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		panel.AddThemeStyleboxOverride("panel", GeneratedUiArt.CreatePremiumSectionStyle(emphasized: true));
		_body.AddChild(panel);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 14);
		margin.AddThemeConstantOverride("margin_right", 14);
		margin.AddThemeConstantOverride("margin_top", 10);
		margin.AddThemeConstantOverride("margin_bottom", 12);
		panel.AddChild(margin);

		var vbox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		vbox.AddThemeConstantOverride("separation", 8);
		margin.AddChild(vbox);

		var heading = new Label { Text = title };
		GameUiTheme.StyleHeading(heading, GameUiTheme.BaseFontSize + 3, GameUiTheme.AccentCyanColor);
		vbox.AddChild(heading);

		var noteLbl = new Label { Text = note, AutowrapMode = TextServer.AutowrapMode.WordSmart };
		noteLbl.AddThemeColorOverride("font_color", GameUiTheme.TextMutedColor);
		noteLbl.AddThemeFontSizeOverride("font_size", GameUiTheme.BaseFontSize - 2);
		vbox.AddChild(noteLbl);

		return (panel, vbox);
	}

	private void BuildWheelSection()
	{
		var (_, vbox) = AddSectionScaffold(
			"WASTELAND WHEEL",
			"Pick a stake and spin. The marked wedges pay x2 and x3, the thin gold sliver pays x5 — everything else feeds the house.");

		var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		row.AddThemeConstantOverride("separation", 18);
		vbox.AddChild(row);

		_wheel = new WheelControl
		{
			CustomMinimumSize = new Vector2(300f, 300f),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		row.AddChild(_wheel);

		var controls = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		controls.AddThemeConstantOverride("separation", 8);
		row.AddChild(controls);

		var stakeLabel = new Label { Text = "STAKE" };
		GameUiTheme.StyleHeading(stakeLabel, GameUiTheme.BaseFontSize - 1, GameUiTheme.TextMutedColor);
		controls.AddChild(stakeLabel);

		var stakeRow = new HBoxContainer();
		stakeRow.AddThemeConstantOverride("separation", 6);
		controls.AddChild(stakeRow);

		var group = new ButtonGroup();
		foreach (var stake in CasinoRules.WheelStakesUsd)
		{
			var stakeUsd = stake;
			var btn = new Button
			{
				Text = $"${stakeUsd}",
				ToggleMode = true,
				ButtonGroup = group,
				ButtonPressed = stakeUsd == _selectedStakeUsd,
				CustomMinimumSize = new Vector2(76f, 36f),
			};
			btn.Pressed += () =>
			{
				_selectedStakeUsd = stakeUsd;
				UpdateWheelControls();
			};
			_stakeButtons.Add(btn);
			stakeRow.AddChild(btn);
		}

		_btnSpin = new Button
		{
			Text = "Spin",
			CustomMinimumSize = new Vector2(190f, 42f),
			SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
		};
		_btnSpin.Pressed += OnSpinPressed;
		controls.AddChild(_btnSpin);

		_lblComp = new Label { Text = "", AutowrapMode = TextServer.AutowrapMode.WordSmart };
		_lblComp.AddThemeColorOverride("font_color", GameUiTheme.AccentGoldColor);
		_lblComp.AddThemeFontSizeOverride("font_size", GameUiTheme.BaseFontSize - 2);
		controls.AddChild(_lblComp);

		_lblWheelResult = new Label { Text = "The wheel waits.", AutowrapMode = TextServer.AutowrapMode.WordSmart };
		_lblWheelResult.AddThemeColorOverride("font_color", GameUiTheme.TextMutedColor);
		controls.AddChild(_lblWheelResult);
	}

	private void BuildDiceSection()
	{
		var (_, vbox) = AddSectionScaffold(
			"HIGHWAY DICE",
			$"Flat ${CasinoRules.DiceStakeUsd} against the house — two dice each, higher total takes ${CasinoRules.DiceWinPayoutUsd}. Ties push.");

		var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		row.AddThemeConstantOverride("separation", 18);
		vbox.AddChild(row);

		_dice = new DicePairControl
		{
			CustomMinimumSize = new Vector2(280f, 110f),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		row.AddChild(_dice);

		var controls = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		controls.AddThemeConstantOverride("separation", 8);
		row.AddChild(controls);

		_btnDice = new Button
		{
			Text = "Roll",
			CustomMinimumSize = new Vector2(190f, 42f),
			SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
		};
		_btnDice.Pressed += OnDicePressed;
		controls.AddChild(_btnDice);

		_lblDiceResult = new Label
		{
			Text = "The dealer flips you the cup.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		_lblDiceResult.AddThemeColorOverride("font_color", GameUiTheme.TextMutedColor);
		controls.AddChild(_lblDiceResult);
	}

	// --- Game flow ------------------------------------------------------------------------------

	private void OnSpinPressed()
	{
		if (_spinning)
			return;

		var app = App.Instance;
		if (app == null) return;
		if (!app.Services.TryGet<GameSession>(out var session) || session == null) return;
		if (!app.Services.TryGet<DefDatabase>(out var defs) || defs == null) return;

		var comped = _compAvailable;
		var stake = comped ? CasinoRules.CompSpinStakeUsd : _selectedStakeUsd;
		if (!session.World.TryPlaceCasinoWheelBet(defs, stake, comped, out var wedgeIndex, out var multiplier, out var payout, out var error))
		{
			UiSfx.Play("error");
			SetStatus(error);
			return;
		}

		if (comped)
		{
			_compAvailable = false;
			_lblComp.Text = "";
		}

		// The bet is already settled in the save; the wheel now just performs the result.
		_pendingStakeUsd = stake;
		_pendingComped = comped;
		_pendingMultiplier = multiplier;
		_pendingPayoutUsd = payout;
		_spinning = true;
		UpdateWheelControls();
		UpdateDiceControls();
		UiSfx.Play("click");
		_lblWheelResult.Text = "Round and round she goes...";
		_wheel.SpinToWedge(wedgeIndex, OnSpinLanded);
	}

	private void OnSpinLanded()
	{
		_spinning = false;
		_houseTakeUsd += (_pendingComped ? 0 : _pendingStakeUsd) - _pendingPayoutUsd;

		if (_pendingMultiplier > 0)
		{
			UiSfx.Play("purchase");
			_consecutiveBusts = 0;
			var compTag = _pendingComped ? " — on the house's own money, too" : "";
			_lblWheelResult.Text = $"x{_pendingMultiplier}! ${_pendingPayoutUsd:N0} out of the cage{compTag}.";
			SetStatus($"The wheel lands x{_pendingMultiplier} — ${_pendingPayoutUsd:N0} pays out.");
		}
		else
		{
			UiSfx.Play("error");
			_consecutiveBusts++;
			_lblWheelResult.Text = _pendingComped
				? "Bust — but it was the house's money anyway."
				: $"Bust. ${_pendingStakeUsd:N0} joins the house take.";
			SetStatus("The wheel comes up empty.");
			MaybeTriggerComp();
		}

		RefreshMoneyLines();
		UpdateWheelControls();
		UpdateDiceControls();
	}

	private void OnDicePressed()
	{
		if (_spinning)
			return;

		var app = App.Instance;
		if (app == null) return;
		if (!app.Services.TryGet<GameSession>(out var session) || session == null) return;
		if (!app.Services.TryGet<DefDatabase>(out var defs) || defs == null) return;

		if (!session.World.TryPlayHighwayDice(defs, out var p1, out var p2, out var h1, out var h2, out var payout, out var error))
		{
			UiSfx.Play("error");
			SetStatus(error);
			return;
		}

		_dice.ShowRoll(p1, p2, h1, h2);
		var playerTotal = p1 + p2;
		var houseTotal = h1 + h2;
		_houseTakeUsd += CasinoRules.DiceStakeUsd - payout;

		if (playerTotal > houseTotal)
		{
			UiSfx.Play("purchase");
			_consecutiveBusts = 0;
			_lblDiceResult.Text = $"You roll {playerTotal}, the house rolls {houseTotal}. Winner — ${CasinoRules.DiceWinPayoutUsd} slides across the felt.";
		}
		else if (playerTotal == houseTotal)
		{
			UiSfx.Play("click");
			_lblDiceResult.Text = $"Both tables show {playerTotal}. Push — your ${CasinoRules.DiceStakeUsd} comes back.";
		}
		else
		{
			UiSfx.Play("error");
			_consecutiveBusts++;
			_lblDiceResult.Text = $"You roll {playerTotal}, the house rolls {houseTotal}. The felt eats your ${CasinoRules.DiceStakeUsd}.";
			MaybeTriggerComp();
		}

		RefreshMoneyLines();
		UpdateWheelControls();
		UpdateDiceControls();
	}

	/// <summary>
	/// Pit-boss mercy timer: after enough consecutive busts (wheel busts and dice losses both
	/// count), the next wheel spin is comped so a cold streak never reads as a rage-quit wall.
	/// </summary>
	private void MaybeTriggerComp()
	{
		if (_compAvailable || _consecutiveBusts < CasinoRules.CompSpinBustThreshold)
			return;

		_compAvailable = true;
		_consecutiveBusts = 0;
		_lblComp.Text = $"Pit boss: \"Rough night, friend. Next spin's on the house.\" (free ${CasinoRules.CompSpinStakeUsd} spin)";
	}

	// --- Refresh helpers --------------------------------------------------------------------------

	private void RefreshMoneyLines()
	{
		_lblWallet.Text = $"Cash: ${CurrentMoney():N0}";
		_lblHouse.Text = _houseTakeUsd >= 0
			? $"House take tonight: ${_houseTakeUsd:N0}"
			: $"House take tonight: -${Math.Abs(_houseTakeUsd):N0} — they're watching you now.";
	}

	private void UpdateWheelControls()
	{
		var money = CurrentMoney();
		foreach (var btn in _stakeButtons)
			btn.Disabled = !_casinoOpen || _spinning || _compAvailable;

		_btnSpin.Text = _compAvailable
			? $"Free Spin (${CasinoRules.CompSpinStakeUsd} comp)"
			: $"Spin  (${_selectedStakeUsd})";
		_btnSpin.Disabled = !_casinoOpen || _spinning || (!_compAvailable && money < _selectedStakeUsd);
	}

	private void UpdateDiceControls()
	{
		_btnDice.Text = $"Roll  (${CasinoRules.DiceStakeUsd})";
		_btnDice.Disabled = !_casinoOpen || _spinning || CurrentMoney() < CasinoRules.DiceStakeUsd;
	}

	private void SetStatus(string text)
	{
		if (_lblStatus != null && GodotObject.IsInstanceValid(_lblStatus))
			_lblStatus.Text = text;
	}

	private static int CurrentMoney()
	{
		var app = App.Instance;
		return app != null && app.Services.TryGet<GameSession>(out var session) && session != null
			? session.GetMoneyUsd()
			: 0;
	}

	private static CityDefinition? ResolveCurrentCity()
	{
		var app = App.Instance;
		if (app == null) return null;
		if (!app.Services.TryGet<GameSession>(out var session) || session == null) return null;
		if (!app.Services.TryGet<DefDatabase>(out var defs) || defs == null) return null;
		return session.GetCurrentCityDef(defs);
	}

	private static IGameNavigator? Nav()
	{
		var app = App.Instance;
		return app != null && app.Services.TryGet<IGameNavigator>(out var nav) ? nav : null;
	}

	private static Texture2D? ResolveCityBackdrop()
	{
		var app = App.Instance;
		if (app == null) return null;
		if (!app.Services.TryGet<GameSession>(out var session) || session == null) return null;
		if (!app.Services.TryGet<DefDatabase>(out var defs) || defs == null) return null;
		return CityBackdropArt.Resolve(session.GetCurrentCityDef(defs), session.Save.Player.CurrentCityId);
	}

	/// <summary>
	/// Styled backdrop behind the drawer (soft vertical gradient + faint hairline grid, same
	/// command-console language as StoreView) so the area beside the panel never reads as dead black.
	/// </summary>
	private void BuildProceduralBackdrop()
	{
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(0.095f, 0.075f, 0.09f));
		gradient.SetColor(1, new Color(0.04f, 0.03f, 0.045f));
		var gradientRect = new TextureRect
		{
			Texture = new GradientTexture2D
			{
				Gradient = gradient,
				Width = 8,
				Height = 256,
				FillFrom = new Vector2(0f, 0f),
				FillTo = new Vector2(0f, 1f),
			},
			StretchMode = TextureRect.StretchModeEnum.Scale,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		AddChild(gradientRect);
		MoveChild(gradientRect, _bg.GetIndex() + 1);
		gradientRect.SetAnchorsPreset(LayoutPreset.FullRect);

		var gridRect = new TextureRect
		{
			Texture = CreateHairlineGridTexture(),
			StretchMode = TextureRect.StretchModeEnum.Tile,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		AddChild(gridRect);
		MoveChild(gridRect, gradientRect.GetIndex() + 1);
		gridRect.SetAnchorsPreset(LayoutPreset.FullRect);
	}

	private static ImageTexture CreateHairlineGridTexture()
	{
		const int cell = 48;
		var img = Image.CreateEmpty(cell, cell, false, Image.Format.Rgba8);
		img.Fill(new Color(0f, 0f, 0f, 0f));
		var line = new Color(1f, 1f, 1f, 0.045f);
		for (var x = 0; x < cell; x++) img.SetPixel(x, 0, line);
		for (var y = 0; y < cell; y++) img.SetPixel(0, y, line);
		return ImageTexture.CreateFromImage(img);
	}

	// --- Drawn controls ---------------------------------------------------------------------------

	/// <summary>
	/// Custom-drawn prize wheel. Wedge arcs come straight from <see cref="CasinoRules.WheelWedges"/>
	/// (the same table the roll uses), so the drawn odds are the real odds. The spin tweens the
	/// rotation with a cubic ease-out over ~2s and always parks the pointer inside the wedge that
	/// SessionWorld already paid out.
	/// </summary>
	private sealed partial class WheelControl : Control
	{
		private float _angle;
		private Tween? _tween;

		public void SpinToWedge(int wedgeIndex, Action onLanded)
		{
			var wedges = CasinoRules.WheelWedges;
			if (wedgeIndex < 0 || wedgeIndex >= wedges.Length)
			{
				onLanded();
				return;
			}

			var total = (float)CasinoRules.TotalWheelWeight;
			var start = 0f;
			for (var i = 0; i < wedgeIndex; i++)
				start += wedges[i].Weight / total * Mathf.Tau;
			var sweep = wedges[wedgeIndex].Weight / total * Mathf.Tau;

			// Land somewhere comfortably inside the wedge — presentation only; the outcome is
			// already settled in the save.
			var landLocal = start + sweep * (0.25f + 0.5f * Random.Shared.NextSingle());

			// The pointer sits at 12 o'clock (-PI/2). Solve landLocal + target = -PI/2 (mod TAU),
			// then add whole extra turns so the wheel always spins forward with some showmanship.
			var target = -Mathf.Pi * 0.5f - landLocal;
			while (target < _angle + Mathf.Tau)
				target += Mathf.Tau;
			target += Mathf.Tau * 3f;

			_tween?.Kill();
			_tween = CreateTween();
			_tween.TweenMethod(Callable.From<float>(SetAngle), _angle, target, 2.1)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			_tween.TweenCallback(Callable.From(onLanded));
		}

		private void SetAngle(float value)
		{
			_angle = value;
			QueueRedraw();
		}

		public override void _Draw()
		{
			var radius = Mathf.Min(Size.X, Size.Y) * 0.5f - 12f;
			if (radius <= 16f)
				return;
			var center = Size * 0.5f;

			// Outer housing behind the wedges.
			DrawCircle(center, radius + 8f, new Color(0.07f, 0.08f, 0.10f));

			var font = GameUiTheme.DisplayFont ?? GetThemeDefaultFont();
			var total = (float)CasinoRules.TotalWheelWeight;
			var start = _angle;
			for (var i = 0; i < CasinoRules.WheelWedges.Length; i++)
			{
				var wedge = CasinoRules.WheelWedges[i];
				var sweep = wedge.Weight / total * Mathf.Tau;
				DrawWedgePolygon(center, radius, start, sweep, WedgeColor(wedge.Multiplier, i));

				// Label wedges that are wide enough to carry text (the x5 sliver stays a
				// mysterious flash of gold — the section note explains it).
				if (sweep > 0.09f && font != null)
				{
					var mid = start + sweep * 0.5f;
					var text = wedge.Multiplier > 0 ? $"x{wedge.Multiplier}" : "BUST";
					var fontSize = wedge.Multiplier > 0 ? GameUiTheme.BaseFontSize : GameUiTheme.BaseFontSize - 5;
					var color = wedge.Multiplier > 0
						? new Color(0.95f, 0.93f, 0.85f)
						: new Color(1f, 1f, 1f, 0.45f);
					var textSize = font.GetStringSize(text, HorizontalAlignment.Left, -1f, fontSize);
					var pos = center
						+ new Vector2(Mathf.Cos(mid), Mathf.Sin(mid)) * radius * 0.66f
						- new Vector2(textSize.X * 0.5f, -textSize.Y * 0.25f);
					DrawString(font, pos, text, HorizontalAlignment.Left, -1f, fontSize, color);
				}

				start += sweep;
			}

			// Hub + rim trim.
			DrawCircle(center, radius * 0.15f, new Color(0.12f, 0.13f, 0.16f));
			DrawArc(center, radius * 0.15f, 0f, Mathf.Tau, 32, GameUiTheme.AccentGoldColor, 1.5f, true);
			DrawArc(center, radius + 4f, 0f, Mathf.Tau, 72, GameUiTheme.AccentGoldColor, 2f, true);

			// Fixed pointer at 12 o'clock.
			var pointer = new[]
			{
				new Vector2(center.X - 10f, center.Y - radius - 10f),
				new Vector2(center.X + 10f, center.Y - radius - 10f),
				new Vector2(center.X, center.Y - radius + 12f),
			};
			DrawColoredPolygon(pointer, GameUiTheme.AccentGoldColor);
		}

		private void DrawWedgePolygon(Vector2 center, float radius, float start, float sweep, Color color)
		{
			var steps = Mathf.Max(2, Mathf.CeilToInt(sweep / 0.07f));
			var points = new Vector2[steps + 2];
			points[0] = center;
			for (var s = 0; s <= steps; s++)
			{
				var a = start + sweep * s / steps;
				points[s + 1] = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
			}

			DrawColoredPolygon(points, color);
		}

		private static Color WedgeColor(int multiplier, int index) => multiplier switch
		{
			5 => new Color(0.72f, 0.56f, 0.16f),
			3 => new Color(0.48f, 0.38f, 0.13f),
			2 => new Color(0.11f, 0.30f, 0.36f),
			_ => (index & 1) == 0 ? new Color(0.30f, 0.10f, 0.10f) : new Color(0.24f, 0.08f, 0.09f),
		};
	}

	/// <summary>
	/// Drawn 2d6-vs-2d6 readout for Highway Dice: two labeled pairs (YOU / HOUSE) with classic pip
	/// faces. Faces render dark and blank until the first roll.
	/// </summary>
	private sealed partial class DicePairControl : Control
	{
		private const float DieSize = 46f;
		private const float DieSpacing = 56f;

		private int _p1 = 1, _p2 = 1, _h1 = 1, _h2 = 1;
		private bool _hasRoll;

		public void ShowRoll(int p1, int p2, int h1, int h2)
		{
			_p1 = p1;
			_p2 = p2;
			_h1 = h1;
			_h2 = h2;
			_hasRoll = true;
			QueueRedraw();
		}

		public override void _Draw()
		{
			var font = GameUiTheme.DisplayFont ?? GetThemeDefaultFont();
			var pairWidth = DieSize + DieSpacing;
			var totalWidth = pairWidth * 2f + 48f;
			var left = Mathf.Max(0f, (Size.X - totalWidth) * 0.5f);
			var top = 28f;

			DrawPair(font, "YOU", new Vector2(left, top), _p1, _p2, GameUiTheme.AccentCyanColor);
			DrawPair(font, "HOUSE", new Vector2(left + pairWidth + 48f, top), _h1, _h2, GameUiTheme.AccentGoldColor);
		}

		private void DrawPair(Font? font, string label, Vector2 origin, int a, int b, Color accent)
		{
			if (font != null)
				DrawString(font, origin + new Vector2(0f, -8f), label, HorizontalAlignment.Left, -1f, GameUiTheme.BaseFontSize - 2, accent);
			DrawDie(origin, a);
			DrawDie(origin + new Vector2(DieSpacing, 0f), b);
		}

		private void DrawDie(Vector2 pos, int value)
		{
			var rect = new Rect2(pos, new Vector2(DieSize, DieSize));
			var face = _hasRoll ? new Color(0.88f, 0.87f, 0.82f) : new Color(0.32f, 0.33f, 0.36f);
			DrawRect(rect, face);
			DrawRect(rect, new Color(0f, 0f, 0f, 0.55f), false, 2f);
			if (!_hasRoll)
				return;

			var pip = new Color(0.12f, 0.12f, 0.14f);
			foreach (var p in PipLayout(value))
				DrawCircle(pos + p * DieSize, 4f, pip);
		}

		private static Vector2[] PipLayout(int value) => value switch
		{
			1 => new[] { new Vector2(0.5f, 0.5f) },
			2 => new[] { new Vector2(0.28f, 0.28f), new Vector2(0.72f, 0.72f) },
			3 => new[] { new Vector2(0.25f, 0.25f), new Vector2(0.5f, 0.5f), new Vector2(0.75f, 0.75f) },
			4 => new[] { new Vector2(0.28f, 0.28f), new Vector2(0.72f, 0.28f), new Vector2(0.28f, 0.72f), new Vector2(0.72f, 0.72f) },
			5 => new[] { new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.25f), new Vector2(0.5f, 0.5f), new Vector2(0.25f, 0.75f), new Vector2(0.75f, 0.75f) },
			_ => new[] { new Vector2(0.28f, 0.22f), new Vector2(0.72f, 0.22f), new Vector2(0.28f, 0.5f), new Vector2(0.72f, 0.5f), new Vector2(0.28f, 0.78f), new Vector2(0.72f, 0.78f) },
		};
	}
}

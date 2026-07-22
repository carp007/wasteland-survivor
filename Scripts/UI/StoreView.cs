// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/UI/StoreView.cs
// Purpose: City parts store (master spec: each city has a Store). Buy/sell weapons, engines, and
//          targeting computers; the Workshop installs only owned parts. Category tabs filter one
//          scroll list; selecting a row opens a right-side detail pane that compares the item
//          against the installed/owned equivalent on the active vehicle.
// -------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GameUiKit.SceneBinding;
using GameUiKit.UI;
using WastelandSurvivor.Core.Defs;
using WastelandSurvivor.Core.IO;
using WastelandSurvivor.Game.Audio;
using WastelandSurvivor.Game.Navigation;
using WastelandSurvivor.Game.Session;

namespace WastelandSurvivor.Game.UI;

public partial class StoreView : Control
{
	private enum StoreTab
	{
		Vehicles,
		Weapons,
		Systems,
		Supplies,
	}

	private enum ItemKind
	{
		Vehicle,
		Weapon,
		Engine,
		Computer,
		Consumable,
	}

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

	[Bind("Panel/VBox/Tabs")]
	private HBoxContainer _tabs = null!;

	[Bind("Panel/VBox/ContentRow/StoreScroll")]
	private ScrollContainer _storeScroll = null!;

	[Bind("Panel/VBox/ContentRow/StoreScroll/StoreList")]
	private VBoxContainer _storeList = null!;

	[Bind("Panel/VBox/ContentRow/DetailPane")]
	private PanelContainer _detailPane = null!;

	[Bind("Panel/VBox/ContentRow/DetailPane/DetailScroll/DetailList")]
	private VBoxContainer _detailList = null!;

	[Bind("Panel/VBox/BottomRow/LblStatus")]
	private Label _lblStatus = null!;

	[Bind("Panel/VBox/BottomRow/BtnBack")]
	private Button _btnBack = null!;

	private Texture2D? _menuBackground;
	private readonly Dictionary<StoreTab, Button> _tabButtons = new();
	private readonly List<(PanelContainer panel, string key)> _rowPanels = new();
	private StoreTab _activeTab = StoreTab.Vehicles;
	private string? _selectedKey;

	// The compare pane needs more width than the shared city drawer offers; same margins, wider cap.
	private static MenuDrawerSpec StoreDrawerSpec => new(1240f, 720f, 0.86f, 24f, 84f, 24f, 24f);

	public override void _Ready()
	{
		GameUiTheme.ApplyToTree(this);
		SceneAutoBinder.Apply(this, nameof(StoreView));

		GameUiTheme.StyleHeading(_lblTitle, GameUiTheme.TitleFontSize + 12);
		_lblSubtitle.AddThemeColorOverride("font_color", GameUiTheme.TextMutedColor);
		_lblSubtitle.AddThemeFontSizeOverride("font_size", GameUiTheme.BaseFontSize - 1);
		_lblSubtitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_lblWallet.AddThemeColorOverride("font_color", GameUiTheme.AccentGoldColor);
		_lblStatus.AddThemeColorOverride("font_color", GameUiTheme.TextMutedColor);

		_panel.AddThemeStyleboxOverride("panel", GeneratedUiArt.CreatePremiumShellStyle());
		_detailPane.AddThemeStyleboxOverride("panel", GeneratedUiArt.CreatePremiumSectionStyle());
		GeneratedUiArt.ApplyIcon(_btnBack, "icon_back", 20);
		_btnBack.Pressed += () => Nav()?.ToCityShell(this);

		BuildTabs();
		BuildProceduralBackdrop();

		// Reuse the current city's backdrop so the store reads as part of this city and never
		// renders a black half-screen; modulated darker than the hub so the drawer stays readable.
		_menuBackground = ResolveCityBackdrop();
		_bg.Color = GameUiTheme.BackgroundColor;
		FullscreenTextureRectUtil.ConfigureCover(_bgImage);
		_bgImage.Texture = _menuBackground;
		_bgImage.Visible = _menuBackground != null;
		// Backdrop presence (play-test note 5): the old 0.30 dim buried the city art in near-black.
		// 0.52 keeps the drawer legible while DETROIT/skyline actually reads behind it.
		_bgImage.SelfModulate = new Color(0.52f, 0.54f, 0.58f);

		ApplyDrawer();
		GetViewport().SizeChanged += ApplyDrawer;

		Rebuild();
	}

	public override void _ExitTree()
	{
		var viewport = GetViewport();
		if (viewport != null)
			viewport.SizeChanged -= ApplyDrawer;
	}

	private void ApplyDrawer() => MenuDrawerLayout.Apply(_panel, this, StoreDrawerSpec);

	// --- Category tabs -----------------------------------------------------------------------------

	private void BuildTabs()
	{
		foreach (var (tab, title) in new[]
		{
			(StoreTab.Vehicles, "VEHICLES"),
			(StoreTab.Weapons, "WEAPONS"),
			(StoreTab.Systems, "SYSTEMS"),
			(StoreTab.Supplies, "SUPPLIES"),
		})
		{
			var btn = new Button
			{
				Text = title,
				ToggleMode = true,
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			};
			// Gold-underline tab language (same as MenuDrawerLayout.StyleTabs) hand-applied because
			// these are toggle buttons, not a TabContainer.
			btn.AddThemeStyleboxOverride("normal", CreateTabStyle(false));
			btn.AddThemeStyleboxOverride("hover", CreateTabStyle(false, hovered: true));
			btn.AddThemeStyleboxOverride("pressed", CreateTabStyle(true));
			btn.AddThemeStyleboxOverride("hover_pressed", CreateTabStyle(true));
			btn.AddThemeColorOverride("font_color", GameUiTheme.TextMutedColor);
			btn.AddThemeColorOverride("font_hover_color", GameUiTheme.TextColor);
			btn.AddThemeColorOverride("font_pressed_color", GameUiTheme.AccentGoldColor);
			btn.AddThemeColorOverride("font_hover_pressed_color", GameUiTheme.AccentGoldColor);
			btn.AddThemeFontSizeOverride("font_size", GameUiTheme.BaseFontSize + 1);

			var t = tab;
			btn.Toggled += pressed =>
			{
				if (pressed)
					OnTabSelected(t);
				else if (_activeTab == t)
					btn.SetPressedNoSignal(true); // the active tab cannot be untoggled
			};
			_tabs.AddChild(btn);
			_tabButtons[tab] = btn;
		}

		_tabButtons[_activeTab].SetPressedNoSignal(true);
	}

	private static StyleBoxFlat CreateTabStyle(bool selected, bool hovered = false)
	{
		var style = new StyleBoxFlat
		{
			BgColor = new Color(1f, 1f, 1f, selected ? 0.07f : hovered ? 0.06f : 0.03f),
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			ContentMarginLeft = 14,
			ContentMarginRight = 14,
			ContentMarginTop = 7,
			ContentMarginBottom = 7,
		};
		if (selected)
		{
			style.BorderColor = GameUiTheme.AccentGoldColor;
			style.BorderWidthBottom = 2;
		}
		return style;
	}

	private void OnTabSelected(StoreTab tab)
	{
		if (_activeTab == tab)
			return;

		_activeTab = tab;
		_selectedKey = null;
		foreach (var (t, b) in _tabButtons)
			b.SetPressedNoSignal(t == tab);
		Rebuild();
		_storeScroll.ScrollVertical = 0;
	}

	// --- Backdrop ---------------------------------------------------------------------------------

	/// <summary>
	/// Styled backdrop behind the drawer (soft vertical gradient + faint hairline grid in the
	/// command-console language) so the area beside the panel never reads as dead black — even on
	/// machines without the optional Assets/ art.
	/// </summary>
	private void BuildProceduralBackdrop()
	{
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(0.075f, 0.095f, 0.12f));
		gradient.SetColor(1, new Color(0.03f, 0.04f, 0.055f));
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

	private static IGameNavigator? Nav()
	{
		var app = App.Instance;
		return app != null && app.Services.TryGet<IGameNavigator>(out var nav) ? nav : null;
	}

	/// <summary>Same backdrop the city hub shows (art or procedural skyline); null only when the
	/// session/defs services are unavailable, in which case the plain themed background remains.</summary>
	private static Texture2D? ResolveCityBackdrop()
	{
		var app = App.Instance;
		if (app == null) return null;
		if (!app.Services.TryGet<GameSession>(out var session) || session == null) return null;
		if (!app.Services.TryGet<DefDatabase>(out var defs) || defs == null) return null;
		return CityBackdropArt.Resolve(session.GetCurrentCityDef(defs), session.Save.Player.CurrentCityId);
	}

	// --- List rebuild -------------------------------------------------------------------------------

	private void Rebuild()
	{
		var app = App.Instance;
		if (app == null) return;
		if (!app.Services.TryGet<GameSession>(out var session) || session == null) return;
		if (!app.Services.TryGet<DefDatabase>(out var defs) || defs == null) return;

		_lblWallet.Text = $"Cash: ${session.GetMoneyUsd():N0}   ·   Scrap: {session.Save.Player.Scrap}";

		_rowPanels.Clear();
		foreach (var child in _storeList.GetChildren())
			(child as Node)?.QueueFree();

		switch (_activeTab)
		{
			case StoreTab.Vehicles:
				AddSection("VEHICLES");
				foreach (var v in defs.Vehicles.Values.Where(v => v.PriceUsd > 0 && v.Class != VehicleClass.Trailer).OrderBy(v => v.PriceUsd))
					AddVehicleRow(session, defs, v);
				AddSection("TRAILERS");
				foreach (var t in defs.Vehicles.Values.Where(v => v.PriceUsd > 0 && v.Class == VehicleClass.Trailer).OrderBy(v => v.PriceUsd))
					AddVehicleRow(session, defs, t);
				break;

			case StoreTab.Weapons:
				AddSection("WEAPONS");
				foreach (var w in defs.Weapons.Values.Where(w => w.PriceUsd > 0).OrderBy(w => w.PriceUsd))
					AddPartRow(session, defs, ItemKind.Weapon, w.Id, w.DisplayName, DescribeWeapon(w), w.PriceUsd, CreateIconTile(new PartGlyphIcon { Glyph = WeaponGlyph(w.WeaponType) }));
				break;

			case StoreTab.Systems:
				AddSection("ENGINES");
				foreach (var e in defs.Engines.Values.Where(e => e.PriceUsd > 0).OrderBy(e => e.PriceUsd))
					AddPartRow(session, defs, ItemKind.Engine, e.Id, e.DisplayName, DescribeEngine(e), e.PriceUsd, CreateIconTile(new PartGlyphIcon { Glyph = PartGlyph.Piston }));
				AddSection("TARGETING COMPUTERS");
				foreach (var c in defs.Computers.Values.Where(c => c.PriceUsd > 0).OrderBy(c => c.PriceUsd))
					AddPartRow(session, defs, ItemKind.Computer, c.Id, c.DisplayName, DescribeComputer(c), c.PriceUsd, CreateIconTile(new PartGlyphIcon { Glyph = PartGlyph.Chip }));
				break;

			case StoreTab.Supplies:
				AddSection("CONSUMABLES");
				AddSpareTireRow(session, defs);
				break;
		}

		// Tail spacer so the last row's border scrolls fully clear of the scroll-region edge.
		_storeList.AddChild(new Control
		{
			CustomMinimumSize = new Vector2(0, 24),
			MouseFilter = MouseFilterEnum.Ignore,
		});

		RebuildDetail();
	}

	// --- Human-readable stat lines (no raw enum/type strings in the storefront) ---

	private static string DescribeWeapon(WeaponDefinition w)
	{
		var parts = new List<string> { DescribeWeaponRole(w.WeaponType) };

		if (w.BaseDamage > 0f)
			parts.Add($"{w.BaseDamage:0} dmg");
		if (w.SplashRadius is > 0f)
			parts.Add($"{w.SplashRadius:0.#} m blast");

		if (w.CooldownMs > 0)
			parts.Add(DescribeRate(w));

		if (w.FireMode == FireMode.LockRequired)
			parts.Add("needs target lock");
		else if (w.FireMode == FireMode.LockOptional)
			parts.Add("lock-on capable");

		if (w.WeaponType == WeaponType.MG && w.AmmoTypeIds.Length > 0)
			parts.Add(DescribeAmmo(w.AmmoTypeIds[0]));

		if (w.MassKg > 0f)
			parts.Add($"{w.MassKg:0} kg");
		if (w.AllowedMountLocations.Length > 0)
			parts.Add($"{string.Join("/", w.AllowedMountLocations.Select(m => m.ToString().ToLowerInvariant()))}-mount only");

		return string.Join(" · ", parts);
	}

	private static bool IsDropper(WeaponType type)
		=> type is WeaponType.MineDropper or WeaponType.OilSlickDropper or WeaponType.SmokeScreenDropper;

	private static string DescribeRate(WeaponDefinition w)
	{
		if (w.CooldownMs <= 0)
			return "instant";
		var perSecond = 1000f / w.CooldownMs;
		if (IsDropper(w.WeaponType))
			return $"drops every {w.CooldownMs / 1000f:0.#} s";
		return perSecond >= 1f ? $"{perSecond:0.#} rounds/s" : $"1 shot per {w.CooldownMs / 1000f:0.#} s";
	}

	private static float RatePerSecond(WeaponDefinition w)
		=> w.CooldownMs > 0 ? 1000f / w.CooldownMs : 0f;

	private static string DescribeWeaponRole(WeaponType type) => type switch
	{
		WeaponType.MG => "Rapid-fire gun",
		WeaponType.Rocket => "Unguided rocket launcher",
		WeaponType.Missile => "Guided missile launcher",
		WeaponType.MineDropper => "Rear-deploy mine layer",
		WeaponType.OilSlickDropper => "Rear-deploy hazard — oil slick cuts chaser traction",
		WeaponType.SmokeScreenDropper => "Rear-deploy smoke — spoils shots and missile locks",
		_ => "Weapon",
	};

	private static string DescribeAmmo(string ammoId) => ammoId switch
	{
		"ammo_mg_9mm" => "9mm",
		"ammo_mg_50cal" => ".50 cal",
		"ammo_ac_20mm" => "20mm shells",
		_ => ammoId.Replace("ammo_", string.Empty).Replace('_', ' '),
	};

	private static string DescribeEngineKind(EngineDefinition e) => e.FuelType switch
	{
		FuelType.Electric => "Electric motor",
		FuelType.Diesel => "Diesel engine",
		_ => "Gas engine",
	};

	private static string DescribeEngine(EngineDefinition e)
	{
		var parts = new List<string> { DescribeEngineKind(e), $"{e.PowerKw:0} kW ({e.PowerKw * 1.341f:0} hp)" };
		if (e.TorqueNm is > 0f)
			parts.Add($"{e.TorqueNm:0} N·m");
		if (e.AllowedVehicleClasses.Length > 0)
			parts.Add($"fits {string.Join(" / ", e.AllowedVehicleClasses.Select(FormatVehicleClass))}");
		return string.Join(" · ", parts);
	}

	private static string FormatVehicleClass(VehicleClass c) => c switch
	{
		VehicleClass.Compact => "compacts",
		VehicleClass.Sedan => "sedans",
		VehicleClass.Sports => "sports cars",
		VehicleClass.LightTruck => "light trucks",
		VehicleClass.Trailer => "trailers",
		_ => c.ToString().ToLowerInvariant(),
	};

	private static string DescribeVehicle(VehicleDefinition v)
	{
		if (v.Class == VehicleClass.Trailer)
		{
			var tires = v.TireCount == 1 ? "1 tire" : $"{v.TireCount} tires";
			return $"Unpowered trailer · {v.BaseMassKg:0} kg empty · {v.StorageCapacityUnits} cargo units · {tires}"
				+ " · takes locational damage like any vehicle · hitch it to your active vehicle in the Garage";
		}

		var mounts = v.MountPoints?.Count ?? 0;
		var mountText = mounts == 1 ? "1 weapon mount" : $"{mounts} weapon mounts";
		// Armor type is a buying decision (ammo-vs-armor matchups), not trivia — the SUV's whole
		// pitch over the cheaper Light Truck is Composite plating + V8 eligibility.
		return $"{DescribeVehicleClass(v.Class)} · {v.ArmorType} armor · {v.BaseMassKg:0} kg chassis · {v.StorageCapacityUnits} cargo units"
			+ $" · {mountText} · {v.FuelCapacityUnits:0} L tank · sold bare — stock engine, no weapons";
	}

	private static string DescribeVehicleClass(VehicleClass c) => c switch
	{
		VehicleClass.Compact => "Compact",
		VehicleClass.Sedan => "Sedan",
		VehicleClass.Sports => "Sports car",
		VehicleClass.LightTruck => "Light truck",
		VehicleClass.Suv => "SUV",
		VehicleClass.HeavyTruck => "Heavy truck",
		VehicleClass.SemiTruck => "Semi tractor",
		VehicleClass.Trailer => "Trailer",
		_ => c.ToString(),
	};

	private static string DescribeComputer(TargetingComputerDefinition c)
	{
		var groups = c.MaxActiveWeaponGroups == 1 ? "1 weapon group" : $"{c.MaxActiveWeaponGroups} weapon groups";
		var aim = c.AutoAimSlots <= 0
			? "no auto-aim"
			: c.AutoAimSlots == 1 ? "1 auto-aim slot" : $"{c.AutoAimSlots} auto-aim slots";
		return $"Fire control · runs {groups} · {aim} · locks targets out to {c.LockRange:0} m";
	}

	// --- Rows ---------------------------------------------------------------------------------------

	private void AddSection(string title)
	{
		var lbl = new Label { Text = title };
		GameUiTheme.StyleHeading(lbl, GameUiTheme.BaseFontSize, GameUiTheme.AccentCyanColor);
		lbl.AddThemeConstantOverride("line_spacing", 0);
		_storeList.AddChild(lbl);
	}

	private static string MakeKey(ItemKind kind, string id) => $"{(int)kind}:{id}";

	private static bool TryParseKey(string? key, out ItemKind kind, out string id)
	{
		kind = ItemKind.Weapon;
		id = "";
		if (string.IsNullOrEmpty(key))
			return false;
		var split = key.IndexOf(':');
		if (split <= 0 || !int.TryParse(key[..split], out var kindValue))
			return false;
		kind = (ItemKind)kindValue;
		id = key[(split + 1)..];
		return id.Length > 0;
	}

	/// <summary>Shared store-row scaffold: selectable inset panel row with icon tile + info column.</summary>
	private (HBoxContainer row, VBoxContainer info) AddRowScaffold(string key, Control iconTile)
	{
		var row = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		row.AddThemeStyleboxOverride("panel", GeneratedUiArt.CreatePremiumInsetStyle(selected: key == _selectedKey));
		row.GuiInput += ev =>
		{
			if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
				SelectRow(key);
		};
		_storeList.AddChild(row);
		_rowPanels.Add((row, key));

		var h = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Pass };
		h.AddThemeConstantOverride("separation", 12);
		row.AddChild(h);

		h.AddChild(iconTile);

		var info = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Pass,
		};
		h.AddChild(info);
		return (h, info);
	}

	private void SelectRow(string key)
	{
		if (_selectedKey == key)
			return;

		_selectedKey = key;
		UiSfx.Play("click");
		foreach (var (panel, k) in _rowPanels)
		{
			if (GodotObject.IsInstanceValid(panel))
				panel.AddThemeStyleboxOverride("panel", GeneratedUiArt.CreatePremiumInsetStyle(selected: k == key));
		}
		RebuildDetail();
	}

	private static void AddRowLabels(VBoxContainer info, string title, string detail)
	{
		var name = new Label { Text = title };
		if (GameUiTheme.DisplayFont is { } df) name.AddThemeFontOverride("font", df);
		name.AddThemeFontSizeOverride("font_size", GameUiTheme.BaseFontSize + 1);
		info.AddChild(name);

		var sub = new Label { Text = detail };
		sub.AddThemeColorOverride("font_color", GameUiTheme.TextMutedColor);
		sub.AddThemeFontSizeOverride("font_size", GameUiTheme.BaseFontSize - 2);
		sub.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		sub.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		info.AddChild(sub);
	}

	// --- Icon tiles ---------------------------------------------------------------------------------

	private static Control CreateIconTile(Control inner, float tileSize = 46f, float margin = 5f)
	{
		var tile = new PanelContainer
		{
			CustomMinimumSize = new Vector2(tileSize, tileSize),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		tile.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(1f, 1f, 1f, 0.045f),
			BorderColor = new Color(1f, 1f, 1f, 0.08f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 7,
			CornerRadiusTopRight = 7,
			CornerRadiusBottomLeft = 7,
			CornerRadiusBottomRight = 7,
			ContentMarginLeft = margin,
			ContentMarginRight = margin,
			ContentMarginTop = margin,
			ContentMarginBottom = margin,
		});
		inner.MouseFilter = MouseFilterEnum.Ignore;
		tile.AddChild(inner);
		return tile;
	}

	/// <summary>Swap-in for the async showroom thumbnail once the icon factory finishes baking it.</summary>
	private static void AttachPortraitIcon(Control iconHost, Texture2D tex)
	{
		var rect = new TextureRect
		{
			Texture = tex,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		iconHost.AddChild(rect);
		rect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	private async void PopulatePortraitIconAsync(Control iconHost, Control placeholder, DefDatabase defs, VehicleDefinition v)
	{
		try
		{
			var tex = await VehiclePortraitIconFactory.GetOrCreateAsync(this, defs, v, Game.Systems.VehiclePresentation.PlayerBodyColor);
			if (tex == null) return; // silhouette fallback stays
			if (!GodotObject.IsInstanceValid(iconHost)) return; // row was rebuilt mid-bake
			if (GodotObject.IsInstanceValid(placeholder))
				placeholder.QueueFree();
			AttachPortraitIcon(iconHost, tex);
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[StoreView] Portrait icon bake failed for '{v.Id}': {ex.Message}");
		}
	}

	/// <summary>Per-family drawn glyph for a weapon row (the generated SVG set only has two weapon
	/// icons, so every weapon row used to show the same forward-gun blob).</summary>
	private static PartGlyph WeaponGlyph(WeaponType type) => type switch
	{
		WeaponType.Rocket => PartGlyph.Rocket,
		WeaponType.Missile => PartGlyph.Missile,
		WeaponType.MineDropper => PartGlyph.MineDisc,
		WeaponType.OilSlickDropper or WeaponType.SmokeScreenDropper => PartGlyph.DropperCanister,
		_ => PartGlyph.MgBarrel,
	};

	private void AddPartRow(GameSession session, DefDatabase defs, ItemKind kind, string partId, string displayName, string detail, int price, Control iconTile)
	{
		var key = MakeKey(kind, partId);
		var (h, info) = AddRowScaffold(key, iconTile);

		var owned = session.GetOwnedPartCount(partId);
		AddRowLabels(info, owned > 0 ? $"{displayName}   (owned ×{owned})" : displayName, detail);

		var money = session.GetMoneyUsd();
		var btnBuy = new Button
		{
			Text = $"Buy  ${price:N0}",
			Disabled = money < price,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			CustomMinimumSize = new Vector2(132, 38),
		};
		btnBuy.Pressed += () =>
		{
			_selectedKey = key;
			if (session.TryBuyPart(defs, partId, out var err))
			{
				UiSfx.Play("purchase");
				SetStatus($"Bought {displayName}.");
			}
			else
			{
				UiSfx.Play("error");
				SetStatus(err);
			}
			Rebuild();
		};
		h.AddChild(btnBuy);

		var sellValue = SessionStore.GetPartSellValue(defs, partId);
		var btnSell = new Button
		{
			Text = $"Sell  ${sellValue:N0}",
			Disabled = owned <= 0,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			CustomMinimumSize = new Vector2(120, 38),
		};
		btnSell.Pressed += () =>
		{
			_selectedKey = key;
			if (session.TrySellPart(defs, partId, out var err))
			{
				UiSfx.Play("purchase");
				SetStatus($"Sold {displayName} for ${sellValue:N0}.");
			}
			else
			{
				UiSfx.Play("error");
				SetStatus(err);
			}
			Rebuild();
		};
		h.AddChild(btnSell);
	}

	/// <summary>
	/// Dealership row: whole vehicles sold as bare chassis (stock engine, no weapons) — the only
	/// way to buy a new ride; the Workshop outfits it from owned parts afterwards.
	/// </summary>
	private void AddVehicleRow(GameSession session, DefDatabase defs, VehicleDefinition v)
	{
		var key = MakeKey(ItemKind.Vehicle, v.Id);
		// Real showroom thumbnail (frozen 3D portrait frame) in a widescreen tile; the class
		// silhouette only shows while the first-ever render for this def is still baking.
		var iconHost = new Control
		{
			CustomMinimumSize = new Vector2(86f, 54f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		var cachedTex = VehiclePortraitIconFactory.GetCached(v.Id, Game.Systems.VehiclePresentation.PlayerBodyColor);
		if (cachedTex != null)
		{
			AttachPortraitIcon(iconHost, cachedTex);
		}
		else
		{
			var silhouette = new VehicleSilhouetteIcon { BodyClass = v.Class, TireCount = v.TireCount, MouseFilter = MouseFilterEnum.Ignore };
			iconHost.AddChild(silhouette);
			silhouette.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			PopulatePortraitIconAsync(iconHost, silhouette, defs, v);
		}
		var (h, info) = AddRowScaffold(key, CreateIconTile(iconHost, tileSize: 56f, margin: 4f));

		var owned = session.GetOwnedVehicles()
			.Count(x => string.Equals(x.DefinitionId, v.Id, StringComparison.OrdinalIgnoreCase));
		AddRowLabels(info, owned > 0 ? $"{v.DisplayName}   (owned ×{owned})" : v.DisplayName, DescribeVehicle(v));

		var price = v.PriceUsd;
		var btnBuy = new Button
		{
			Text = $"Buy  ${price:N0}",
			Disabled = session.GetMoneyUsd() < price,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			CustomMinimumSize = new Vector2(132, 38),
		};
		btnBuy.Pressed += () =>
		{
			_selectedKey = key;
			if (session.TryBuyVehicle(defs, v.Id, out var err))
			{
				UiSfx.Play("purchase");
				SetStatus(v.Class == VehicleClass.Trailer
					? $"Bought {v.DisplayName} — parked in your garage. Hitch it from the Garage fleet list."
					: $"Bought {v.DisplayName} — parked in your garage as a bare chassis.");
			}
			else
			{
				UiSfx.Play("error");
				SetStatus(err);
			}
			Rebuild();
		};
		h.AddChild(btnBuy);
	}

	/// <summary>
	/// Consumables: spare tire, bought straight into the active vehicle's cargo (the roadside
	/// tire swap in the arena is the only field repair, and vehicles only start with one).
	/// </summary>
	private void AddSpareTireRow(GameSession session, DefDatabase defs)
	{
		var key = MakeKey(ItemKind.Consumable, GameBalance.SpareTireCargoId);
		var (h, info) = AddRowScaffold(key, CreateIconTile(new PartGlyphIcon { Glyph = PartGlyph.Canister }));

		var vehicle = session.GetActiveVehicle();
		var carried = 0;
		if (vehicle?.CargoInventory != null)
			vehicle.CargoInventory.TryGetValue(GameBalance.SpareTireCargoId, out carried);

		var title = vehicle != null ? $"Spare Tire   (carried ×{carried})" : "Spare Tire";
		var detail = vehicle != null
			? "Roadside tire swap — stows in your active vehicle's cargo. Your only fix out on the road."
			: "Roadside tire swap — needs an active vehicle to stow it in.";
		AddRowLabels(info, title, detail);

		var price = GameBalance.SpareTireCostUsd;
		var btnBuy = new Button
		{
			Text = $"Buy  ${price:N0}",
			Disabled = vehicle == null || session.GetMoneyUsd() < price,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			CustomMinimumSize = new Vector2(132, 38),
		};
		btnBuy.Pressed += () =>
		{
			_selectedKey = key;
			if (session.TryBuySpareTireForActiveVehicle(defs, out var err))
			{
				UiSfx.Play("purchase");
				SetStatus("Bought Spare Tire — stowed in the active vehicle's cargo.");
			}
			else
			{
				UiSfx.Play("error");
				SetStatus(err);
			}
			Rebuild();
		};
		h.AddChild(btnBuy);
	}

	// --- Detail / compare pane ----------------------------------------------------------------------

	private void RebuildDetail()
	{
		foreach (var child in _detailList.GetChildren())
			(child as Node)?.QueueFree();

		var app = App.Instance;
		if (app == null) return;
		if (!app.Services.TryGet<GameSession>(out var session) || session == null) return;
		if (!app.Services.TryGet<DefDatabase>(out var defs) || defs == null) return;

		if (!TryParseKey(_selectedKey, out var kind, out var id))
		{
			BuildEmptyDetailState(session, defs);
			return;
		}

		switch (kind)
		{
			case ItemKind.Weapon when defs.Weapons.TryGetValue(id, out var w):
				BuildWeaponDetail(session, defs, w);
				break;
			case ItemKind.Engine when defs.Engines.TryGetValue(id, out var e):
				BuildEngineDetail(session, defs, e);
				break;
			case ItemKind.Computer when defs.Computers.TryGetValue(id, out var c):
				BuildComputerDetail(session, defs, c);
				break;
			case ItemKind.Vehicle when defs.Vehicles.TryGetValue(id, out var v):
				BuildVehicleDetail(session, defs, v);
				break;
			case ItemKind.Consumable:
				BuildSpareTireDetail(session);
				break;
			default:
				BuildEmptyDetailState(session, defs);
				break;
		}
	}

	/// <summary>
	/// Empty-state for the details pane (round-9 P1: pre-selection it rendered as a bare black
	/// column). Watermark glyph + city storefront identity + flavor copy fill the space until an
	/// item is selected.
	/// </summary>
	private void BuildEmptyDetailState(GameSession session, DefDatabase defs)
	{
		AddDetailHeading("DETAILS");

		var city = session.GetCurrentCityDef(defs);
		var cityName = string.IsNullOrWhiteSpace(city?.DisplayName) ? "Outpost" : city!.DisplayName;

		_detailList.AddChild(new Control { CustomMinimumSize = new Vector2(0f, 26f), MouseFilter = MouseFilterEnum.Ignore });

		// Dim watermark glyph, centered — keeps the column from reading as dead space.
		var markCenter = new CenterContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		markCenter.AddChild(new TextureRect
		{
			Texture = GeneratedUiArt.LoadSized("icon_workshop", 132),
			CustomMinimumSize = new Vector2(132f, 132f),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			SelfModulate = new Color(1f, 1f, 1f, 0.13f),
			MouseFilter = MouseFilterEnum.Ignore,
		});
		_detailList.AddChild(markCenter);

		_detailList.AddChild(new Control { CustomMinimumSize = new Vector2(0f, 14f), MouseFilter = MouseFilterEnum.Ignore });

		var kicker = new Label
		{
			Text = "PARTS & CHASSIS",
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		GameUiTheme.StyleHeading(kicker, GameUiTheme.BaseFontSize - 2, GameUiTheme.AccentGoldColor);
		_detailList.AddChild(kicker);

		var cityLbl = new Label
		{
			Text = cityName.ToUpperInvariant(),
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		GameUiTheme.StyleHeading(cityLbl, GameUiTheme.TitleFontSize + 4);
		_detailList.AddChild(cityLbl);

		_detailList.AddChild(new Control { CustomMinimumSize = new Vector2(0f, 14f), MouseFilter = MouseFilterEnum.Ignore });

		// Flavor copy in an inset card, with the original instruction as the closing line.
		var flavorShell = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		flavorShell.AddThemeStyleboxOverride("panel", GeneratedUiArt.CreatePremiumInsetStyle());
		_detailList.AddChild(flavorShell);

		var flavor = new Label
		{
			Text = "Every part on these shelves came off something that lost a fight — cleaned, torqued, and warrantied until the door.\n\nStock turns over with the salvage runs. The Workshop only installs what you own.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		flavor.AddThemeColorOverride("font_color", GameUiTheme.TextMutedColor);
		flavor.AddThemeFontSizeOverride("font_size", GameUiTheme.BaseFontSize - 2);
		flavorShell.AddChild(flavor);

		_detailList.AddChild(new Control { CustomMinimumSize = new Vector2(0f, 6f), MouseFilter = MouseFilterEnum.Ignore });
		AddDetailText("Select an item to inspect its full stats and compare it against what your active vehicle is running.");
	}

	private void AddDetailHeader(string name, string category)
	{
		var cat = new Label { Text = category };
		GameUiTheme.StyleHeading(cat, GameUiTheme.BaseFontSize - 3, GameUiTheme.AccentCyanColor);
		_detailList.AddChild(cat);

		var lbl = new Label { Text = name, AutowrapMode = TextServer.AutowrapMode.WordSmart };
		GameUiTheme.StyleHeading(lbl, GameUiTheme.TitleFontSize);
		_detailList.AddChild(lbl);
	}

	private void AddDetailHeading(string text)
	{
		var lbl = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
		GameUiTheme.StyleHeading(lbl, GameUiTheme.BaseFontSize - 2, GameUiTheme.AccentGoldColor);
		_detailList.AddChild(lbl);
	}

	private void AddDetailText(string text)
	{
		var lbl = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
		lbl.AddThemeColorOverride("font_color", GameUiTheme.TextMutedColor);
		lbl.AddThemeFontSizeOverride("font_size", GameUiTheme.BaseFontSize - 2);
		_detailList.AddChild(lbl);
	}

	private void AddStatRow(string label, string value) => AddValueRow(label, value, GameUiTheme.TextColor);

	private void AddValueRow(string label, string value, Color valueColor)
	{
		var h = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		var l = new Label { Text = label, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		l.AddThemeColorOverride("font_color", GameUiTheme.TextMutedColor);
		l.AddThemeFontSizeOverride("font_size", GameUiTheme.BaseFontSize - 2);
		h.AddChild(l);

		var v = new Label { Text = value, HorizontalAlignment = HorizontalAlignment.Right };
		v.AddThemeColorOverride("font_color", valueColor);
		v.AddThemeFontSizeOverride("font_size", GameUiTheme.BaseFontSize - 2);
		h.AddChild(v);
		_detailList.AddChild(h);
	}

	/// <summary>Delta row: "installed → candidate (±delta)", green when the change is an improvement.</summary>
	private void AddDeltaRow(string label, float candidate, float installed, string unit, bool higherIsBetter, string fmt = "0.#")
	{
		var delta = candidate - installed;
		var same = Mathf.Abs(delta) < 0.0001f;
		var color = same
			? GameUiTheme.TextMutedColor
			: (delta > 0f) == higherIsBetter ? GameUiTheme.SuccessColor : GameUiTheme.DangerColor;
		var text = same
			? $"{candidate.ToString(fmt)}{unit}  (same)"
			: $"{installed.ToString(fmt)} → {candidate.ToString(fmt)}{unit}  ({(delta > 0f ? "+" : "")}{delta.ToString(fmt)})";
		AddValueRow(label, text, color);
	}

	private void BuildWeaponDetail(GameSession session, DefDatabase defs, WeaponDefinition w)
	{
		AddDetailHeader(w.DisplayName, DescribeWeaponRole(w.WeaponType).ToUpperInvariant());
		AddStatRow("Price", $"${w.PriceUsd:N0}");
		AddStatRow("Sell value", $"${SessionStore.GetPartSellValue(defs, w.Id):N0}");
		AddStatRow("Owned", $"×{session.GetOwnedPartCount(w.Id)}");

		AddDetailHeading("STATS");
		if (w.BaseDamage > 0f) AddStatRow("Damage", $"{w.BaseDamage:0}");
		if (w.SplashRadius is > 0f) AddStatRow("Blast radius", $"{w.SplashRadius:0.#} m");
		if (w.CooldownMs > 0) AddStatRow(IsDropper(w.WeaponType) ? "Drop interval" : "Fire rate", DescribeRate(w));
		AddStatRow("Fire mode", w.FireMode switch
		{
			FireMode.LockRequired => "Needs target lock",
			FireMode.LockOptional => "Lock-on capable",
			_ => "Dumb-fire",
		});
		if (w.AmmoTypeIds.Length > 0) AddStatRow("Ammo", DescribeAmmo(w.AmmoTypeIds[0]));
		if (w.MassKg > 0f) AddStatRow("Mass", $"{w.MassKg:0} kg");
		AddStatRow("Mounts", w.AllowedMountLocations.Length == 0
			? "Any"
			: string.Join(" / ", w.AllowedMountLocations));

		var equivalent = FindInstalledWeaponEquivalent(session, defs, w);
		if (equivalent == null)
		{
			AddDetailHeading("COMPARE");
			AddDetailText(session.GetActiveVehicle() == null
				? "No active vehicle to compare against."
				: "Nothing comparable installed on your active vehicle's compatible mounts.");
			return;
		}

		var (installed, mountId) = equivalent.Value;
		AddDetailHeading($"VS INSTALLED — {installed.DisplayName.ToUpperInvariant()}");
		AddDetailText($"On your active vehicle's {mountId} mount.");
		AddDeltaRow("Damage", w.BaseDamage, installed.BaseDamage, "", higherIsBetter: true, "0");
		AddDeltaRow(IsDropper(w.WeaponType) ? "Drops/s" : "Rounds/s", RatePerSecond(w), RatePerSecond(installed), "", higherIsBetter: true);
		AddDeltaRow("Mass", w.MassKg, installed.MassKg, " kg", higherIsBetter: false, "0");
		AddDeltaRow("Price ($)", w.PriceUsd, installed.PriceUsd, "", higherIsBetter: false, "N0");
	}

	/// <summary>
	/// "Installed equivalent" for a weapon: among weapons on the active vehicle whose mount the
	/// candidate is allowed to occupy, prefer the same weapon family, then the strongest occupant.
	/// </summary>
	private static (WeaponDefinition def, string mountId)? FindInstalledWeaponEquivalent(GameSession session, DefDatabase defs, WeaponDefinition candidate)
	{
		var vehicle = session.GetActiveVehicle();
		if (vehicle == null)
			return null;
		defs.Vehicles.TryGetValue(vehicle.DefinitionId, out var vDef);

		(WeaponDefinition def, string mountId)? best = null;
		var bestScore = float.MinValue;
		foreach (var (mountId, installed) in vehicle.InstalledWeaponsByMountId)
		{
			if (installed == null || !defs.Weapons.TryGetValue(installed.WeaponId, out var iDef))
				continue;

			var mount = vDef?.MountPoints?.FirstOrDefault(m => string.Equals(m.MountId, mountId, StringComparison.OrdinalIgnoreCase));
			var location = mount?.MountLocation ?? MountLocation.Front;
			if (!candidate.IsMountLocationAllowed(location))
				continue;

			var score = (iDef.WeaponType == candidate.WeaponType ? 1000f : 0f) + iDef.BaseDamage;
			if (score > bestScore)
			{
				bestScore = score;
				best = (iDef, mountId);
			}
		}
		return best;
	}

	private void BuildEngineDetail(GameSession session, DefDatabase defs, EngineDefinition e)
	{
		AddDetailHeader(e.DisplayName, DescribeEngineKind(e).ToUpperInvariant());
		AddStatRow("Price", $"${e.PriceUsd:N0}");
		AddStatRow("Sell value", $"${SessionStore.GetPartSellValue(defs, e.Id):N0}");
		AddStatRow("Owned", $"×{session.GetOwnedPartCount(e.Id)}");

		AddDetailHeading("STATS");
		AddStatRow("Power", $"{e.PowerKw:0} kW ({e.PowerKw * 1.341f:0} hp)");
		if (e.TorqueNm is > 0f) AddStatRow("Torque", $"{e.TorqueNm:0} N·m");
		AddStatRow("Efficiency", $"{e.Efficiency:0.##}×");
		if (e.AllowedVehicleClasses.Length > 0)
			AddStatRow("Fits", string.Join(", ", e.AllowedVehicleClasses.Select(DescribeVehicleClass)));

		var vehicle = session.GetActiveVehicle();
		var installedId = vehicle?.InstalledEngineId;
		if (vehicle == null || string.IsNullOrEmpty(installedId) || !defs.Engines.TryGetValue(installedId, out var installed))
		{
			AddDetailHeading("COMPARE");
			AddDetailText(vehicle == null
				? "No active vehicle to compare against."
				: "Your active vehicle is running its stock engine — no installed engine to compare.");
			return;
		}

		if (string.Equals(installed.Id, e.Id, StringComparison.OrdinalIgnoreCase))
		{
			AddDetailHeading("COMPARE");
			AddDetailText("This engine is already installed on your active vehicle.");
			return;
		}

		AddDetailHeading($"VS INSTALLED — {installed.DisplayName.ToUpperInvariant()}");
		AddDeltaRow("Power (kW)", e.PowerKw, installed.PowerKw, "", higherIsBetter: true, "0");
		if (e.TorqueNm is > 0f || installed.TorqueNm is > 0f)
			AddDeltaRow("Torque (N·m)", e.TorqueNm ?? 0f, installed.TorqueNm ?? 0f, "", higherIsBetter: true, "0");
		AddDeltaRow("Efficiency", e.Efficiency, installed.Efficiency, "×", higherIsBetter: true, "0.##");
		AddDeltaRow("Price ($)", e.PriceUsd, installed.PriceUsd, "", higherIsBetter: false, "N0");
	}

	private void BuildComputerDetail(GameSession session, DefDatabase defs, TargetingComputerDefinition c)
	{
		AddDetailHeader(c.DisplayName, "TARGETING COMPUTER");
		AddStatRow("Price", $"${c.PriceUsd:N0}");
		AddStatRow("Sell value", $"${SessionStore.GetPartSellValue(defs, c.Id):N0}");
		AddStatRow("Owned", $"×{session.GetOwnedPartCount(c.Id)}");

		AddDetailHeading("STATS");
		AddStatRow("Weapon groups", $"{c.MaxActiveWeaponGroups}");
		AddStatRow("Auto-aim slots", c.AutoAimSlots <= 0 ? "None" : $"{c.AutoAimSlots}");
		AddStatRow("Lock range", $"{c.LockRange:0} m");

		var vehicle = session.GetActiveVehicle();
		var installedId = vehicle?.InstalledComputerId;
		if (vehicle == null || string.IsNullOrEmpty(installedId) || !defs.Computers.TryGetValue(installedId, out var installed))
		{
			AddDetailHeading("COMPARE");
			AddDetailText(vehicle == null
				? "No active vehicle to compare against."
				: "No targeting computer installed on your active vehicle.");
			return;
		}

		if (string.Equals(installed.Id, c.Id, StringComparison.OrdinalIgnoreCase))
		{
			AddDetailHeading("COMPARE");
			AddDetailText("This computer is already installed on your active vehicle.");
			return;
		}

		AddDetailHeading($"VS INSTALLED — {installed.DisplayName.ToUpperInvariant()}");
		AddDeltaRow("Weapon groups", c.MaxActiveWeaponGroups, installed.MaxActiveWeaponGroups, "", higherIsBetter: true, "0");
		AddDeltaRow("Auto-aim slots", c.AutoAimSlots, installed.AutoAimSlots, "", higherIsBetter: true, "0");
		AddDeltaRow("Lock range", c.LockRange, installed.LockRange, " m", higherIsBetter: true, "0");
		AddDeltaRow("Price ($)", c.PriceUsd, installed.PriceUsd, "", higherIsBetter: false, "N0");
	}

	private void BuildVehicleDetail(GameSession session, DefDatabase defs, VehicleDefinition v)
	{
		AddDetailHeader(v.DisplayName, DescribeVehicleClass(v.Class).ToUpperInvariant());
		AddVehicleDetailPortrait(defs, v);
		AddStatRow("Price", $"${v.PriceUsd:N0}");
		var owned = session.GetOwnedVehicles()
			.Count(x => string.Equals(x.DefinitionId, v.Id, StringComparison.OrdinalIgnoreCase));
		AddStatRow("Owned", $"×{owned}");

		AddDetailHeading("STATS");
		AddStatRow("Chassis mass", $"{v.BaseMassKg:0} kg");
		AddStatRow("Cargo", $"{v.StorageCapacityUnits} units");
		if (v.Class != VehicleClass.Trailer)
		{
			AddStatRow("Weapon mounts", $"{v.MountPoints?.Count ?? 0}");
			AddStatRow("Fuel tank", $"{v.FuelCapacityUnits:0} L");
		}
		AddStatRow("Tires", $"{v.TireCount}");
		AddStatRow("Armor (total)", $"{TotalArmor(v)}");
		AddStatRow("Structure (total)", $"{TotalHp(v)} hp");
		AddStatRow("Armor type", v.ArmorType.ToString());

		var (compareDef, compareLabel, missingText) = ResolveVehicleCompareTarget(session, defs, v);
		if (compareDef == null)
		{
			AddDetailHeading("COMPARE");
			AddDetailText(missingText);
			return;
		}

		if (string.Equals(compareDef.Id, v.Id, StringComparison.OrdinalIgnoreCase))
		{
			AddDetailHeading("COMPARE");
			AddDetailText(v.Class == VehicleClass.Trailer
				? "Same model as the trailer on your hitch."
				: "Same model as your active vehicle.");
			return;
		}

		AddDetailHeading($"{compareLabel} — {compareDef.DisplayName.ToUpperInvariant()}");
		AddDeltaRow("Mass (kg)", v.BaseMassKg, compareDef.BaseMassKg, "", higherIsBetter: false, "0");
		AddDeltaRow("Cargo", v.StorageCapacityUnits, compareDef.StorageCapacityUnits, "", higherIsBetter: true, "0");
		if (v.Class != VehicleClass.Trailer)
		{
			AddDeltaRow("Mounts", v.MountPoints?.Count ?? 0, compareDef.MountPoints?.Count ?? 0, "", higherIsBetter: true, "0");
			AddDeltaRow("Fuel (L)", v.FuelCapacityUnits, compareDef.FuelCapacityUnits, "", higherIsBetter: true, "0");
		}
		AddDeltaRow("Armor", TotalArmor(v), TotalArmor(compareDef), "", higherIsBetter: true, "0");
		AddDeltaRow("Structure", TotalHp(v), TotalHp(compareDef), "", higherIsBetter: true, "0");
	}

	/// <summary>
	/// Dealership turntable: the same live 3D portrait (pedestal rig included) the garage hero
	/// card uses, fed a factory-fresh preview instance of the def — the old tilted-capsule glyph
	/// plus dead space read as a placeholder (round 10 P2-10). The vector silhouette in the list
	/// rows stays; here the buyer gets the real showroom shot. Falls back to a centered class
	/// silhouette if the pawn fails to build.
	/// </summary>
	private void AddVehicleDetailPortrait(DefDatabase defs, VehicleDefinition v)
	{
		var shell = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0f, 186f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		shell.AddThemeStyleboxOverride("panel", GeneratedUiArt.CreatePremiumInsetStyle());
		_detailList.AddChild(shell);

		// Factory-fresh preview state: healthy tires/sections, no weapons (chassis sell bare).
		var preview = VehiclePortraitIconFactory.CreateFactoryFreshInstance(v);

		var portrait = new VehiclePortraitViewport
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		shell.AddChild(portrait);
		portrait.ShowVehicle(defs, v, preview, Game.Systems.VehiclePresentation.PlayerBodyColor);

		if (!portrait.HasPortrait)
		{
			portrait.QueueFree();
			var center = new CenterContainer
			{
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				SizeFlagsVertical = SizeFlags.ExpandFill,
				MouseFilter = MouseFilterEnum.Ignore,
			};
			var silhouette = new VehicleSilhouetteIcon
			{
				BodyClass = v.Class,
				TireCount = v.TireCount,
				CustomMinimumSize = new Vector2(120f, 120f),
				MouseFilter = MouseFilterEnum.Ignore,
			};
			center.AddChild(silhouette);
			shell.AddChild(center);
		}

		_detailList.AddChild(new Control { CustomMinimumSize = new Vector2(0f, 4f), MouseFilter = MouseFilterEnum.Ignore });
	}

	/// <summary>
	/// Debug/harness hook: select the first vehicle row so capture runs can film the dealership
	/// detail portrait without simulated mouse input.
	/// </summary>
	public void DebugSelectFirstVehicle()
	{
		foreach (var (_, key) in _rowPanels)
		{
			if (TryParseKey(key, out var kind, out _) && kind == ItemKind.Vehicle)
			{
				SelectRow(key);
				return;
			}
		}
	}

	/// <summary>
	/// "Owned equivalent" for a chassis: vehicles compare against the ACTIVE vehicle's definition;
	/// trailers compare against whatever trailer is currently on the active vehicle's hitch.
	/// </summary>
	private static (VehicleDefinition? def, string label, string missingText) ResolveVehicleCompareTarget(GameSession session, DefDatabase defs, VehicleDefinition candidate)
	{
		var active = session.GetActiveVehicle();
		if (active == null)
			return (null, "", "No active vehicle to compare against.");

		if (candidate.Class == VehicleClass.Trailer)
		{
			var hitchedId = active.HitchedTrailerInstanceId;
			var hitched = string.IsNullOrEmpty(hitchedId)
				? null
				: session.GetOwnedVehicles().FirstOrDefault(x => string.Equals(x.InstanceId, hitchedId, StringComparison.OrdinalIgnoreCase));
			if (hitched == null || !defs.Vehicles.TryGetValue(hitched.DefinitionId, out var hitchedDef))
				return (null, "", "No trailer on your active vehicle's hitch to compare against.");
			return (hitchedDef, "VS HITCHED", "");
		}

		if (!defs.Vehicles.TryGetValue(active.DefinitionId, out var activeDef))
			return (null, "", "No active vehicle to compare against.");
		return (activeDef, "VS ACTIVE", "");
	}

	private static int TotalArmor(VehicleDefinition v)
		=> v.BaseArmorBySection.Values.Sum() + v.BaseTireArmor * v.TireCount;

	private static int TotalHp(VehicleDefinition v)
		=> v.BaseHpBySection.Values.Sum() + v.BaseTireHp * v.TireCount;

	private void BuildSpareTireDetail(GameSession session)
	{
		AddDetailHeader("Spare Tire", "CONSUMABLE");
		AddStatRow("Price", $"${GameBalance.SpareTireCostUsd:N0}");

		var vehicle = session.GetActiveVehicle();
		var carried = 0;
		if (vehicle?.CargoInventory != null)
			vehicle.CargoInventory.TryGetValue(GameBalance.SpareTireCargoId, out carried);
		AddStatRow("Carried", vehicle == null ? "No active vehicle" : $"×{carried}");

		AddDetailHeading("NOTES");
		AddDetailText("Stows in the active vehicle's cargo. The roadside tire swap is your only field repair — carry more than the one your vehicle shipped with.");
	}

	private void SetStatus(string text)
	{
		if (_lblStatus != null && GodotObject.IsInstanceValid(_lblStatus))
			_lblStatus.Text = text;
	}

	// --- Vehicle silhouette icon ----------------------------------------------------------------------

	/// <summary>
	/// Top-down per-class chassis silhouette for dealership rows (also reused as the Garage's
	/// empty-bay watermark). Each class gets its own hand-drawn shape — short rounded compact,
	/// three-box sedan, pointed sports wedge, cab + open-bed truck, drawbar trailer — because the
	/// previous single parametric outline collapsed into one identical blob at tile size.
	/// </summary>
	internal partial class VehicleSilhouetteIcon : Control
	{
		public VehicleClass BodyClass { get; set; } = VehicleClass.Compact;

		/// <summary>Axle cue for trailers: 2-tire utility vs 4-tire tandem cargo.</summary>
		public int TireCount { get; set; } = 4;

		private static readonly Color BodyColor = new(0.66f, 0.72f, 0.78f);
		private static readonly Color BodyEdge = new(0.85f, 0.89f, 0.93f);
		private static readonly Color BodyShade = new(0.40f, 0.45f, 0.51f);
		private static readonly Color WheelColor = new(0.07f, 0.08f, 0.10f);
		private static readonly Color WheelRim = new(1f, 1f, 1f, 0.30f);
		private static readonly Color GlassColor = new(0.45f, 0.78f, 1.0f, 0.80f);

		public VehicleSilhouetteIcon()
		{
			MouseFilter = MouseFilterEnum.Ignore;
			CustomMinimumSize = new Vector2(34f, 34f);
		}

		public override void _Draw()
		{
			var s = Mathf.Min(Size.X, Size.Y);
			if (s < 14f)
				return;

			var cx = Size.X * 0.5f;
			var oy = (Size.Y - s) * 0.5f;
			switch (BodyClass)
			{
				case VehicleClass.Trailer: DrawTrailer(cx, oy, s); break;
				case VehicleClass.Sports: DrawSports(cx, oy, s); break;
				case VehicleClass.Sedan: DrawSedan(cx, oy, s); break;
				case VehicleClass.LightTruck: DrawTruck(cx, oy, s); break;
				case VehicleClass.Suv: DrawSuv(cx, oy, s); break;
				case VehicleClass.HeavyTruck: DrawHeavyTruck(cx, oy, s); break;
				case VehicleClass.SemiTruck: DrawSemiTruck(cx, oy, s); break;
				default: DrawCompact(cx, oy, s); break;
			}
		}

		/// <summary>Tire block poking out past the body edge, faint rim so it reads on the dark tile.</summary>
		private void DrawWheel(float x, float y, float w, float h)
		{
			var rect = new Rect2(x - w * 0.5f, y - h * 0.5f, w, h);
			DrawRect(rect, WheelColor, true);
			DrawRect(rect, WheelRim, false, 1f);
		}

		private void DrawBody(Vector2[] pts)
		{
			DrawColoredPolygon(pts, BodyColor);
			for (var i = 0; i < pts.Length; i++)
				DrawLine(pts[i], pts[(i + 1) % pts.Length], BodyEdge, 1.2f);
		}

		private void DrawGlass(Rect2 rect)
		{
			DrawRect(rect, WheelColor, true);
			DrawRect(rect.GrowIndividual(-1.5f, -1.5f, -1.5f, -1.5f), GlassColor, true);
		}

		/// <summary>Compact: short, wide, rounded one-box body — deliberate empty tile above and below.</summary>
		private void DrawCompact(float cx, float oy, float s)
		{
			var top = oy + s * 0.19f;
			var bot = oy + s * 0.81f;
			var halfW = s * 0.29f;
			var c = s * 0.11f;

			DrawWheel(cx - halfW, oy + s * 0.33f, s * 0.13f, s * 0.15f);
			DrawWheel(cx + halfW, oy + s * 0.33f, s * 0.13f, s * 0.15f);
			DrawWheel(cx - halfW, oy + s * 0.67f, s * 0.13f, s * 0.15f);
			DrawWheel(cx + halfW, oy + s * 0.67f, s * 0.13f, s * 0.15f);

			DrawBody(new[]
			{
				new Vector2(cx - halfW + c, top),
				new Vector2(cx + halfW - c, top),
				new Vector2(cx + halfW, top + c),
				new Vector2(cx + halfW, bot - c),
				new Vector2(cx + halfW - c, bot),
				new Vector2(cx - halfW + c, bot),
				new Vector2(cx - halfW, bot - c),
				new Vector2(cx - halfW, top + c),
			});

			// One-box hatch: the cabin glass dominates the little footprint.
			DrawGlass(new Rect2(cx - s * 0.185f, oy + s * 0.345f, s * 0.37f, s * 0.26f));
		}

		/// <summary>Sedan: full-length three-box body — hood seam, long glass cabin split by a roof band, trunk seam.</summary>
		private void DrawSedan(float cx, float oy, float s)
		{
			var top = oy + s * 0.05f;
			var bot = oy + s * 0.95f;
			var halfW = s * 0.26f;

			DrawWheel(cx - halfW, oy + s * 0.22f, s * 0.12f, s * 0.15f);
			DrawWheel(cx + halfW, oy + s * 0.22f, s * 0.12f, s * 0.15f);
			DrawWheel(cx - halfW, oy + s * 0.78f, s * 0.12f, s * 0.15f);
			DrawWheel(cx + halfW, oy + s * 0.78f, s * 0.12f, s * 0.15f);

			DrawBody(new[]
			{
				new Vector2(cx - halfW * 0.70f, top),
				new Vector2(cx + halfW * 0.70f, top),
				new Vector2(cx + halfW, top + s * 0.13f),
				new Vector2(cx + halfW, bot - s * 0.10f),
				new Vector2(cx + halfW * 0.76f, bot),
				new Vector2(cx - halfW * 0.76f, bot),
				new Vector2(cx - halfW, bot - s * 0.10f),
				new Vector2(cx - halfW, top + s * 0.13f),
			});

			// Long cabin: windshield + rear glass with a body-colored roof band between them.
			DrawGlass(new Rect2(cx - s * 0.185f, oy + s * 0.30f, s * 0.37f, s * 0.36f));
			DrawRect(new Rect2(cx - s * 0.185f, oy + s * 0.42f, s * 0.37f, s * 0.12f), BodyColor, true);
			// Hood + trunk seams sell the three-box read.
			DrawLine(new Vector2(cx - halfW * 0.86f, oy + s * 0.165f), new Vector2(cx + halfW * 0.86f, oy + s * 0.165f), BodyShade, 1.2f);
			DrawLine(new Vector2(cx - halfW * 0.86f, oy + s * 0.825f), new Vector2(cx + halfW * 0.86f, oy + s * 0.825f), BodyShade, 1.2f);
		}

		/// <summary>Sports: narrow wedge — pointed nose, hood vents, cockpit set far back, fat rear tires, tail wing.</summary>
		private void DrawSports(float cx, float oy, float s)
		{
			var top = oy + s * 0.04f;
			var bot = oy + s * 0.90f;
			var halfW = s * 0.235f;

			DrawWheel(cx - halfW * 0.74f, oy + s * 0.30f, s * 0.11f, s * 0.14f);
			DrawWheel(cx + halfW * 0.74f, oy + s * 0.30f, s * 0.11f, s * 0.14f);
			DrawWheel(cx - halfW, oy + s * 0.72f, s * 0.14f, s * 0.16f);
			DrawWheel(cx + halfW, oy + s * 0.72f, s * 0.14f, s * 0.16f);

			DrawBody(new[]
			{
				new Vector2(cx, top), // pointed nose
				new Vector2(cx + halfW * 0.55f, oy + s * 0.34f),
				new Vector2(cx + halfW, oy + s * 0.56f),
				new Vector2(cx + halfW, bot - s * 0.04f),
				new Vector2(cx + halfW * 0.62f, bot),
				new Vector2(cx - halfW * 0.62f, bot),
				new Vector2(cx - halfW, bot - s * 0.04f),
				new Vector2(cx - halfW, oy + s * 0.56f),
				new Vector2(cx - halfW * 0.55f, oy + s * 0.34f),
			});

			// Cockpit sits far back; vents run up the long nose.
			DrawGlass(new Rect2(cx - s * 0.11f, oy + s * 0.48f, s * 0.22f, s * 0.20f));
			DrawLine(new Vector2(cx - s * 0.045f, oy + s * 0.17f), new Vector2(cx - s * 0.045f, oy + s * 0.33f), BodyShade, 1.4f);
			DrawLine(new Vector2(cx + s * 0.045f, oy + s * 0.17f), new Vector2(cx + s * 0.045f, oy + s * 0.33f), BodyShade, 1.4f);
			// Rear wing spans wider than the tail.
			DrawRect(new Rect2(cx - halfW * 1.18f, oy + s * 0.92f, halfW * 2.36f, s * 0.06f), BodyShade, true);
			DrawRect(new Rect2(cx - halfW * 1.18f, oy + s * 0.92f, halfW * 2.36f, s * 0.06f), WheelRim, false, 1f);
		}

		/// <summary>Light truck: hood + glass cab up front, long open cargo bed (dark hollow with rails) behind.</summary>
		private void DrawTruck(float cx, float oy, float s)
		{
			var top = oy + s * 0.06f;
			var bot = oy + s * 0.96f;
			var halfW = s * 0.295f;

			DrawWheel(cx - halfW, oy + s * 0.24f, s * 0.14f, s * 0.17f);
			DrawWheel(cx + halfW, oy + s * 0.24f, s * 0.14f, s * 0.17f);
			DrawWheel(cx - halfW, oy + s * 0.76f, s * 0.14f, s * 0.17f);
			DrawWheel(cx + halfW, oy + s * 0.76f, s * 0.14f, s * 0.17f);

			DrawBody(new[]
			{
				new Vector2(cx - halfW * 0.84f, top),
				new Vector2(cx + halfW * 0.84f, top),
				new Vector2(cx + halfW, top + s * 0.09f),
				new Vector2(cx + halfW, bot), // squared-off tail
				new Vector2(cx - halfW, bot),
				new Vector2(cx - halfW, top + s * 0.09f),
			});

			// Cab windshield band up front...
			DrawGlass(new Rect2(cx - s * 0.21f, oy + s * 0.27f, s * 0.42f, s * 0.14f));
			// ...then the open bed: a dark hollow inside light rails is the truck's signature from above.
			var bed = new Rect2(cx - halfW + s * 0.045f, oy + s * 0.48f, halfW * 2f - s * 0.09f, s * 0.42f);
			DrawRect(bed, WheelColor, true);
			DrawRect(bed, BodyEdge, false, 1.2f);
			DrawLine(new Vector2(bed.Position.X + 2f, bed.Position.Y + bed.Size.Y * 0.34f), new Vector2(bed.End.X - 2f, bed.Position.Y + bed.Size.Y * 0.34f), BodyShade, 1f);
			DrawLine(new Vector2(bed.Position.X + 2f, bed.Position.Y + bed.Size.Y * 0.67f), new Vector2(bed.End.X - 2f, bed.Position.Y + bed.Size.Y * 0.67f), BodyShade, 1f);
		}

		/// <summary>SUV: tall two-box wagon — short hood, long cabin greenhouse running almost to the tail.</summary>
		private void DrawSuv(float cx, float oy, float s)
		{
			var top = oy + s * 0.06f;
			var bot = oy + s * 0.94f;
			var halfW = s * 0.285f;

			DrawWheel(cx - halfW, oy + s * 0.24f, s * 0.135f, s * 0.165f);
			DrawWheel(cx + halfW, oy + s * 0.24f, s * 0.135f, s * 0.165f);
			DrawWheel(cx - halfW, oy + s * 0.76f, s * 0.135f, s * 0.165f);
			DrawWheel(cx + halfW, oy + s * 0.76f, s * 0.135f, s * 0.165f);

			DrawBody(new[]
			{
				new Vector2(cx - halfW * 0.80f, top),
				new Vector2(cx + halfW * 0.80f, top),
				new Vector2(cx + halfW, top + s * 0.10f),
				new Vector2(cx + halfW, bot - s * 0.06f),
				new Vector2(cx + halfW * 0.82f, bot),
				new Vector2(cx - halfW * 0.82f, bot),
				new Vector2(cx - halfW, bot - s * 0.06f),
				new Vector2(cx - halfW, top + s * 0.10f),
			});

			// Short hood, then one long greenhouse with roof-rack rails to the tail.
			DrawGlass(new Rect2(cx - s * 0.19f, oy + s * 0.28f, s * 0.38f, s * 0.54f));
			DrawRect(new Rect2(cx - s * 0.19f, oy + s * 0.40f, s * 0.38f, s * 0.32f), BodyColor, true);
			DrawLine(new Vector2(cx - s * 0.13f, oy + s * 0.42f), new Vector2(cx - s * 0.13f, oy + s * 0.70f), BodyShade, 1.4f);
			DrawLine(new Vector2(cx + s * 0.13f, oy + s * 0.42f), new Vector2(cx + s * 0.13f, oy + s * 0.70f), BodyShade, 1.4f);
			// Rear hatch glass strip.
			DrawGlass(new Rect2(cx - s * 0.16f, oy + s * 0.80f, s * 0.32f, s * 0.08f));
		}

		/// <summary>Heavy truck: short cab, huge squared cargo box with container ribs — widest body on the shelf.</summary>
		private void DrawHeavyTruck(float cx, float oy, float s)
		{
			var top = oy + s * 0.03f;
			var bot = oy + s * 0.99f;
			var halfW = s * 0.33f;

			DrawWheel(cx - halfW, oy + s * 0.17f, s * 0.15f, s * 0.18f);
			DrawWheel(cx + halfW, oy + s * 0.17f, s * 0.15f, s * 0.18f);
			DrawWheel(cx - halfW, oy + s * 0.62f, s * 0.15f, s * 0.18f);
			DrawWheel(cx + halfW, oy + s * 0.62f, s * 0.15f, s * 0.18f);
			DrawWheel(cx - halfW, oy + s * 0.84f, s * 0.15f, s * 0.18f);
			DrawWheel(cx + halfW, oy + s * 0.84f, s * 0.15f, s * 0.18f);

			// Cab block.
			DrawBody(new[]
			{
				new Vector2(cx - halfW * 0.78f, top),
				new Vector2(cx + halfW * 0.78f, top),
				new Vector2(cx + halfW * 0.88f, top + s * 0.07f),
				new Vector2(cx + halfW * 0.88f, oy + s * 0.30f),
				new Vector2(cx - halfW * 0.88f, oy + s * 0.30f),
				new Vector2(cx - halfW * 0.88f, top + s * 0.07f),
			});
			DrawGlass(new Rect2(cx - s * 0.22f, oy + s * 0.115f, s * 0.44f, s * 0.115f));

			// Cargo box: full width, ribbed like a container.
			var box = new Rect2(cx - halfW, oy + s * 0.33f, halfW * 2f, bot - (oy + s * 0.33f));
			DrawRect(box, BodyColor, true);
			DrawRect(box, BodyEdge, false, 1.4f);
			for (var i = 1; i <= 4; i++)
			{
				var y = box.Position.Y + box.Size.Y * i / 5f;
				DrawLine(new Vector2(box.Position.X + 2f, y), new Vector2(box.End.X - 2f, y), BodyShade, 1.1f);
			}
		}

		/// <summary>Semi tractor: long cab block up front, then a short bare chassis tail with the
		/// fifth-wheel coupler plate at the rear third — a hauler sold without its trailer.</summary>
		private void DrawSemiTruck(float cx, float oy, float s)
		{
			var top = oy + s * 0.03f;
			var bot = oy + s * 0.97f;
			var halfW = s * 0.31f;

			DrawWheel(cx - halfW, oy + s * 0.20f, s * 0.15f, s * 0.18f);
			DrawWheel(cx + halfW, oy + s * 0.20f, s * 0.15f, s * 0.18f);
			DrawWheel(cx - halfW, oy + s * 0.78f, s * 0.15f, s * 0.18f);
			DrawWheel(cx + halfW, oy + s * 0.78f, s * 0.15f, s * 0.18f);

			// Long cab block — the tractor unit is mostly cab.
			DrawBody(new[]
			{
				new Vector2(cx - halfW * 0.76f, top),
				new Vector2(cx + halfW * 0.76f, top),
				new Vector2(cx + halfW * 0.90f, top + s * 0.08f),
				new Vector2(cx + halfW * 0.90f, oy + s * 0.52f),
				new Vector2(cx - halfW * 0.90f, oy + s * 0.52f),
				new Vector2(cx - halfW * 0.90f, top + s * 0.08f),
			});
			DrawGlass(new Rect2(cx - s * 0.21f, oy + s * 0.13f, s * 0.42f, s * 0.12f));
			// Twin exhaust stacks flank the back of the cab.
			DrawCircle(new Vector2(cx - halfW * 0.70f, oy + s * 0.47f), s * 0.035f, BodyShade);
			DrawCircle(new Vector2(cx + halfW * 0.70f, oy + s * 0.47f), s * 0.035f, BodyShade);

			// Short bare chassis tail: twin frame rails, no cargo box.
			var rail = halfW * 0.58f;
			var chassisTop = oy + s * 0.54f;
			var chassis = new Rect2(cx - rail, chassisTop, rail * 2f, bot - chassisTop);
			DrawRect(chassis, WheelColor, true);
			DrawRect(chassis, BodyEdge, false, 1.2f);
			DrawLine(new Vector2(cx - rail * 0.52f, chassisTop + 2f), new Vector2(cx - rail * 0.52f, bot - 2f), BodyShade, 1.2f);
			DrawLine(new Vector2(cx + rail * 0.52f, chassisTop + 2f), new Vector2(cx + rail * 0.52f, bot - 2f), BodyShade, 1.2f);

			// Fifth-wheel coupler plate at the rear third — towing is this chassis's whole pitch.
			var coupler = new Vector2(cx, oy + s * 0.78f);
			DrawCircle(coupler, s * 0.105f, BodyColor);
			DrawCircle(coupler, s * 0.06f, WheelColor);
			DrawCircle(coupler, s * 0.022f, BodyEdge);
		}

		/// <summary>Trailer: coupler + A-frame drawbar into a cargo box; wheel studs show the axle count
		/// (one stud per side on the 2-tire utility, tandem pair per side on the 4-tire cargo box).</summary>
		private void DrawTrailer(float cx, float oy, float s)
		{
			var tandem = TireCount >= 4;
			var halfW = tandem ? s * 0.27f : s * 0.22f;
			var boxTop = oy + s * (tandem ? 0.30f : 0.38f);
			var boxBot = oy + s * 0.94f;

			// Coupler ball + A-frame drawbar.
			var hitch = new Vector2(cx, oy + s * 0.07f);
			DrawLine(hitch, new Vector2(cx - halfW * 0.66f, boxTop + 2f), BodyShade, 2f);
			DrawLine(hitch, new Vector2(cx + halfW * 0.66f, boxTop + 2f), BodyShade, 2f);
			DrawCircle(hitch, s * 0.05f, BodyEdge);

			// Wheel studs poke out the sides: single axle vs tandem pair.
			foreach (var a in tandem ? new[] { 0.52f, 0.74f } : new[] { 0.64f })
			{
				DrawWheel(cx - halfW, oy + s * a, s * 0.12f, s * 0.15f);
				DrawWheel(cx + halfW, oy + s * a, s * 0.12f, s * 0.15f);
			}

			var box = new Rect2(cx - halfW, boxTop, halfW * 2f, boxBot - boxTop);
			DrawRect(box, BodyColor, true);
			DrawRect(box, BodyEdge, false, 1.2f);
			if (tandem)
			{
				// Enclosed cargo box: roof seam down the middle.
				DrawLine(new Vector2(cx, boxTop + 2f), new Vector2(cx, boxBot - 2f), BodyShade, 1.2f);
			}
			else
			{
				// Open utility bed: dark hollow inside the rim.
				DrawRect(box.GrowIndividual(-s * 0.05f, -s * 0.05f, -s * 0.05f, -s * 0.05f), WheelColor, true);
			}
		}
	}

	// --- Part glyph icons -----------------------------------------------------------------------------

	internal enum PartGlyph
	{
		MgBarrel,
		Rocket,
		Missile,
		MineDisc,
		DropperCanister,
		Piston,
		Chip,
		Canister,
	}

	/// <summary>
	/// Hand-drawn part glyphs for store rows. The generated SVG icon set shares one blocky
	/// canister-like silhouette at 34 px, so every category read as the same jug; these draw a
	/// distinct shape per part family in the same palette as VehicleSilhouetteIcon.
	/// </summary>
	internal partial class PartGlyphIcon : Control
	{
		public PartGlyph Glyph { get; set; } = PartGlyph.MgBarrel;

		private static readonly Color Steel = new(0.62f, 0.68f, 0.74f);
		private static readonly Color SteelDark = new(0.40f, 0.45f, 0.51f);
		private static readonly Color Ink = new(0.10f, 0.11f, 0.13f, 0.95f);
		private static readonly Color Gold = new(0.93f, 0.77f, 0.32f);
		private static readonly Color Cyan = new(0.45f, 0.85f, 1f, 0.85f);

		public PartGlyphIcon()
		{
			MouseFilter = MouseFilterEnum.Ignore;
			CustomMinimumSize = new Vector2(34f, 34f);
		}

		public override void _Draw()
		{
			var size = Size;
			if (size.X < 12f || size.Y < 12f)
				return;

			var s = Mathf.Min(size.X, size.Y);
			var cx = size.X * 0.5f;
			var cy = size.Y * 0.5f;
			switch (Glyph)
			{
				case PartGlyph.Rocket: DrawRocket(cx, cy, s); break;
				case PartGlyph.Missile: DrawMissile(cx, cy, s); break;
				case PartGlyph.MineDisc: DrawMine(cx, cy, s); break;
				case PartGlyph.DropperCanister: DrawDropper(cx, cy, s); break;
				case PartGlyph.Piston: DrawPiston(cx, cy, s); break;
				case PartGlyph.Chip: DrawChip(cx, cy, s); break;
				case PartGlyph.Canister: DrawCanister(cx, cy, s); break;
				default: DrawMgBarrel(cx, cy, s); break;
			}
		}

		/// <summary>Top-down MG: muzzle brake, barrel, receiver, side ammo box with gold belt.</summary>
		private void DrawMgBarrel(float cx, float cy, float s)
		{
			var top = cy - s * 0.46f;
			DrawRect(new Rect2(cx - s * 0.085f, top, s * 0.17f, s * 0.10f), SteelDark, true);
			DrawRect(new Rect2(cx - s * 0.045f, top + s * 0.10f, s * 0.09f, s * 0.40f), Steel, true);
			var receiver = new Rect2(cx - s * 0.17f, top + s * 0.50f, s * 0.34f, s * 0.30f);
			DrawRect(receiver, Steel, true);
			DrawRect(receiver, Steel.Lightened(0.22f), false, 1f);
			DrawRect(new Rect2(cx - s * 0.10f, top + s * 0.585f, s * 0.20f, s * 0.05f), Ink, true);
			var box = new Rect2(cx + s * 0.18f, top + s * 0.54f, s * 0.17f, s * 0.18f);
			DrawRect(box, SteelDark, true);
			DrawLine(new Vector2(box.Position.X + 1f, box.GetCenter().Y), new Vector2(box.End.X - 1f, box.GetCenter().Y), Gold, 1.5f);
		}

		/// <summary>Unguided rocket: fat body, gold nose cone, splayed tail fins, exhaust flame.</summary>
		private void DrawRocket(float cx, float cy, float s)
		{
			var top = cy - s * 0.46f;
			var bodyW = s * 0.20f;
			var body = new Rect2(cx - bodyW * 0.5f, top + s * 0.16f, bodyW, s * 0.42f);
			DrawColoredPolygon(new[]
			{
				new Vector2(body.Position.X, body.Position.Y),
				new Vector2(body.End.X, body.Position.Y),
				new Vector2(cx, top),
			}, Gold);
			DrawRect(body, Steel, true);
			DrawRect(body, Steel.Lightened(0.22f), false, 1f);
			DrawColoredPolygon(new[]
			{
				new Vector2(body.Position.X, body.End.Y - s * 0.10f),
				new Vector2(body.Position.X - s * 0.11f, body.End.Y + s * 0.06f),
				new Vector2(body.Position.X, body.End.Y + s * 0.06f),
			}, SteelDark);
			DrawColoredPolygon(new[]
			{
				new Vector2(body.End.X, body.End.Y - s * 0.10f),
				new Vector2(body.End.X + s * 0.11f, body.End.Y + s * 0.06f),
				new Vector2(body.End.X, body.End.Y + s * 0.06f),
			}, SteelDark);
			DrawColoredPolygon(new[]
			{
				new Vector2(cx - s * 0.06f, body.End.Y + s * 0.08f),
				new Vector2(cx + s * 0.06f, body.End.Y + s * 0.08f),
				new Vector2(cx, body.End.Y + s * 0.26f),
			}, Gold);
		}

		/// <summary>Guided missile: slim body, canard + tail fins, cyan lock reticle at the seeker.</summary>
		private void DrawMissile(float cx, float cy, float s)
		{
			var top = cy - s * 0.44f;
			var bodyW = s * 0.13f;
			var body = new Rect2(cx - bodyW * 0.5f, top + s * 0.14f, bodyW, s * 0.52f);
			DrawColoredPolygon(new[]
			{
				new Vector2(body.Position.X, body.Position.Y),
				new Vector2(body.End.X, body.Position.Y),
				new Vector2(cx, top),
			}, Gold);
			DrawRect(body, Steel, true);
			foreach (var (yRatio, fin) in new[] { (0.34f, s * 0.07f), (0.60f, s * 0.11f) })
			{
				var y = top + s * yRatio;
				DrawColoredPolygon(new[]
				{
					new Vector2(body.Position.X, y),
					new Vector2(body.Position.X - fin, y + s * 0.06f),
					new Vector2(body.Position.X, y + s * 0.06f),
				}, SteelDark);
				DrawColoredPolygon(new[]
				{
					new Vector2(body.End.X, y),
					new Vector2(body.End.X + fin, y + s * 0.06f),
					new Vector2(body.End.X, y + s * 0.06f),
				}, SteelDark);
			}
			// Lock reticle around the seeker head — reads "guided" vs the dumb rocket.
			DrawArc(new Vector2(cx, top + s * 0.10f), s * 0.15f, 0f, Mathf.Tau, 20, Cyan, 1.5f);
			DrawColoredPolygon(new[]
			{
				new Vector2(cx - s * 0.045f, body.End.Y + s * 0.03f),
				new Vector2(cx + s * 0.045f, body.End.Y + s * 0.03f),
				new Vector2(cx, body.End.Y + s * 0.18f),
			}, Gold);
		}

		/// <summary>Top-down mine: spiked disc with a gold pressure plate.</summary>
		private void DrawMine(float cx, float cy, float s)
		{
			var center = new Vector2(cx, cy);
			var r = s * 0.30f;
			for (var i = 0; i < 8; i++)
			{
				var a = i / 8f * Mathf.Tau;
				var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
				DrawLine(center + dir * r, center + dir * (r + s * 0.09f), SteelDark, 2f);
			}
			DrawCircle(center, r, Steel);
			DrawArc(center, r, 0f, Mathf.Tau, 28, Steel.Lightened(0.22f), 1f);
			DrawArc(center, r * 0.62f, 0f, Mathf.Tau, 24, Ink, 1.5f);
			DrawCircle(center, r * 0.30f, Gold);
		}

		/// <summary>Rear-deploy pod: squat hopper, open hatch, payload pellets falling behind.</summary>
		private void DrawDropper(float cx, float cy, float s)
		{
			var top = cy - s * 0.46f;
			var body = new Rect2(cx - s * 0.19f, top + s * 0.06f, s * 0.38f, s * 0.42f);
			DrawRect(new Rect2(cx - s * 0.10f, top, s * 0.20f, s * 0.06f), SteelDark, true);
			DrawRect(body, Steel, true);
			DrawRect(body, Steel.Lightened(0.22f), false, 1f);
			DrawRect(new Rect2(cx - s * 0.12f, body.End.Y - s * 0.07f, s * 0.24f, s * 0.07f), Ink, true);
			DrawCircle(new Vector2(cx - s * 0.07f, body.End.Y + s * 0.10f), s * 0.045f, Gold);
			DrawCircle(new Vector2(cx + s * 0.06f, body.End.Y + s * 0.20f), s * 0.045f, Gold);
			DrawCircle(new Vector2(cx - s * 0.02f, body.End.Y + s * 0.32f), s * 0.045f, Gold);
		}

		/// <summary>Engine: piston crown with ring grooves, connecting rod, crank throw.</summary>
		private void DrawPiston(float cx, float cy, float s)
		{
			var top = cy - s * 0.44f;
			var crown = new Rect2(cx - s * 0.19f, top, s * 0.38f, s * 0.24f);
			DrawRect(crown, Steel, true);
			DrawRect(crown, Steel.Lightened(0.22f), false, 1f);
			DrawLine(new Vector2(crown.Position.X + 2f, top + s * 0.07f), new Vector2(crown.End.X - 2f, top + s * 0.07f), Ink, 1.5f);
			DrawLine(new Vector2(crown.Position.X + 2f, top + s * 0.13f), new Vector2(crown.End.X - 2f, top + s * 0.13f), Ink, 1.5f);
			DrawLine(new Vector2(crown.Position.X + s * 0.03f, crown.End.Y), new Vector2(crown.Position.X + s * 0.03f, crown.End.Y + s * 0.07f), Steel, 2f);
			DrawLine(new Vector2(crown.End.X - s * 0.03f, crown.End.Y), new Vector2(crown.End.X - s * 0.03f, crown.End.Y + s * 0.07f), Steel, 2f);
			var pin = new Vector2(cx, top + s * 0.30f);
			var crank = new Vector2(cx + s * 0.10f, top + s * 0.70f);
			DrawLine(pin, crank, Steel, 3f);
			DrawCircle(pin, s * 0.05f, SteelDark);
			DrawCircle(crank, s * 0.13f, SteelDark);
			DrawCircle(crank, s * 0.05f, Gold);
		}

		/// <summary>Targeting computer: IC package with legs and a gold core.</summary>
		private void DrawChip(float cx, float cy, float s)
		{
			var half = s * 0.22f;
			var body = new Rect2(cx - half, cy - half, half * 2f, half * 2f);
			for (var i = 0; i < 3; i++)
			{
				var offset = (i - 1) * half * 0.6f;
				DrawLine(new Vector2(cx + offset, body.Position.Y - s * 0.08f), new Vector2(cx + offset, body.Position.Y), Steel, 2f);
				DrawLine(new Vector2(cx + offset, body.End.Y), new Vector2(cx + offset, body.End.Y + s * 0.08f), Steel, 2f);
				DrawLine(new Vector2(body.Position.X - s * 0.08f, cy + offset), new Vector2(body.Position.X, cy + offset), Steel, 2f);
				DrawLine(new Vector2(body.End.X, cy + offset), new Vector2(body.End.X + s * 0.08f, cy + offset), Steel, 2f);
			}
			DrawRect(body, SteelDark, true);
			DrawRect(body, Steel.Lightened(0.10f), false, 1f);
			var core = new Rect2(cx - half * 0.45f, cy - half * 0.45f, half * 0.9f, half * 0.9f);
			DrawRect(core, Gold, true);
			DrawCircle(new Vector2(body.Position.X + s * 0.045f, body.Position.Y + s * 0.045f), s * 0.02f, Ink);
		}

		/// <summary>Supplies: jerry can with gold filler cap, recessed handle, stamped X brace.</summary>
		private void DrawCanister(float cx, float cy, float s)
		{
			var top = cy - s * 0.42f;
			var body = new Rect2(cx - s * 0.21f, top + s * 0.14f, s * 0.42f, s * 0.62f);
			DrawRect(body, Steel, true);
			DrawRect(body, Steel.Lightened(0.22f), false, 1f);
			DrawRect(new Rect2(cx + s * 0.05f, top + s * 0.04f, s * 0.11f, s * 0.10f), Gold, true);
			DrawRect(new Rect2(cx - s * 0.15f, top + s * 0.20f, s * 0.30f, s * 0.055f), Ink, true);
			DrawLine(new Vector2(body.Position.X + 3f, top + s * 0.32f), new Vector2(body.End.X - 3f, body.End.Y - 3f), SteelDark, 2f);
			DrawLine(new Vector2(body.End.X - 3f, top + s * 0.32f), new Vector2(body.Position.X + 3f, body.End.Y - 3f), SteelDark, 2f);
		}
	}
}

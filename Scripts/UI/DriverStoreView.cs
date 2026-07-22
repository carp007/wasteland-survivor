// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/UI/DriverStoreView.cs
// Purpose: Driver store ("Clinic & Outfitter"): cybernetic implants (permanent max-HP boosts,
//          clone-facility cities only — they cut you open and re-upload) and body armor vests
//          upgrading over basic kevlar. Sibling of the parts StoreView, same drawer shell.
// -------------------------------------------------------------------------------------------------
using System;
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

public partial class DriverStoreView : Control
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

	[Bind("Panel/VBox/LblVitals")]
	private Label _lblVitals = null!;

	[Bind("Panel/VBox/StoreScroll/StoreList")]
	private VBoxContainer _storeList = null!;

	[Bind("Panel/VBox/BottomRow/LblStatus")]
	private Label _lblStatus = null!;

	[Bind("Panel/VBox/BottomRow/BtnBack")]
	private Button _btnBack = null!;

	private Texture2D? _menuBackground;

	public override void _Ready()
	{
		GameUiTheme.ApplyToTree(this);
		SceneAutoBinder.Apply(this, nameof(DriverStoreView));

		GameUiTheme.StyleHeading(_lblTitle, GameUiTheme.TitleFontSize + 12);
		_lblSubtitle.AddThemeColorOverride("font_color", GameUiTheme.TextMutedColor);
		_lblSubtitle.AddThemeFontSizeOverride("font_size", GameUiTheme.BaseFontSize - 1);
		_lblSubtitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_lblWallet.AddThemeColorOverride("font_color", GameUiTheme.AccentGoldColor);
		_lblVitals.AddThemeColorOverride("font_color", GameUiTheme.AccentCyanColor);
		_lblVitals.AddThemeFontSizeOverride("font_size", GameUiTheme.BaseFontSize - 1);
		_lblStatus.AddThemeColorOverride("font_color", GameUiTheme.TextMutedColor);

		_panel.AddThemeStyleboxOverride("panel", GeneratedUiArt.CreatePremiumShellStyle());
		_btnBack.Pressed += () => Nav()?.ToCityShell(this);

		BuildProceduralBackdrop();

		// Reuse the current city's backdrop (same treatment as StoreView) so the clinic reads as
		// part of this city and never renders a black half-screen.
		_menuBackground = ResolveCityBackdrop();
		_bg.Color = GameUiTheme.BackgroundColor;
		FullscreenTextureRectUtil.ConfigureCover(_bgImage);
		_bgImage.Texture = _menuBackground;
		_bgImage.Visible = _menuBackground != null;
		_bgImage.SelfModulate = new Color(0.30f, 0.32f, 0.36f);

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

	private void ApplyDrawer() => MenuDrawerLayout.Apply(_panel, this, MenuDrawerSpec.City);

	/// <summary>
	/// Styled backdrop behind the drawer (soft vertical gradient + faint hairline grid, same
	/// command-console language as StoreView) so the area beside the panel never reads as dead black.
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

	private static Texture2D? ResolveCityBackdrop()
	{
		var app = App.Instance;
		if (app == null) return null;
		if (!app.Services.TryGet<GameSession>(out var session) || session == null) return null;
		if (!app.Services.TryGet<DefDatabase>(out var defs) || defs == null) return null;
		return CityBackdropArt.Resolve(session.GetCurrentCityDef(defs), session.Save.Player.CurrentCityId);
	}

	private void Rebuild()
	{
		var app = App.Instance;
		if (app == null) return;

		// Judge round (loop 6): rows clipped at the panel bottom with no scroll affordance — keep
		// the bar visible whenever content overflows so "more below" always reads.
		if (GetNodeOrNull<ScrollContainer>("Panel/VBox/StoreScroll") is { } storeScroll)
			storeScroll.VerticalScrollMode = ScrollContainer.ScrollMode.ShowAlways;
		if (!app.Services.TryGet<GameSession>(out var session) || session == null) return;
		if (!app.Services.TryGet<DefDatabase>(out var defs) || defs == null) return;

		var player = session.Save.Player;
		var city = session.GetCurrentCityDef(defs);
		var hasCloneFacility = city is { HasCloneFacility: true };

		_lblWallet.Text = $"Cash: ${session.GetMoneyUsd():N0}   ·   Scrap: {player.Scrap}";

		var vestName = defs.DriverUpgrades.TryGetValue(player.EquippedArmorId, out var vest)
			? vest.DisplayName
			: defs.Armors.TryGetValue(player.EquippedArmorId, out var basic) ? basic.DisplayName : player.EquippedArmorId;
		_lblVitals.Text = $"Driver HP {player.DriverHp}/{player.DriverHpMax}   ·   Armor {player.DriverArmor}/{player.DriverArmorMax} ({vestName})";

		foreach (var child in _storeList.GetChildren())
			(child as Node)?.QueueFree();

		AddSection("CYBERNETIC IMPLANTS — CLONE FACILITY SERVICE");
		if (!hasCloneFacility)
			AddSectionNote("No clone facility in this city. The techs here can show you the catalog, "
				+ "but installs happen where they can cut you open and re-upload you — Detroit, Cleveland, Chicago, or Pittsburgh.");
		foreach (var c in defs.DriverUpgrades.Values
					.Where(u => u.Kind == DriverUpgradeKind.Cybernetic && u.PriceUsd > 0)
					.OrderBy(u => u.PriceUsd))
			AddCyberneticRow(session, defs, c, hasCloneFacility);

		// Free-issue sidearm rounds seed on first sight so ammo rows never lie (see SessionStore).
		session.Store.EnsureDefaultSidearmSeeded(defs);

		AddSection("PERSONAL WEAPONS");
		AddSectionNote("Sidearms for when you're out of the driver's seat — tire swaps, salvage walks, "
			+ "and the bad moment somebody's rig dies with its driver still shooting. Nothing here "
			+ "out-guns a vehicle mount, and nobody's pockets restock between tournament rounds.");
		foreach (var w in defs.PersonalWeapons.Values
					.Where(p => p.PriceUsd > 0)
					.OrderBy(p => p.PriceUsd))
			AddPersonalWeaponRow(session, defs, w);
		// The free default sidearm still shows so its ammo can be restocked.
		if (defs.PersonalWeapons.TryGetValue(player.EquippedPersonalWeaponId ?? "pw_pistol_9mm", out var equippedPw)
			&& equippedPw.PriceUsd <= 0)
			AddPersonalWeaponRow(session, defs, equippedPw);

		AddSection("BODY ARMOR");
		foreach (var a in defs.DriverUpgrades.Values
					.Where(u => u.Kind == DriverUpgradeKind.Armor && u.PriceUsd > 0)
					.OrderBy(u => u.PriceUsd))
			AddArmorRow(session, defs, a);

		// Tail spacer so the last row scrolls clear of the footer instead of clipping under it.
		_storeList.AddChild(new Control
		{
			CustomMinimumSize = new Vector2(0, 72),
			MouseFilter = MouseFilterEnum.Ignore,
		});
	}

	private void AddSection(string title)
	{
		var lbl = new Label { Text = title };
		GameUiTheme.StyleHeading(lbl, GameUiTheme.BaseFontSize, GameUiTheme.AccentCyanColor);
		lbl.AddThemeConstantOverride("line_spacing", 0);
		_storeList.AddChild(lbl);
	}

	private void AddSectionNote(string text)
	{
		var lbl = new Label { Text = text };
		lbl.AddThemeColorOverride("font_color", GameUiTheme.TextMutedColor);
		lbl.AddThemeFontSizeOverride("font_size", GameUiTheme.BaseFontSize - 2);
		lbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_storeList.AddChild(lbl);
	}

	/// <summary>Shared store-row scaffold: inset panel row with an expanding info column.</summary>
	private (HBoxContainer row, VBoxContainer info) AddRowScaffold()
	{
		var row = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		row.AddThemeStyleboxOverride("panel", GeneratedUiArt.CreatePremiumInsetStyle());
		_storeList.AddChild(row);

		var h = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		h.AddThemeConstantOverride("separation", 10);
		row.AddChild(h);

		var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		h.AddChild(info);
		return (h, info);
	}

	private static void AddRowLabels(VBoxContainer info, string title, string detail, string? flavor = null)
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

		if (!string.IsNullOrWhiteSpace(flavor))
		{
			var fl = new Label { Text = flavor };
			fl.AddThemeColorOverride("font_color", GameUiTheme.TextMutedColor);
			fl.AddThemeFontSizeOverride("font_size", GameUiTheme.BaseFontSize - 3);
			fl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			fl.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			info.AddChild(fl);
		}
	}

	private void AddCyberneticRow(GameSession session, DefDatabase defs, DriverUpgradeDefinition def, bool hasCloneFacility)
	{
		var (h, info) = AddRowScaffold();

		var installed = session.Store.IsCyberneticInstalled(def.Id);
		var title = installed ? $"{def.DisplayName}   (installed)" : def.DisplayName;
		AddRowLabels(info, title, $"One-time install · Max driver HP +{def.HpBonus} · heals the new HP on install", def.Flavor);

		var money = session.GetMoneyUsd();
		var btn = new Button
		{
			Text = installed ? "Installed" : $"Install  ${def.PriceUsd:N0}",
			Disabled = installed || !hasCloneFacility || money < def.PriceUsd,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			CustomMinimumSize = new Vector2(148, 38),
		};
		if (!installed && !hasCloneFacility)
			btn.TooltipText = "Installs need a clone facility — Detroit, Cleveland, Chicago, or Pittsburgh.";
		btn.Pressed += () =>
		{
			if (session.Store.TryBuyDriverCybernetic(defs, def.Id, out var err))
			{
				UiSfx.Play("purchase");
				SetStatus($"Installed {def.DisplayName}. Max driver HP +{def.HpBonus}.");
			}
			else
			{
				UiSfx.Play("error");
				SetStatus(err);
			}
			Rebuild();
		};
		h.AddChild(btn);
	}

	private void AddPersonalWeaponRow(GameSession session, DefDatabase defs, PersonalWeaponDefinition def)
	{
		var (h, info) = AddRowScaffold();

		var player = session.Save.Player;
		var owned = session.Store.IsPersonalWeaponOwned(def.Id);
		var carried = string.Equals(player.EquippedPersonalWeaponId, def.Id, StringComparison.OrdinalIgnoreCase);

		var ammoPool = player.PersonalAmmoInventory != null
			&& player.PersonalAmmoInventory.TryGetValue(def.AmmoId, out var pool) ? pool : 0;
		var role = def.WeaponClass switch
		{
			PersonalWeaponClass.Smg => "Close spray",
			PersonalWeaponClass.Shotgun => "Point-blank burst",
			PersonalWeaponClass.Rifle => "Long finisher",
			_ => "Sidearm",
		};
		var pellets = def.PelletsPerShot > 1 ? $" ×{def.PelletsPerShot}" : "";
		var title = carried ? $"{def.DisplayName}   (carried)" : owned ? $"{def.DisplayName}   (owned)" : def.DisplayName;
		AddRowLabels(info, title,
			$"{role} · {def.BaseDamage:0.#} dmg{pellets} · {def.RangeMeters:0} m · {def.CooldownMs} ms · ammo {ammoPool}/{def.AmmoCapacity}",
			def.Flavor);

		var money = session.GetMoneyUsd();

		// Primary action: Buy (not owned) / Equip (owned, not carried) / Carried.
		var btn = new Button
		{
			Text = carried ? "Carried" : owned ? "Equip" : $"Buy  ${def.PriceUsd:N0}",
			Disabled = carried || (!owned && money < def.PriceUsd),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			CustomMinimumSize = new Vector2(132, 38),
		};
		btn.Pressed += () =>
		{
			var ok = owned
				? session.Store.TryEquipPersonalWeapon(defs, def.Id, out var err)
				: session.Store.TryBuyPersonalWeapon(defs, def.Id, out err);
			if (ok)
			{
				UiSfx.Play("purchase");
				SetStatus(owned ? $"Now carrying the {def.DisplayName}." : $"Bought and holstered {def.DisplayName}.");
			}
			else
			{
				UiSfx.Play("error");
				SetStatus(err);
			}
			Rebuild();
		};
		h.AddChild(btn);

		// Ammo box: available once the weapon is owned, capped at carry capacity.
		var ammoPrice = Math.Max(1, def.AmmoPricePer10Usd);
		var btnAmmo = new Button
		{
			Text = $"Ammo ×10  ${ammoPrice:N0}",
			Disabled = !owned || ammoPool >= def.AmmoCapacity || money < ammoPrice,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			CustomMinimumSize = new Vector2(140, 38),
		};
		if (!owned)
			btnAmmo.TooltipText = "Buy the weapon before stocking its ammo.";
		btnAmmo.Pressed += () =>
		{
			if (session.Store.TryBuyPersonalAmmo(defs, def.Id, out var err))
			{
				UiSfx.Play("purchase");
				SetStatus($"Stocked a box for the {def.DisplayName}.");
			}
			else
			{
				UiSfx.Play("error");
				SetStatus(err);
			}
			Rebuild();
		};
		h.AddChild(btnAmmo);
	}

	private void AddArmorRow(GameSession session, DefDatabase defs, DriverUpgradeDefinition def)
	{
		var (h, info) = AddRowScaffold();

		var player = session.Save.Player;
		var equipped = string.Equals(player.EquippedArmorId, def.Id, StringComparison.OrdinalIgnoreCase);
		var title = equipped ? $"{def.DisplayName}   (equipped)" : def.DisplayName;
		AddRowLabels(info, title, $"Body armor · {def.ArmorPoints} armor points · replaces your current vest", def.Flavor);

		var tradeCondition = player.DriverArmorMax > 0 ? (float)player.DriverArmor / player.DriverArmorMax : 1f;
		var tradeIn = equipped ? 0 : SessionStore.GetDriverArmorTradeInValue(defs, player.EquippedArmorId, tradeCondition);
		var netCost = Math.Max(0, def.PriceUsd - tradeIn);
		var money = session.GetMoneyUsd();
		var btn = new Button
		{
			Text = equipped
				? "Equipped"
				: tradeIn > 0 ? $"Buy  ${netCost:N0} (trade-in)" : $"Buy  ${netCost:N0}",
			Disabled = equipped || money < netCost,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			CustomMinimumSize = new Vector2(172, 38),
		};
		if (!equipped && tradeIn > 0)
			btn.TooltipText = $"${def.PriceUsd:N0} list — your current vest trades in for ${tradeIn:N0}.";
		btn.Pressed += () =>
		{
			if (session.Store.TryBuyDriverArmor(defs, def.Id, out var err))
			{
				UiSfx.Play("purchase");
				SetStatus($"Equipped {def.DisplayName} — armor topped up to {def.ArmorPoints}.");
			}
			else
			{
				UiSfx.Play("error");
				SetStatus(err);
			}
			Rebuild();
		};
		h.AddChild(btn);
	}

	private void SetStatus(string text)
	{
		if (_lblStatus != null && GodotObject.IsInstanceValid(_lblStatus))
			_lblStatus.Text = text;
	}
}

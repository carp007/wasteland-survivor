// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/UI/WorkshopView.cs
// Purpose: Workshop screen where the player installs engines/computers/weapons and buys/refills ammo.
// -------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WastelandSurvivor.Core.Defs;
using WastelandSurvivor.Core.IO;
using WastelandSurvivor.Core.State;
using GameUiKit.SceneBinding;
using GameUiKit.UI;
using WastelandSurvivor.Game;
using WastelandSurvivor.Game.Audio;
using WastelandSurvivor.Game.Navigation;
using WastelandSurvivor.Game.Systems;

namespace WastelandSurvivor.Game.UI;

public partial class WorkshopView : Control
{
	[Bind("Bg", Optional = true)]
	private ColorRect? _bg;

	[Bind("BgImage", Optional = true)]
	private TextureRect? _bgImage;

	[Bind("Panel", Optional = true)]
	private PanelContainer? _panel;

	[Bind("Panel/VBox/HeaderArt", Optional = true)]
	private TextureRect? _headerArt;

	[Bind("Panel/VBox/ContextPanel", Optional = true)]
	private PanelContainer? _contextPanel;
	[Bind("Panel/VBox/ContextPanel/ContextVBox/LblContextSummary", Optional = true)]
	private Label? _lblContextSummary;

	[Bind("Panel/VBox/BodyTabs", Optional = true)]
	private TabContainer? _bodyTabs;

	[Bind("Panel/VBox/BodyTabs/OverviewTab/OverviewScroll/OverviewContent/ActivePanel", Fallback = "Panel/VBox/ContentColumns/LeftColumn/ActivePanel", Optional = true)]
	private PanelContainer? _activePanel;
	[Bind("Panel/VBox/BodyTabs/OverviewTab/OverviewScroll/OverviewContent/ActionsPanel", Fallback = "Panel/VBox/ContentColumns/LeftColumn/ActionsPanel", Optional = true)]
	private PanelContainer? _actionsPanel;
	[Bind("Panel/VBox/BodyTabs/OverviewTab/OverviewScroll/OverviewContent/SystemsPanel", Fallback = "Panel/VBox/ContentColumns/RightColumn/SystemsPanel", Optional = true)]
	private PanelContainer? _systemsPanel;
	[Bind("Panel/VBox/BodyTabs/MountsTab/MountsPanel", Fallback = "Panel/VBox/ContentColumns/RightColumn/MountsPanel", Optional = true)]
	private PanelContainer? _mountsPanel;
	[Bind("Panel/VBox/BodyTabs/AmmoTab/AmmoPanel", Fallback = "Panel/VBox/ContentColumns/RightColumn/AmmoPanel", Optional = true)]
	private PanelContainer? _ammoPanel;

	[Bind("Panel/VBox/BodyTabs/OverviewTab/OverviewScroll/OverviewContent/ActivePanel/ActiveVBox/LblActive", Fallback = "Panel/VBox/ContentColumns/LeftColumn/ActivePanel/ActiveVBox/LblActive")]
	private Label _lblActive = null!;
	[Bind("Panel/VBox/BodyTabs/OverviewTab/OverviewScroll/OverviewContent/ActivePanel/ActiveVBox/ShowcaseHost", Fallback = "Panel/VBox/ContentColumns/LeftColumn/ActivePanel/ActiveVBox/ShowcaseHost")]
	private VBoxContainer _showcaseHost = null!;
	[Bind("Panel/VBox/BodyTabs/OverviewTab/OverviewScroll/OverviewContent/SystemsPanel/SystemsVBox/EngineRow/OptEngine", Fallback = "Panel/VBox/ContentColumns/RightColumn/SystemsPanel/SystemsVBox/EngineRow/OptEngine")]
	private OptionButton _optEngine = null!;
	[Bind("Panel/VBox/BodyTabs/OverviewTab/OverviewScroll/OverviewContent/SystemsPanel/SystemsVBox/ComputerRow/OptComputer", Fallback = "Panel/VBox/ContentColumns/RightColumn/SystemsPanel/SystemsVBox/ComputerRow/OptComputer")]
	private OptionButton _optComputer = null!;
	[Bind("Panel/VBox/BodyTabs/MountsTab/MountsPanel/MountsVBox/MountsScroll/VBoxMounts", Fallback = "Panel/VBox/ContentColumns/RightColumn/MountsPanel/MountsVBox/MountsScroll/VBoxMounts")]
	private VBoxContainer _vboxMounts = null!;
	[Bind("Panel/VBox/BodyTabs/OverviewTab/OverviewScroll/OverviewContent/ActionsPanel/ActionsVBox/ActionGrid/BtnApply", Fallback = "Panel/VBox/ContentColumns/LeftColumn/ActionsPanel/ActionsVBox/ActionGrid/BtnApply")]
	private Button _btnApply = null!;
	[Bind("Panel/VBox/BodyTabs/OverviewTab/OverviewScroll/OverviewContent/ActionsPanel/ActionsVBox/ActionGrid/BtnBack", Fallback = "Panel/VBox/ContentColumns/LeftColumn/ActionsPanel/ActionsVBox/ActionGrid/BtnBack")]
	private Button _btnBack = null!;

	[Bind("Panel/VBox/BodyTabs/AmmoTab/AmmoPanel/AmmoVBox/LblAmmo", Fallback = "Panel/VBox/ContentColumns/RightColumn/AmmoPanel/AmmoVBox/LblAmmo")]
	private Label _lblAmmo = null!;
	[Bind("Panel/VBox/BodyTabs/AmmoTab/AmmoPanel/AmmoVBox/AmmoActions/BtnRefillAll", Fallback = "Panel/VBox/ContentColumns/RightColumn/AmmoPanel/AmmoVBox/AmmoActions/BtnRefillAll")]
	private Button _btnRefillAll = null!;
	[Bind("Panel/VBox/BodyTabs/AmmoTab/AmmoPanel/AmmoVBox/AmmoScroll/VBoxAmmoRows", Fallback = "Panel/VBox/ContentColumns/RightColumn/AmmoPanel/AmmoVBox/AmmoScroll/VBoxAmmoRows")]
	private VBoxContainer _vboxAmmoRows = null!;

	private const string WorkshopBackgroundPath = "res://Assets/Images/Workshop/Workshop1.png";
	private Texture2D? _workshopBackground;

	private VehicleInstanceState? _vehicle;
	private VehicleDefinition? _vdef;
	private VehicleShowcaseCard? _showcaseCard;

	/// <summary>Compact spec line under the Engine dropdown (created in code; additive decision support).</summary>
	private Label? _lblEngineInfo;

	// mountId -> (weaponOpt, ammoOpt)
	private readonly Dictionary<string, (OptionButton weapon, OptionButton ammo)> _mountControls = new();

	private sealed record AmmoNeed(
		string AmmoId,
		string DisplayName,
		AmmoKind Kind,
		int Current,
		int Target,
		int Need,
		int UnitCostUsd,
		int MountCount,
		string UsedByText)
	{
		public int CostUsd => checked(Need * UnitCostUsd);
	}

	private sealed class AmmoAccumulator
	{
		public string AmmoId = "";
		public string DisplayName = "";
		public AmmoKind Kind = AmmoKind.Ballistic;
		public int Current;
		public int UnitCostUsd;
		public readonly List<string> UsedBy = new();
	}

	/// <summary>
	/// Unit price resolution (def JSON price → legacy override map → per-kind baseline) lives on
	/// <see cref="GameBalance.GetAmmoUnitPriceUsd"/> so the Workshop and the tournament pit-crew
	/// restock share one source of truth. Thin wrapper kept for the local call sites.
	/// </summary>
	private static int GetAmmoUnitCostUsd(AmmoDefinition? adef, string ammoId, AmmoKind kind)
		=> GameBalance.GetAmmoUnitPriceUsd(adef, ammoId, kind);

	/// <summary>
	/// Human-readable armor-matchup hint per <see cref="AmmoDefinition.ArmorPenetrationTag"/>.
	/// Percentages mirror <see cref="DamageMatrix.GetMultiplier"/> (Ball 1.0/0.70/1.0,
	/// AP 1.25/1.1/0.8, HE 0.9/1.25/0.7 vs Steel/Composite/Reactive) — keep the two in sync.
	/// </summary>
	private static string GetMatchupHint(string? penetrationTag)
	{
		if (string.IsNullOrWhiteSpace(penetrationTag))
			return "Neutral — no armor matchup bonus or penalty.";

		if (penetrationTag.Equals(DamageMatrix.TagBall, StringComparison.OrdinalIgnoreCase))
			return "Ball — baseline round; badly blunted by Composite (-30%).";
		if (penetrationTag.Equals(DamageMatrix.TagAp, StringComparison.OrdinalIgnoreCase))
			return "AP — shreds Steel (+25%), bites Composite (+10%), blunted by Reactive (-20%).";
		if (penetrationTag.Equals(DamageMatrix.TagHe, StringComparison.OrdinalIgnoreCase))
			return "HE — cracks Composite (+25%), shed by Steel (-10%), eaten by Reactive (-30%).";

		return "Neutral — no armor matchup bonus or penalty.";
	}

	private static void UpdateAmmoHint(DefDatabase defs, OptionButton optAmmo, Label hint)
	{
		var ammoId = GetSelectedMetadata(optAmmo);
		if (string.IsNullOrWhiteSpace(ammoId) || !defs.Ammo.TryGetValue(ammoId, out var adef))
		{
			hint.Text = "";
			hint.Visible = false;
			return;
		}

		hint.Visible = true;
		hint.Text = GetMatchupHint(adef.ArmorPenetrationTag);
	}

	public override void _Ready()
	{
		GameUiTheme.ApplyToTree(this);
		SceneAutoBinder.Apply(this, nameof(WorkshopView));
		LoadWorkshopBackground();
		ConfigureWorkshopVisuals();
		ConfigureDrawerLayout();
		EnsureShowcaseCard();

		_optEngine.ItemSelected += _ => RefreshPlannedContext();
		_optComputer.ItemSelected += _ => RefreshPlannedContext();
		_btnApply.Pressed += ApplyAndSave;
		_btnBack.Pressed += Back;
		_btnRefillAll.Pressed += RefillAllAmmo;

		LoadActiveVehicleAndBuildUi();
	}


	public override void _ExitTree()
	{
		var viewport = GetViewport();
		if (viewport != null)
			viewport.SizeChanged -= ApplyDrawerLayout;
	}

	private void ConfigureDrawerLayout()
	{
		var viewport = GetViewport();
		if (viewport != null)
			viewport.SizeChanged += ApplyDrawerLayout;

		ApplyDrawerLayout();
		CallDeferred(nameof(ApplyDrawerLayout));
	}

	private void ApplyDrawerLayout()
		=> MenuDrawerLayout.Apply(_panel, this, MenuDrawerSpec.Workshop);

	private void LoadWorkshopBackground()
	{
		_workshopBackground = OptionalBackgroundPresenter.Apply(
			_bg,
			_bgImage,
			WorkshopBackgroundPath,
			GameUiTheme.BackgroundColor,
			_workshopBackground);
	}

	private void ConfigureWorkshopVisuals()
	{
		if (_panel != null)
			_panel.AddThemeStyleboxOverride("panel", GeneratedUiArt.CreatePremiumShellStyle());

		_contextPanel?.AddThemeStyleboxOverride("panel", GeneratedUiArt.CreatePremiumInsetStyle(selected: true));
		_activePanel?.AddThemeStyleboxOverride("panel", GeneratedUiArt.CreatePremiumSectionStyle(emphasized: true));
		_actionsPanel?.AddThemeStyleboxOverride("panel", GeneratedUiArt.CreatePremiumSectionStyle(gold: true));
		_systemsPanel?.AddThemeStyleboxOverride("panel", GeneratedUiArt.CreatePremiumSectionStyle());
		_mountsPanel?.AddThemeStyleboxOverride("panel", GeneratedUiArt.CreatePremiumSectionStyle());
		_ammoPanel?.AddThemeStyleboxOverride("panel", GeneratedUiArt.CreatePremiumSectionStyle());

		// Banner art off; display-font title carries the header (matches City/Garage).
		if (_headerArt != null) _headerArt.Visible = false;
		GameUiTheme.StyleHeading(GetNodeOrNull<Label>("Panel/VBox/LblTitle"), GameUiTheme.TitleFontSize + 12);
		GeneratedUiArt.ApplyIcon(_optEngine, "icon_engine", 20);
		GeneratedUiArt.ApplyIcon(_optComputer, "icon_targeting", 20);
		GeneratedUiArt.ApplyIcon(_btnRefillAll, "icon_ammo", 18);
		GeneratedUiArt.ApplyIcon(_btnApply, "icon_save", 22);
		GeneratedUiArt.ApplyIcon(_btnBack, "icon_back", 24);

		MenuDrawerLayout.StyleTabs(_bodyTabs);
		if (_bodyTabs != null && _bodyTabs.GetTabCount() >= 3)
		{
			_bodyTabs.SetTabTitle(0, "Overview");
			_bodyTabs.SetTabTitle(1, "Hardpoints");
			_bodyTabs.SetTabTitle(2, "Ammo / Loadout");
		}
	}

	private void EnsureShowcaseCard()
	{
		if (_showcaseHost == null)
			return;

		if (_showcaseCard != null && GodotObject.IsInstanceValid(_showcaseCard))
			return;

		_showcaseCard = new VehicleShowcaseCard
		{
			CustomMinimumSize = new Vector2(0f, 144f),
		};
		_showcaseHost.AddChild(_showcaseCard);
		GameUiTheme.ApplyToTree(_showcaseCard);
	}

	private void LoadActiveVehicleAndBuildUi()
	{
		var app = App.Instance;
		if (app == null)
		{
			_lblActive.Text = "Active: (App missing)";
			DisableAll();
			return;
		}

		var session = app.Services.Get<GameSession>();
		var defs = app.Services.Get<DefDatabase>();

		_vehicle = session.GetActiveVehicle();
		if (_vehicle == null)
		{
			_lblActive.Text = "Active: (none). Go to Garage and set an active vehicle.";
			_showcaseCard?.Clear("Active Vehicle", "Set an active vehicle in the garage before changing engines, computers, or weapon mounts.");
			DisableAll();
			return;
		}

		if (!defs.Vehicles.TryGetValue(_vehicle.DefinitionId, out _vdef))
		{
			_lblActive.Text = $"Active: (missing def '{_vehicle.DefinitionId}')";
			_showcaseCard?.Clear("Active Vehicle", "Vehicle definition data is missing for the active loadout target.");
			DisableAll();
			return;
		}

		_lblActive.Text = $"Active: {_vdef.DisplayName} · Class {_vdef.Class}";
		_showcaseCard?.Configure("Active Vehicle", _vehicle, _vdef, defs, "Current workshop target. Save Loadout commits drivetrain, computer, and mount changes.");

		BuildEngineOptions(defs);
		BuildComputerOptions(defs);
		BuildMountOptions(defs);
		RefreshAmmoUi(session, defs);
		RefreshContextSummary(defs);
	}

	private void DisableAll()
	{
		_showcaseCard?.Clear("Active Vehicle", "Set an active vehicle in the garage before opening the workshop.");
		_optEngine.Disabled = true;
		_optComputer.Disabled = true;
		_btnApply.Disabled = true;
		_btnRefillAll.Disabled = true;
		if (_lblAmmo != null)
			_lblAmmo.Text = "Mounted weapon ammo: unavailable";
		if (_lblContextSummary != null)
			_lblContextSummary.Text = "No active vehicle selected. Set one in the garage before opening the workshop.";
		RebuildAmmoRows(new List<AmmoNeed>(), 0);
	}

	private List<AmmoNeed> ComputeAmmoNeedsForInstalledWeapons(DefDatabase defs, VehicleInstanceState vehicle)
	{
		var byAmmoId = new Dictionary<string, AmmoAccumulator>(StringComparer.OrdinalIgnoreCase);
		var inv = vehicle.AmmoInventory ?? new Dictionary<string, int>();

		foreach (var kv in vehicle.InstalledWeaponsByMountId)
		{
			var mountId = kv.Key;
			var inst = kv.Value;
			if (inst == null || string.IsNullOrWhiteSpace(inst.WeaponId))
				continue;
			if (!defs.Weapons.TryGetValue(inst.WeaponId, out var wdef))
				continue;

			var ammoIds = wdef.AmmoTypeIds ?? Array.Empty<string>();
			if (ammoIds.Length == 0)
				continue;

			var ammoId = inst.SelectedAmmoId;
			if (string.IsNullOrWhiteSpace(ammoId))
				ammoId = ammoIds[0];
			if (string.IsNullOrWhiteSpace(ammoId))
				continue;

			if (!byAmmoId.TryGetValue(ammoId!, out var acc))
			{
				var kind = defs.Ammo.TryGetValue(ammoId!, out var adef) ? adef.AmmoKind : AmmoKind.Ballistic;
				var unitCost = GetAmmoUnitCostUsd(adef, ammoId!, kind);
				inv.TryGetValue(ammoId!, out var current);
				acc = new AmmoAccumulator
				{
					AmmoId = ammoId!,
					DisplayName = defs.Ammo.TryGetValue(ammoId!, out var ammoDef) ? ammoDef.DisplayName : ammoId!,
					Kind = kind,
					Current = current,
					UnitCostUsd = unitCost,
				};
				byAmmoId[ammoId!] = acc;
			}

			acc.UsedBy.Add($"{mountId}: {wdef.DisplayName}");
		}

		// Reserves the loadout no longer feeds (e.g. the player just switched an MG from Ball to
		// AP): keep them visible with Need 0 so switching ammo types never looks like it deleted
		// rounds the player paid for. They stay in AmmoInventory and still count toward mass.
		foreach (var kv in inv)
		{
			if (kv.Value <= 0 || byAmmoId.ContainsKey(kv.Key))
				continue;

			var kind = defs.Ammo.TryGetValue(kv.Key, out var adef) ? adef.AmmoKind : AmmoKind.Ballistic;
			byAmmoId[kv.Key] = new AmmoAccumulator
			{
				AmmoId = kv.Key,
				DisplayName = defs.Ammo.TryGetValue(kv.Key, out var ammoDef) ? ammoDef.DisplayName : kv.Key,
				Kind = kind,
				Current = kv.Value,
				UnitCostUsd = GetAmmoUnitCostUsd(adef, kv.Key, kind),
			};
		}

		return byAmmoId.Values
			.Select(acc =>
			{
				var inLoadout = acc.UsedBy.Count > 0;
				var (targetPerMount, _) = GameBalance.GetAmmoRefillPolicy(acc.Kind);
				// A retained reserve targets its current stock: nothing to buy, nothing lost.
				var target = inLoadout ? Math.Max(targetPerMount, targetPerMount * acc.UsedBy.Count) : acc.Current;
				var need = Math.Max(0, target - acc.Current);
				return new AmmoNeed(
					acc.AmmoId,
					acc.DisplayName,
					acc.Kind,
					acc.Current,
					target,
					need,
					acc.UnitCostUsd,
					acc.UsedBy.Count,
					string.Join(", ", acc.UsedBy));
			})
			.OrderBy(n => n.MountCount == 0) // active feeds first, retained reserves last
			.ThenBy(n => n.Kind)
			.ThenBy(n => n.DisplayName)
			.ToList();
	}

	private void BuildEngineOptions(DefDatabase defs)
	{
		_optEngine.Clear();
		_optEngine.AddItem("(none)");
		_optEngine.SetItemMetadata(0, "");

		var session = App.Instance?.Services.Get<GameSession>();
		var idx = 1;
		foreach (var eng in defs.Engines.Values.OrderBy(e => e.DisplayName))
		{
			if (_vdef == null)
				continue;

			if (eng.AllowedVehicleClasses != null && eng.AllowedVehicleClasses.Length > 0)
			{
				if (!eng.AllowedVehicleClasses.Contains(_vdef.Class))
					continue;
			}

			// Economy: only owned parts (or the one already installed) can be fitted.
			var installedHere = string.Equals(_vehicle?.InstalledEngineId, eng.Id, StringComparison.OrdinalIgnoreCase);
			if (!installedHere && (session?.GetOwnedPartCount(eng.Id) ?? 0) <= 0)
				continue;

			_optEngine.AddItem($"{eng.DisplayName} [{eng.FuelType}]");
			_optEngine.SetItemMetadata(idx, eng.Id);
			idx++;
		}

		SelectByMetadata(_optEngine, _vehicle?.InstalledEngineId ?? "");
	}

	private void BuildComputerOptions(DefDatabase defs)
	{
		_optComputer.Clear();
		_optComputer.AddItem("(none)");
		_optComputer.SetItemMetadata(0, "");

		var session = App.Instance?.Services.Get<GameSession>();
		var idx = 1;
		foreach (var c in defs.Computers.Values.OrderBy(c => c.DisplayName))
		{
			var installedHere = string.Equals(_vehicle?.InstalledComputerId, c.Id, StringComparison.OrdinalIgnoreCase);
			if (!installedHere && (session?.GetOwnedPartCount(c.Id) ?? 0) <= 0)
				continue;

			_optComputer.AddItem($"{c.DisplayName} (Groups {c.MaxActiveWeaponGroups}, AutoAim {c.AutoAimSlots})");
			_optComputer.SetItemMetadata(idx, c.Id);
			idx++;
		}

		SelectByMetadata(_optComputer, _vehicle?.InstalledComputerId ?? "");
	}

	private void BuildMountOptions(DefDatabase defs)
	{
		if (_vboxMounts == null || _vdef == null || _vehicle == null)
			return;

		foreach (var child in _vboxMounts.GetChildren())
			(child as Node)?.QueueFree();

		_mountControls.Clear();

		if (_vdef.MountPoints.Count == 0)
		{
			var lbl = new Label { Text = "(No mounts on this vehicle)" };
			_vboxMounts.AddChild(lbl);
			return;
		}

		foreach (var mount in _vdef.MountPoints)
		{
			var card = new PanelContainer
			{
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			};
			card.AddThemeStyleboxOverride("panel", GeneratedUiArt.CreateCardStyle());

			var root = new VBoxContainer();
			root.AddThemeConstantOverride("separation", 8);
			card.AddChild(root);

			var header = new HBoxContainer();
			header.AddThemeConstantOverride("separation", 12);
			root.AddChild(header);

			var iconShell = new PanelContainer
			{
				CustomMinimumSize = new Vector2(42f, 42f),
			};
			iconShell.AddThemeStyleboxOverride("panel", GeneratedUiArt.CreatePremiumBadgeStyle());
			var iconCenter = new CenterContainer();
			iconShell.AddChild(iconCenter);
			iconCenter.AddChild(CreateIconRect(GeneratedUiArt.LoadMountIcon(mount.MountLocation), new Vector2(24f, 24f)));
			header.AddChild(iconShell);

			var info = new VBoxContainer
			{
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			};
			info.AddThemeConstantOverride("separation", 6);
			header.AddChild(info);

			var title = new Label
			{
				Text = $"{mount.MountId} · {mount.MountLocation} mount",
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			};
			title.AddThemeColorOverride("font_color", GameUiTheme.AccentGoldColor);
			title.AddThemeFontSizeOverride("font_size", 16);
			info.AddChild(title);

			var badgeRow = new HBoxContainer
			{
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			};
			badgeRow.AddThemeConstantOverride("separation", 8);
			info.AddChild(badgeRow);
			badgeRow.AddChild(CreateBadge($"Arc {mount.ArcDegrees:0}°"));
			badgeRow.AddChild(CreateBadge(mount.CanAutoAim ? "Auto Aim" : "Fixed Arc", gold: !mount.CanAutoAim, success: mount.CanAutoAim));

			var detail = new Label
			{
				Text = mount.CanAutoAim
					? "Targeting computer can assist this hardpoint when a supported system is installed."
					: "This hardpoint fires on a fixed arc, so pick a weapon that matches your approach."
				,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			};
			detail.AddThemeColorOverride("font_color", GameUiTheme.TextMutedColor);
			detail.AddThemeFontSizeOverride("font_size", GameUiTheme.BaseFontSize - 1);
			info.AddChild(detail);

			var controls = new GridContainer
			{
				Columns = 2,
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			};
			controls.AddThemeConstantOverride("h_separation", 10);
			controls.AddThemeConstantOverride("v_separation", 8);
			root.AddChild(controls);

			var optWeapon = new OptionButton
			{
				CustomMinimumSize = new Vector2(220, 40),
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			};
			var optAmmo = new OptionButton
			{
				CustomMinimumSize = new Vector2(220, 40),
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			};
			GeneratedUiArt.ApplyIcon(optWeapon, "icon_weapon_front", 16);
			GeneratedUiArt.ApplyIcon(optAmmo, "icon_ammo", 16);

			controls.AddChild(CreateLabeledField("Installed Weapon", optWeapon));
			controls.AddChild(CreateLabeledField("Ammo Feed", optAmmo));

			// Armor-matchup hint for the selected ammo feed (pre-fight rock-paper-scissors readout).
			var ammoHint = new Label
			{
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			};
			ammoHint.AddThemeColorOverride("font_color", GameUiTheme.TextMutedColor);
			ammoHint.AddThemeFontSizeOverride("font_size", GameUiTheme.BaseFontSize - 1);
			root.AddChild(ammoHint);

			_vboxMounts.AddChild(card);

			optWeapon.AddItem("(none)");
			optWeapon.SetItemMetadata(0, "");

			_vehicle.InstalledWeaponsByMountId.TryGetValue(mount.MountId, out var installed);
			var currentWeaponId = installed?.WeaponId ?? "";
			var currentAmmoId = installed?.SelectedAmmoId ?? "";

			// Mount-location rules (master spec): only offer weapons allowed on this mount location.
			// A legacy/imported loadout may still have a now-disallowed weapon installed here — keep it
			// visible (flagged) so the player can see it and swap it out rather than silently losing it.
			// Economy: weapons must be OWNED (Parts Store) or already installed somewhere on this vehicle.
			var sessionForOwnership = App.Instance?.Services.Get<GameSession>();
			var installedAnywhere = new HashSet<string>(
				_vehicle.InstalledWeaponsByMountId.Values.Select(iw => iw.WeaponId),
				StringComparer.OrdinalIgnoreCase);
			var wi = 1;
			foreach (var w in defs.Weapons.Values.OrderBy(w => w.DisplayName))
			{
				var allowed = w.IsMountLocationAllowed(mount.MountLocation);
				if (!allowed && !string.Equals(w.Id, currentWeaponId, StringComparison.OrdinalIgnoreCase))
					continue;

				var ownedCount = sessionForOwnership?.GetOwnedPartCount(w.Id) ?? 0;
				if (ownedCount <= 0 && !installedAnywhere.Contains(w.Id))
					continue;

				var restriction = w.AllowedMountLocations.Length > 0
					? $" · {string.Join("/", w.AllowedMountLocations).ToLowerInvariant()} only"
					: "";
				var prefix = allowed ? "" : "[!] ";
				optWeapon.AddItem($"{prefix}{w.DisplayName} [{w.WeaponType}]{restriction}");
				optWeapon.SetItemMetadata(wi, w.Id);
				wi++;
			}

			SelectByMetadata(optWeapon, currentWeaponId);
			PopulateAmmo(defs, optAmmo, currentWeaponId, currentAmmoId);
			UpdateAmmoHint(defs, optAmmo, ammoHint);

			optWeapon.ItemSelected += _ =>
			{
				var wId = GetSelectedMetadata(optWeapon);
				PopulateAmmo(defs, optAmmo, wId, "");
				UpdateAmmoHint(defs, optAmmo, ammoHint);
				RefreshCurrentAmmoUi(defs);
				RefreshContextSummary(defs);
			};
			optAmmo.ItemSelected += _ =>
			{
				UpdateAmmoHint(defs, optAmmo, ammoHint);
				RefreshCurrentAmmoUi(defs);
				RefreshContextSummary(defs);
			};

			_mountControls[mount.MountId] = (optWeapon, optAmmo);
			GameUiTheme.ApplyToTree(card);
		}
	}

	private static void PopulateAmmo(DefDatabase defs, OptionButton optAmmo, string weaponId, string preferredAmmoId)
	{
		optAmmo.Clear();

		if (string.IsNullOrWhiteSpace(weaponId) || !defs.Weapons.TryGetValue(weaponId, out var wdef))
		{
			optAmmo.AddItem("(n/a)");
			optAmmo.SetItemMetadata(0, "");
			optAmmo.Disabled = true;
			return;
		}

		optAmmo.Disabled = false;

		var ammoIds = wdef.AmmoTypeIds ?? Array.Empty<string>();
		if (ammoIds.Length == 0)
		{
			optAmmo.AddItem("(no ammo)");
			optAmmo.SetItemMetadata(0, "");
			return;
		}

		var idx = 0;
		foreach (var aId in ammoIds)
		{
			if (!defs.Ammo.TryGetValue(aId, out var adef))
			{
				optAmmo.AddItem($"(missing ammo) {aId}");
				optAmmo.SetItemMetadata(idx, aId);
			}
			else
			{
				var tag = string.IsNullOrWhiteSpace(adef.ArmorPenetrationTag)
					? adef.AmmoKind.ToString()
					: adef.ArmorPenetrationTag;
				optAmmo.AddItem($"{adef.DisplayName} [{tag}] · ${GetAmmoUnitCostUsd(adef, aId, adef.AmmoKind)} ea");
				optAmmo.SetItemMetadata(idx, aId);
			}
			idx++;
		}

		SelectByMetadata(optAmmo, preferredAmmoId);
		if (optAmmo.Selected < 0 && optAmmo.ItemCount > 0)
			optAmmo.Select(0);
	}

	private void RefreshCurrentAmmoUi(DefDatabase defs)
	{
		var app = App.Instance;
		if (app == null)
			return;

		var session = app.Services.Get<GameSession>();
		_vehicle = session.GetActiveVehicle();
		RefreshAmmoUi(session, defs);
		RefreshContextSummary(defs);
	}

	private void RefreshPlannedContext()
	{
		var app = App.Instance;
		if (app == null)
			return;

		RefreshContextSummary(app.Services.Get<DefDatabase>());
	}

	/// <summary>
	/// Lazily inserts the per-engine spec line directly beneath the EngineRow so the dropdown's
	/// name-only entries get decision support (power/torque/fuel/efficiency/tow capacity).
	/// </summary>
	private void EnsureEngineInfoLabel()
	{
		if (_lblEngineInfo != null && GodotObject.IsInstanceValid(_lblEngineInfo))
			return;

		var engineRow = _optEngine?.GetParent() as Control;
		var systemsVBox = engineRow?.GetParent() as Control;
		if (engineRow == null || systemsVBox == null)
			return;

		_lblEngineInfo = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		_lblEngineInfo.AddThemeColorOverride("font_color", GameUiTheme.TextMutedColor);
		_lblEngineInfo.AddThemeFontSizeOverride("font_size", GameUiTheme.BaseFontSize - 1);
		systemsVBox.AddChild(_lblEngineInfo);
		systemsVBox.MoveChild(_lblEngineInfo, engineRow.GetIndex() + 1);
	}

	/// <summary>
	/// Updates the engine spec line for the CURRENTLY SELECTED (planned) engine. Tow capacity uses
	/// the same shared math the towing system applies (VehicleMassMath.ComputeTowCapacityKg), so the
	/// Diesel V12's whole towing pitch is finally visible where the buying decision happens.
	/// </summary>
	private void UpdateEngineInfoLine(DefDatabase defs)
	{
		EnsureEngineInfoLabel();
		if (_lblEngineInfo == null)
			return;

		var engineId = GetSelectedMetadata(_optEngine);
		if (string.IsNullOrWhiteSpace(engineId) || !defs.Engines.TryGetValue(engineId, out var eng))
		{
			_lblEngineInfo.Text = "No engine selected — the vehicle cannot drive or tow.";
			return;
		}

		var towKg = VehicleMassMath.ComputeTowCapacityKg(eng);
		var torqueText = eng.TorqueNm is float t && t > 0f ? $"{t:N0} Nm" : "torque n/a";
		_lblEngineInfo.Text =
			$"{eng.PowerKw:0} kW · {torqueText} · {eng.FuelType} · Efficiency ×{eng.Efficiency:0.00} · Tow capacity {towKg:N0} kg";
	}

	private void RefreshContextSummary(DefDatabase defs)
	{
		UpdateEngineInfoLine(defs);

		if (_lblContextSummary == null)
			return;
		if (_vehicle == null || _vdef == null)
		{
			_lblContextSummary.Text = "No active vehicle selected. Set one in the garage before opening the workshop.";
			return;
		}

		var plannedVehicle = GetPlannedVehicleSnapshot() ?? _vehicle;
		var displayName = VehiclePresentation.GetDisplayName(plannedVehicle, _vdef);
		var condition = VehicleRecoveryValueMath.ComputeConditionPercent(_vdef, plannedVehicle);
		var weaponCount = VehiclePresentation.CountInstalledWeapons(plannedVehicle);
		var engineId = GetSelectedMetadata(_optEngine);
		var computerId = GetSelectedMetadata(_optComputer);
		var engineName = string.IsNullOrWhiteSpace(engineId)
			? "Stock / none"
			: defs.Engines.TryGetValue(engineId, out var engineDef) ? engineDef.DisplayName : engineId;
		var computerName = string.IsNullOrWhiteSpace(computerId)
			? "No computer"
			: defs.Computers.TryGetValue(computerId, out var computerDef) ? computerDef.DisplayName : computerId;
		var ammoFeeds = ComputeAmmoNeedsForInstalledWeapons(defs, plannedVehicle).Count(n => n.MountCount > 0);

		// Live weight/performance preview for the *planned* loadout (master-spec: customization is
		// constrained by weight). Uses the same shared math the arena pawn applies, so the predicted
		// top-speed effect matches what the player will actually feel in a match.
		var mass = VehicleMassMath.ComputeBreakdown(_vdef, plannedVehicle, defs);
		// Same power-to-weight math the arena pawn applies (planned engine, not just installed).
		var plannedPowerKw = !string.IsNullOrWhiteSpace(engineId) && defs.Engines.TryGetValue(engineId, out var plannedEngine)
			? plannedEngine.PowerKw
			: 0f;
		var speedPct = Mathf.RoundToInt(VehicleMassMath.ComputeSpeedFactor(_vdef.BaseMassKg, mass.TotalKg, plannedPowerKw) * 100f);
		var massLine = $"Mass {mass.TotalKg:0} kg (chassis {mass.VehicleKg:0} · weapons {mass.WeaponsKg:0} · ammo {mass.AmmoKg:0}) · Est. top speed {speedPct}% of stock";

		_lblContextSummary.Text = $"{displayName} · Class {_vdef.Class} · Condition {condition}% · Weapons {weaponCount} · Engine {engineName} · Computer {computerName} · Ammo feeds {ammoFeeds}\n{massLine}";
	}

	private VehicleInstanceState? GetPlannedVehicleSnapshot()
	{
		if (_vehicle == null)
			return null;

		var installs = new Dictionary<string, InstalledWeaponState>();
		foreach (var kv in _mountControls)
		{
			var mountId = kv.Key;
			var weaponId = GetSelectedMetadata(kv.Value.weapon);
			if (string.IsNullOrWhiteSpace(weaponId))
				continue;

			string? ammoId = GetSelectedMetadata(kv.Value.ammo);
			if (string.IsNullOrWhiteSpace(ammoId))
				ammoId = null;

			installs[mountId] = new InstalledWeaponState
			{
				WeaponId = weaponId,
				SelectedAmmoId = ammoId
			};
		}

		return _vehicle with { InstalledWeaponsByMountId = installs };
	}

	private void RefreshAmmoUi(GameSession session, DefDatabase defs)
	{
		if (_lblAmmo == null)
			return;

		if (_vehicle == null)
		{
			_lblAmmo.Text = "Mounted weapon ammo: unavailable";
			_btnRefillAll.Disabled = true;
			RebuildAmmoRows(new List<AmmoNeed>(), session.Save.Player.MoneyUsd);
			return;
		}

		var plannedVehicle = GetPlannedVehicleSnapshot() ?? _vehicle;
		var needs = ComputeAmmoNeedsForInstalledWeapons(defs, plannedVehicle);
		var money = session.Save.Player.MoneyUsd;
		var totalNeed = needs.Sum(n => n.Need);
		var totalCost = needs.Sum(n => n.CostUsd);

		if (needs.Count == 0)
		{
			_lblAmmo.Text = $"Mounted weapon ammo\nInstall a weapon with ammo to purchase resupplies.    Money: ${money}";
			_btnRefillAll.Text = "Refill All";
			_btnRefillAll.Disabled = true;
			RebuildAmmoRows(needs, money);
			return;
		}

		var loadoutCount = needs.Count(n => n.MountCount > 0);
		var reserveCount = needs.Count - loadoutCount;
		var reserveNote = reserveCount > 0 ? $" · {reserveCount} retained reserve(s)" : "";
		_lblAmmo.Text = $"Mounted weapon ammo\n{loadoutCount} ammo type(s) in current loadout{reserveNote}. Refill all cost: ${totalCost}    Money: ${money}";
		_btnRefillAll.Text = totalNeed > 0 ? $"Refill All Mounted Ammo (${totalCost})" : "Ammo Full";
		_btnRefillAll.Disabled = totalNeed <= 0 || money < totalCost;
		RebuildAmmoRows(needs, money);
	}

	private void RebuildAmmoRows(List<AmmoNeed> needs, int money)
	{
		if (_vboxAmmoRows == null)
			return;

		foreach (var child in _vboxAmmoRows.GetChildren())
			(child as Node)?.QueueFree();

		if (needs.Count == 0)
		{
			var empty = new Label
			{
				Text = "No ammo-consuming mounted weapons in the current loadout.",
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
			};
			_vboxAmmoRows.AddChild(empty);
			GameUiTheme.ApplyTo(empty);
			return;
		}

		var app = App.Instance;
		var defs = app?.Services.Get<DefDatabase>();
		if (defs == null)
			return;

		foreach (var need in needs)
		{
			var card = new PanelContainer
			{
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			};
			card.AddThemeStyleboxOverride("panel", GeneratedUiArt.CreateCardStyle());
			var root = new VBoxContainer();
			root.AddThemeConstantOverride("separation", 8);
			card.AddChild(root);

			var header = new HBoxContainer();
			header.AddThemeConstantOverride("separation", 12);
			root.AddChild(header);

			var titleColumn = new VBoxContainer
			{
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			};
			titleColumn.AddThemeConstantOverride("separation", 6);
			header.AddChild(CreateIconRect(GeneratedUiArt.Load("icon_ammo"), new Vector2(28f, 28f)));
			header.AddChild(titleColumn);

			var title = new Label
			{
				Text = $"{need.DisplayName} [{need.Kind}]",
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			};
			title.AddThemeFontSizeOverride("font_size", 17);
			titleColumn.AddChild(title);

			var badgeRow = new HBoxContainer
			{
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			};
			badgeRow.AddThemeConstantOverride("separation", 8);
			titleColumn.AddChild(badgeRow);
			if (need.MountCount > 0)
				badgeRow.AddChild(CreateBadge($"{need.MountCount} mount{(need.MountCount == 1 ? string.Empty : "s")}"));
			else
				badgeRow.AddChild(CreateBadge("Not in loadout", gold: true));
			badgeRow.AddChild(CreateBadge($"${need.UnitCostUsd} ea", gold: true));
			if (need.MountCount > 0)
				badgeRow.AddChild(CreateBadge(need.Need > 0 ? $"Need {need.Need}" : "Reserve Full", success: need.Need <= 0, gold: need.Need > 0));
			else
				badgeRow.AddChild(CreateBadge("Reserve kept", success: true));

			var qty = new Label
			{
				Text = $"Current {need.Current} / Target {need.Target}",
				HorizontalAlignment = HorizontalAlignment.Right,
				VerticalAlignment = VerticalAlignment.Center,
				CustomMinimumSize = new Vector2(180, 0),
			};
			header.AddChild(qty);

			var progress = new ProgressBar
			{
				MinValue = 0,
				MaxValue = Math.Max(1, need.Target),
				Value = Math.Clamp(need.Current, 0, Math.Max(1, need.Target)),
				CustomMinimumSize = new Vector2(0, 20),
				ShowPercentage = false,
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			};
			root.AddChild(progress);

			var details = new Label
			{
				Text = need.MountCount <= 0
					? "Not fed to any mounted weapon in the planned loadout. The reserve stays on this vehicle (and still counts toward mass) until an Ammo Feed selects it again."
					: need.Need > 0
						? $"Mounted on: {need.UsedByText}\nNeed {need.Need} more rounds to reach the target reserve for this loadout."
						: $"Mounted on: {need.UsedByText}\nThis ammo reserve is already at the current workshop target.",
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
			};
			details.AddThemeColorOverride("font_color", GameUiTheme.TextMutedColor);
			root.AddChild(details);

			// Purchase actions only apply to ammo the planned loadout actually feeds; retained
			// reserves are display-only (no accidental stockpiling of a type nothing fires).
			if (need.MountCount > 0)
			{
				var actions = new GridContainer
				{
					Columns = 4,
					SizeFlagsHorizontal = SizeFlags.ExpandFill,
				};
				actions.AddThemeConstantOverride("h_separation", 8);
				actions.AddThemeConstantOverride("v_separation", 8);
				root.AddChild(actions);

				foreach (var qtyOption in GetPurchaseOptions(need.Kind))
				{
					var count = qtyOption;
					var btn = new Button
					{
						Text = $"+{count} (${count * need.UnitCostUsd})",
						CustomMinimumSize = new Vector2(0, 42),
						SizeFlagsHorizontal = SizeFlags.ExpandFill,
					};
					GeneratedUiArt.ApplyIcon(btn, "icon_ammo", 16);
					btn.Disabled = money < count * need.UnitCostUsd;
					btn.Pressed += () => BuySpecificAmmo(need.AmmoId, count, need.UnitCostUsd, need.DisplayName, defs);
					actions.AddChild(btn);
				}

				var btnFill = new Button
				{
					Text = need.Need > 0 ? $"Fill (${need.CostUsd})" : "Full",
					CustomMinimumSize = new Vector2(0, 42),
					SizeFlagsHorizontal = SizeFlags.ExpandFill,
				};
				GeneratedUiArt.ApplyIcon(btnFill, "icon_ammo", 16);
				btnFill.Disabled = need.Need <= 0 || money < need.CostUsd;
				if (need.Need > 0)
					btnFill.Pressed += () => BuySpecificAmmo(need.AmmoId, need.Need, need.UnitCostUsd, need.DisplayName, defs);
				actions.AddChild(btnFill);
			}

			_vboxAmmoRows.AddChild(card);
			GameUiTheme.ApplyToTree(card);
		}
	}

	private static Control CreateLabeledField(string labelText, Control field)
	{
		var column = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		column.AddThemeConstantOverride("separation", 6);

		var label = new Label
		{
			Text = labelText,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		label.AddThemeColorOverride("font_color", GameUiTheme.TextMutedColor);
		label.AddThemeFontSizeOverride("font_size", 13);
		column.AddChild(label);
		column.AddChild(field);
		return column;
	}

	private static PanelContainer CreateBadge(string text, bool gold = false, bool success = false)
	{
		var shell = new PanelContainer();
		shell.AddThemeStyleboxOverride("panel", GeneratedUiArt.CreatePremiumBadgeStyle(gold: gold, success: success, warning: gold && !success));

		var label = new Label
		{
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
		};
		label.AddThemeFontSizeOverride("font_size", 12);
		label.AddThemeColorOverride("font_color", success ? GameUiTheme.SuccessColor : gold ? GameUiTheme.AccentGoldColor : GameUiTheme.AccentCyanColor);
		shell.AddChild(label);
		return shell;
	}

	private static TextureRect CreateIconRect(Texture2D? texture, Vector2 size)
	{
		var rect = new TextureRect
		{
			Texture = texture,
			CustomMinimumSize = size,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		rect.Set("expand_mode", 1);
		rect.Set("stretch_mode", 6);
		return rect;
	}

	private static int[] GetPurchaseOptions(AmmoKind kind)
		=> kind switch
		{
			AmmoKind.Guided => new[] { 1, 5, 10 },
			AmmoKind.Explosive => new[] { 1, 5, 10 },
			AmmoKind.Mine => new[] { 1, 2, 4 },
			_ => new[] { 10, 50, 200 },
		};

	private void BuySpecificAmmo(string ammoId, int count, int unitCostUsd, string displayName, DefDatabase defs)
	{
		var app = App.Instance;
		if (app == null || _vehicle == null)
			return;

		var session = app.Services.Get<GameSession>();
		if (!session.TryBuyAmmoForActiveVehicle(ammoId, count, unitCostUsd, out var err))
		{
			UiSfx.Play("error");
			GD.Print($"[Workshop] Buy ammo failed for {ammoId}: {err}");
			RefreshAmmoUi(session, defs);
			return;
		}

		_vehicle = session.GetActiveVehicle();
		UiSfx.Play("purchase");
		GD.Print($"[Workshop] Bought +{count} {displayName}.");
		RefreshAmmoUi(session, defs);
	}

	private void RefillAllAmmo()
	{
		var app = App.Instance;
		if (app == null || _vehicle == null)
			return;

		var session = app.Services.Get<GameSession>();
		var defs = app.Services.Get<DefDatabase>();
		var plannedVehicle = GetPlannedVehicleSnapshot() ?? _vehicle;

		var needs = ComputeAmmoNeedsForInstalledWeapons(defs, plannedVehicle)
			.Where(n => n.Need > 0)
			.ToList();
		if (needs.Count == 0)
		{
			RefreshAmmoUi(session, defs);
			return;
		}

		var totalCost = needs.Sum(n => n.CostUsd);
		var money = session.Save.Player.MoneyUsd;
		if (money < totalCost)
		{
			UiSfx.Play("error");
			GD.Print($"[Workshop] Refill all ammo failed: need ${totalCost}.");
			RefreshAmmoUi(session, defs);
			return;
		}

		var allSucceeded = true;
		foreach (var n in needs)
		{
			if (!session.TryBuyAmmoForActiveVehicle(n.AmmoId, n.Need, n.UnitCostUsd, out var err))
			{
				allSucceeded = false;
				GD.Print($"[Workshop] Refill all ammo failed on {n.AmmoId}: {err}");
				break;
			}
		}

		UiSfx.Play(allSucceeded ? "purchase" : "error");
		_vehicle = session.GetActiveVehicle();
		GD.Print("[Workshop] Refilled ammo for all mounted weapons.");
		RefreshAmmoUi(session, defs);
	}

	private void ApplyAndSave()
	{
		var app = App.Instance;
		if (app == null || _vehicle == null)
			return;

		var session = app.Services.Get<GameSession>();
		var defs = app.Services.Get<DefDatabase>();

		string? engineId = GetSelectedMetadata(_optEngine);
		if (string.IsNullOrWhiteSpace(engineId))
			engineId = null;

		string? computerId = GetSelectedMetadata(_optComputer);
		if (string.IsNullOrWhiteSpace(computerId))
			computerId = null;

		var installs = new Dictionary<string, InstalledWeaponState>();
		foreach (var kv in _mountControls)
		{
			var mountId = kv.Key;
			var weaponId = GetSelectedMetadata(kv.Value.weapon);
			if (string.IsNullOrWhiteSpace(weaponId))
				continue;

			string? ammoId = GetSelectedMetadata(kv.Value.ammo);
			if (string.IsNullOrWhiteSpace(ammoId))
				ammoId = null;

			installs[mountId] = new InstalledWeaponState
			{
				WeaponId = weaponId,
				SelectedAmmoId = ammoId
			};
		}

		// Economy: installing a part consumes one from PartsInventory; uninstalling returns it.
		// Compare id-multisets so moving a weapon between mounts nets zero.
		var oldCounts = CountPartIds(_vehicle.InstalledWeaponsByMountId.Values.Select(iw => iw.WeaponId), _vehicle.InstalledEngineId, _vehicle.InstalledComputerId);
		var newCounts = CountPartIds(installs.Values.Select(iw => iw.WeaponId), engineId, computerId);

		var additions = new List<string>();
		var removals = new List<string>();
		foreach (var id in newCounts.Keys.Union(oldCounts.Keys, StringComparer.OrdinalIgnoreCase))
		{
			oldCounts.TryGetValue(id, out var before);
			newCounts.TryGetValue(id, out var after);
			for (var i = 0; i < after - before; i++) additions.Add(id);
			for (var i = 0; i < before - after; i++) removals.Add(id);
		}

		foreach (var id in additions)
		{
			if (!session.TryConsumeOwnedPart(id))
			{
				var name = defs.Weapons.TryGetValue(id, out var wd) ? wd.DisplayName
					: defs.Engines.TryGetValue(id, out var ed) ? ed.DisplayName
					: defs.Computers.TryGetValue(id, out var cd) ? cd.DisplayName : id;
				UiSfx.Play("error");
				GD.PrintErr($"[Workshop] Cannot install {name}: not owned. Buy it at the city Parts Store.");
				if (_lblContextSummary != null)
					_lblContextSummary.Text = $"Cannot install {name}: you don't own one. Buy it at the city Parts Store.";
				// Roll back any parts consumed so far in this apply.
				for (var i = 0; i < additions.IndexOf(id); i++)
					session.ReturnPartToInventory(additions[i]);
				LoadActiveVehicleAndBuildUi();
				return;
			}
		}
		foreach (var id in removals)
			session.ReturnPartToInventory(id);

		var updated = _vehicle with
		{
			InstalledEngineId = engineId,
			InstalledComputerId = computerId,
			InstalledWeaponsByMountId = installs
		};

		session.UpdateVehicle(updated);
		_vehicle = session.GetActiveVehicle() ?? updated;
		UiSfx.Play("confirm");
		GD.Print("[Workshop] Applied changes to active vehicle.");
		RefreshAmmoUi(session, defs);
	}

	private static Dictionary<string, int> CountPartIds(IEnumerable<string> weaponIds, string? engineId, string? computerId)
	{
		var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		void Add(string? id)
		{
			if (string.IsNullOrWhiteSpace(id)) return;
			counts.TryGetValue(id!, out var n);
			counts[id!] = n + 1;
		}
		foreach (var w in weaponIds) Add(w);
		Add(engineId);
		Add(computerId);
		return counts;
	}

	private void Back()
	{
		var app = App.Instance;
		if (app == null)
			return;
		if (!app.Services.TryGet<IGameNavigator>(out var nav) || nav == null)
		{
			GD.PrintErr("[WorkshopView] IGameNavigator not registered (cannot navigate back to CityShell).");
			return;
		}

		nav.ToCityShell(this);
	}

	private static void SelectByMetadata(OptionButton opt, string desired)
	{
		if (opt.ItemCount == 0)
			return;

		for (var i = 0; i < opt.ItemCount; i++)
		{
			var md = opt.GetItemMetadata(i).AsString();
			if (string.Equals(md, desired ?? "", StringComparison.OrdinalIgnoreCase))
			{
				opt.Select(i);
				return;
			}
		}

		opt.Select(0);
	}

	private static string GetSelectedMetadata(OptionButton opt)
	{
		if (opt.Selected < 0 || opt.Selected >= opt.ItemCount)
			return "";
		return opt.GetItemMetadata(opt.Selected).AsString();
	}
}

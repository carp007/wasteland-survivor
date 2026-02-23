using System.Collections.Generic;
using Godot;
using WastelandSurvivor.Core.IO;

namespace WastelandSurvivor.Game.UI;

public partial class GarageView : Control
{
	private Label? _lblActive;
	private Label? _lblRepair;
	private Label? _lblUpgrades;
	private ItemList? _list;
	private Button? _btnSetActive;
	private Button? _btnPatchArmor;
	private Button? _btnPatchTire;
	private Button? _btnUpgradeArmor;
	private Button? _btnUpgradeTire;
	private Button? _btnBack;

	private readonly List<string> _instanceIdsByIndex = new();

	public override void _Ready()
	{
		_lblActive = GetNodeOrNull<Label>("Panel/VBox/LblActive");
		_lblRepair = GetNodeOrNull<Label>("Panel/VBox/LblRepair");
		_lblUpgrades = GetNodeOrNull<Label>("Panel/VBox/LblUpgrades");
		_list = GetNodeOrNull<ItemList>("Panel/VBox/VehicleList");
		_btnSetActive = GetNodeOrNull<Button>("Panel/VBox/HBoxButtons/BtnSetActive");
		_btnPatchArmor = GetNodeOrNull<Button>("Panel/VBox/HBoxRepair/BtnPatchArmor");
		_btnPatchTire = GetNodeOrNull<Button>("Panel/VBox/HBoxRepair/BtnPatchTire");
		_btnUpgradeArmor = GetNodeOrNull<Button>("Panel/VBox/HBoxUpgrades/BtnUpgradeArmor");
		_btnUpgradeTire = GetNodeOrNull<Button>("Panel/VBox/HBoxUpgrades/BtnUpgradeTire");
		_btnBack = GetNodeOrNull<Button>("Panel/VBox/HBoxButtons/BtnBack");

		_btnSetActive!.Pressed += SetActiveFromSelection;
		_btnPatchArmor!.Pressed += PatchArmor;
		_btnPatchTire!.Pressed += PatchTire;
		_btnUpgradeArmor!.Pressed += UpgradeArmorPlating;
		_btnUpgradeTire!.Pressed += UpgradeTirePlating;
		_btnBack!.Pressed += Back;

		Refresh();
	}

	private void Refresh()
	{
		var app = App.Instance;
		if (app == null) return;

		var session = app.Services.Get<GameSession>();
		var defs = app.Services.Get<DefDatabase>();

		_list!.Clear();
		_instanceIdsByIndex.Clear();

		foreach (var v in session.GetOwnedVehicles())
		{
			var defName = defs.Vehicles.TryGetValue(v.DefinitionId, out var vdef)
				? vdef.DisplayName
				: v.DefinitionId;
			var label = $"{defName}   ({v.InstanceId[..8]})";
			_list.AddItem(label);
			_instanceIdsByIndex.Add(v.InstanceId);
		}

		var active = session.GetActiveVehicle();
		_lblActive!.Text = active == null ? "Active: (none)" : $"Active: {active.DefinitionId} ({active.InstanceId[..8]})";

		if (active is null)
		{
			_lblRepair!.Text = "Scrap repairs: select an active vehicle.";
			_lblUpgrades!.Text = "";
			_btnPatchArmor!.Disabled = true;
			_btnPatchTire!.Disabled = true;
			_btnUpgradeArmor!.Disabled = true;
			_btnUpgradeTire!.Disabled = true;
			return;
		}

		var (armorMissing, tireMissing, totalMissing) = session.ComputeMissingRepairPointsByType(active.InstanceId, defs);
		var scrap = session.Save.Player.Scrap;
		_lblRepair!.Text = $"Scrap: {scrap} | Armor missing: {armorMissing} | Tire missing: {tireMissing} | Total: {totalMissing}";

		var canSpend = scrap >= GameSession.ScrapRepairCostPerPoint && !session.HasActiveEncounter();
		_btnPatchArmor!.Disabled = !canSpend || armorMissing <= 0;
		_btnPatchTire!.Disabled = !canSpend || tireMissing <= 0;

		var canUpgrade = !session.HasActiveEncounter();
		var armorLevel = active.ArmorPlatingLevel;
		var tireLevel = active.TirePlatingLevel;

		var armorNext = armorLevel + 1;
		var tireNext = tireLevel + 1;
		var armorCost = GameSession.GetArmorPlatingUpgradeCost(armorNext);
		var tireCost = GameSession.GetTirePlatingUpgradeCost(tireNext);

		_lblUpgrades!.Text = $"Upgrades: Armor Plating L{armorLevel}/{GameSession.MaxArmorPlatingLevel} (+{armorLevel} max) | Tire Plating L{tireLevel}/{GameSession.MaxTirePlatingLevel} (+{tireLevel} max)";

		_btnUpgradeArmor!.Text = armorLevel >= GameSession.MaxArmorPlatingLevel
			? "Armor Plating (MAX)"
			: $"Install Armor Plating (L{armorLevel}→L{armorNext}) (-{armorCost} scrap)";

		_btnUpgradeTire!.Text = tireLevel >= GameSession.MaxTirePlatingLevel
			? "Tire Plating (MAX)"
			: $"Install Tire Plating (L{tireLevel}→L{tireNext}) (-{tireCost} scrap)";

		_btnUpgradeArmor!.Disabled = !canUpgrade || armorLevel >= GameSession.MaxArmorPlatingLevel || scrap < armorCost;
		_btnUpgradeTire!.Disabled = !canUpgrade || tireLevel >= GameSession.MaxTirePlatingLevel || scrap < tireCost;
	}

	private void PatchArmor()
	{
		var app = App.Instance;
		if (app == null) return;
		var session = app.Services.Get<GameSession>();
		var defs = app.Services.Get<DefDatabase>();
		var active = session.GetActiveVehicle();
		if (active is null) return;

		session.TryPatchArmorWithScrap(active.InstanceId, defs, out _);
		Refresh();
	}

	private void PatchTire()
	{
		var app = App.Instance;
		if (app == null) return;
		var session = app.Services.Get<GameSession>();
		var defs = app.Services.Get<DefDatabase>();
		var active = session.GetActiveVehicle();
		if (active is null) return;

		session.TryPatchTireWithScrap(active.InstanceId, defs, out _);
		Refresh();
	}

	
	private void UpgradeArmorPlating()
	{
		var app = App.Instance;
		if (app == null) return;
		var session = app.Services.Get<GameSession>();
		var defs = app.Services.Get<DefDatabase>();
		var active = session.GetActiveVehicle();
		if (active is null) return;

		session.TryUpgradeArmorPlating(active.InstanceId, defs, out _);
		Refresh();
	}

	private void UpgradeTirePlating()
	{
		var app = App.Instance;
		if (app == null) return;
		var session = app.Services.Get<GameSession>();
		var defs = app.Services.Get<DefDatabase>();
		var active = session.GetActiveVehicle();
		if (active is null) return;

		session.TryUpgradeTirePlating(active.InstanceId, defs, out _);
		Refresh();
	}

	private void SetActiveFromSelection()
	{
		var app = App.Instance;
		if (app == null) return;

		var selected = _list!.GetSelectedItems();
		if (selected.Length == 0) return;

		var idx = selected[0];
		if (idx < 0 || idx >= _instanceIdsByIndex.Count) return;

		var session = app.Services.Get<GameSession>();
		session.SetActiveVehicle(_instanceIdsByIndex[idx]);
		Refresh();
	}

	private void Back()
	{
		var parent = GetParent();
		if (parent == null) return;

		var scene = GD.Load<PackedScene>("res://Scenes/UI/CityShell.tscn");
		var ui = scene.Instantiate();
		parent.AddChild(ui);
		QueueFree();
	}
}

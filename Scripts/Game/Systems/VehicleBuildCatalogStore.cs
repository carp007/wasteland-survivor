// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Game/Systems/VehicleBuildCatalogStore.cs
// Purpose: Data-driven vehicle build presets for starter vehicles and arena opponents.
// -------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WastelandSurvivor.Core.IO;

namespace WastelandSurvivor.Game.Systems;

public sealed class VehicleBuildCatalogStore : JsonConfigStore<VehicleBuildCatalog>
{
	public static VehicleBuildCatalogStore Instance { get; } = new();

	private VehicleBuildCatalogStore() : base("res://Data/Config/vehicle_builds.json")
	{
	}

	protected override VehicleBuildCatalog CreateDefault() => VehicleBuildCatalog.CreateDefault();

	protected override VehicleBuildCatalog Normalize(VehicleBuildCatalog config)
	{
		config.StarterBuilds ??= new List<VehicleBuildPreset>();
		config.ArenaEnemyBuilds ??= new List<ArenaEnemyBuildPreset>();

		foreach (var preset in config.StarterBuilds)
			NormalizePreset(preset);
		foreach (var preset in config.ArenaEnemyBuilds)
			NormalizeArenaPreset(preset);

		return config;
	}

	private static void NormalizeArenaPreset(ArenaEnemyBuildPreset? preset)
	{
		if (preset == null)
			return;

		NormalizePreset(preset);
		preset.Variants ??= new List<ArenaEnemyBuildPreset>();
		preset.Variants.RemoveAll(v => v == null || string.IsNullOrWhiteSpace(v.VehicleDefinitionId));
		foreach (var variant in preset.Variants)
		{
			NormalizePreset(variant);
			// Variants always fight in their parent's bracket and never nest further.
			variant.Tier = preset.Tier;
			variant.Variants = new List<ArenaEnemyBuildPreset>();
		}
	}

	private static void NormalizePreset(VehicleBuildPreset? preset)
	{
		if (preset == null)
			return;

		preset.WeaponsByMountId ??= new Dictionary<string, VehicleMountBuild>(StringComparer.OrdinalIgnoreCase);
		preset.AmmoInventory ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		preset.CargoInventory ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
	}
}


public sealed class VehicleBuildCatalog
{
	public List<VehicleBuildPreset> StarterBuilds { get; set; } = new();
	public List<ArenaEnemyBuildPreset> ArenaEnemyBuilds { get; set; } = new();

	public VehicleBuildPreset? FindStarterPreset(string vehicleDefinitionId)
		=> StarterBuilds.FirstOrDefault(x => string.Equals(x.VehicleDefinitionId, vehicleDefinitionId, StringComparison.OrdinalIgnoreCase));

	public ArenaEnemyBuildPreset? FindArenaEnemyPreset(int tier)
		=> ArenaEnemyBuilds
			.Where(x => x.Tier == tier)
			.OrderBy(x => x.PresetId, StringComparer.OrdinalIgnoreCase)
			.FirstOrDefault();

	/// <summary>
	/// Full roster for a tier: the base preset first, then its authored <see cref="ArenaEnemyBuildPreset.Variants"/>.
	/// Empty when the tier has no base preset. Callers roll uniformly over this pool so every
	/// variant (and the base) is equally likely.
	/// </summary>
	public IReadOnlyList<ArenaEnemyBuildPreset> GetArenaEnemyPresetPool(int tier)
	{
		var basePreset = FindArenaEnemyPreset(tier);
		if (basePreset == null)
			return Array.Empty<ArenaEnemyBuildPreset>();

		var pool = new List<ArenaEnemyBuildPreset> { basePreset };
		if (basePreset.Variants != null)
			pool.AddRange(basePreset.Variants.Where(v => v != null && !string.IsNullOrWhiteSpace(v.VehicleDefinitionId)));
		return pool;
	}

	public static VehicleBuildCatalog CreateDefault()
	{
		return new VehicleBuildCatalog
		{
			StarterBuilds = new List<VehicleBuildPreset>
			{
				new()
				{
					PresetId = "starter_compact",
					VehicleDefinitionId = "veh_compact",
					EngineId = "eng_gas_v6",
					ComputerId = "tc_advanced",
					WeaponsByMountId = new Dictionary<string, VehicleMountBuild>
					{
						["F1"] = new() { WeaponId = "wpn_mg_50cal", SelectedAmmoId = "ammo_mg_50cal" },
						["R1"] = new() { WeaponId = "wpn_missile", SelectedAmmoId = "ammo_missile_std" },
						["B1"] = new() { WeaponId = "wpn_mine_dropper", SelectedAmmoId = "ammo_mine_std" }
					},
					AmmoInventory = new Dictionary<string, int>
					{
						["ammo_mg_50cal"] = 80,
						["ammo_missile_std"] = 12,
						["ammo_mine_std"] = 8
					},
					CargoInventory = new Dictionary<string, int>
					{
						["spare_tire"] = 1
					}
				},
				new()
				{
					PresetId = "starter_sedan",
					VehicleDefinitionId = "veh_sedan",
					EngineId = "eng_gas_v6",
					ComputerId = "tc_advanced",
					WeaponsByMountId = new Dictionary<string, VehicleMountBuild>
					{
						["F1"] = new() { WeaponId = "wpn_mg_50cal", SelectedAmmoId = "ammo_mg_50cal" },
						["L1"] = new() { WeaponId = "wpn_missile", SelectedAmmoId = "ammo_missile_std" },
						["B1"] = new() { WeaponId = "wpn_mine_dropper", SelectedAmmoId = "ammo_mine_std" }
					},
					AmmoInventory = new Dictionary<string, int>
					{
						["ammo_mg_50cal"] = 90,
						["ammo_missile_std"] = 10,
						["ammo_mine_std"] = 8
					},
					CargoInventory = new Dictionary<string, int>
					{
						["spare_tire"] = 1
					}
				},
				new()
				{
					PresetId = "starter_sports",
					VehicleDefinitionId = "veh_sports",
					EngineId = "eng_gas_v6",
					ComputerId = "tc_advanced",
					WeaponsByMountId = new Dictionary<string, VehicleMountBuild>
					{
						["F1"] = new() { WeaponId = "wpn_mg_50cal", SelectedAmmoId = "ammo_mg_50cal" },
						["T1"] = new() { WeaponId = "wpn_missile", SelectedAmmoId = "ammo_missile_std" },
						["B1"] = new() { WeaponId = "wpn_mine_dropper", SelectedAmmoId = "ammo_mine_std" }
					},
					AmmoInventory = new Dictionary<string, int>
					{
						["ammo_mg_50cal"] = 75,
						["ammo_missile_std"] = 10,
						["ammo_mine_std"] = 6
					}
				},
				new()
				{
					PresetId = "starter_light_truck",
					VehicleDefinitionId = "veh_light_truck",
					EngineId = "eng_diesel_i4",
					ComputerId = "tc_advanced",
					WeaponsByMountId = new Dictionary<string, VehicleMountBuild>
					{
						["F1"] = new() { WeaponId = "wpn_mg_50cal", SelectedAmmoId = "ammo_mg_50cal" },
						["T1"] = new() { WeaponId = "wpn_missile", SelectedAmmoId = "ammo_missile_std" },
						["B1"] = new() { WeaponId = "wpn_mine_dropper", SelectedAmmoId = "ammo_mine_std" }
					},
					AmmoInventory = new Dictionary<string, int>
					{
						["ammo_mg_50cal"] = 120,
						["ammo_missile_std"] = 14,
						["ammo_mine_std"] = 10
					},
					CargoInventory = new Dictionary<string, int>
					{
						["spare_tire"] = 1
					}
				}
			},
			ArenaEnemyBuilds = new List<ArenaEnemyBuildPreset>
			{
				new()
				{
					PresetId = "arena_tier_1",
					Tier = 1,
					VehicleDefinitionId = "veh_compact",
					EngineId = "eng_gas_v6",
					ComputerId = "tc_basic",
					VisualModelPathOverride = "res://Assets/Models/Vehicles/KenneyCarKit/taxi.glb",
					BodyColorHex = "#8a4a2b",
					MirrorPlayerWeaponsWhenPossible = true,
					WeaponsByMountId = new Dictionary<string, VehicleMountBuild>
					{
						["F1"] = new() { WeaponId = "wpn_mg_50cal", SelectedAmmoId = "ammo_mg_50cal" },
						["R1"] = new() { WeaponId = "wpn_missile", SelectedAmmoId = "ammo_missile_std" }
					},
					AmmoInventory = new Dictionary<string, int>
					{
						["ammo_mg_50cal"] = 80,
						["ammo_missile_std"] = 8
					}
				},
				new()
				{
					PresetId = "arena_tier_2",
					Tier = 2,
					VehicleDefinitionId = "veh_sedan",
					EngineId = "eng_gas_v6",
					ComputerId = "tc_advanced",
					VisualModelPathOverride = "res://Assets/Models/Vehicles/KenneyCarKit/police.glb",
					BodyColorHex = "#1f3a5f",
					MirrorPlayerWeaponsWhenPossible = true,
					WeaponsByMountId = new Dictionary<string, VehicleMountBuild>
					{
						["F1"] = new() { WeaponId = "wpn_mg_50cal", SelectedAmmoId = "ammo_mg_50cal" },
						["L1"] = new() { WeaponId = "wpn_missile", SelectedAmmoId = "ammo_missile_std" }
					},
					AmmoInventory = new Dictionary<string, int>
					{
						["ammo_mg_50cal"] = 90,
						["ammo_missile_std"] = 10
					}
				},
				new()
				{
					PresetId = "arena_tier_3",
					Tier = 3,
					VehicleDefinitionId = "veh_light_truck",
					EngineId = "eng_diesel_i4",
					ComputerId = "tc_advanced",
					VisualModelPathOverride = "res://Assets/Models/Vehicles/KenneyCarKit/van.glb",
					BodyColorHex = "#5c6134",
					ArmorPlatingLevel = 1,
					TirePlatingLevel = 1,
					MirrorPlayerWeaponsWhenPossible = true,
					WeaponsByMountId = new Dictionary<string, VehicleMountBuild>
					{
						["F1"] = new() { WeaponId = "wpn_mg_50cal", SelectedAmmoId = "ammo_mg_50cal" },
						["T1"] = new() { WeaponId = "wpn_missile", SelectedAmmoId = "ammo_missile_std" }
					},
					AmmoInventory = new Dictionary<string, int>
					{
						["ammo_mg_50cal"] = 120,
						["ammo_missile_std"] = 12
					}
				}
			}
		};
	}
}

public class VehicleBuildPreset
{
	public string PresetId { get; set; } = "";
	public string VehicleDefinitionId { get; set; } = "";
	public string? EngineId { get; set; }
	public string? ComputerId { get; set; }
	public Dictionary<string, VehicleMountBuild> WeaponsByMountId { get; set; } = new();
	public Dictionary<string, int> AmmoInventory { get; set; } = new();
	public Dictionary<string, int> CargoInventory { get; set; } = new();
	public int ArmorPlatingLevel { get; set; }
	public int TirePlatingLevel { get; set; }
	public bool IncludeDefaultSpareTire { get; set; } = true;
}

public sealed class ArenaEnemyBuildPreset : VehicleBuildPreset
{
	public int Tier { get; set; } = 1;
	public bool MirrorPlayerWeaponsWhenPossible { get; set; } = true;

	// Optional per-tier visual identity. When set, the arena spawner feeds these into
	// VehiclePawn.ApplyVisualPreset(): the model path wins over VehicleDefinition.VisualModelPath
	// and the "#rrggbb" color replaces the default enemy body tint.
	public string? VisualModelPathOverride { get; set; }
	public string? BodyColorHex { get; set; }

	// Optional callsign for the target HUD lock readout (e.g. "WARDEN"). Null keeps the
	// spawner's generic enemy name.
	public string? DisplayNameOverride { get; set; }

	// Optional per-tier roster: full alternate presets that fight in the same bracket. The tier's
	// effective pool is [base] + Variants, rolled uniformly once per encounter (armor-type variety
	// so no single ammo tag is auto-optimal against a tier). Variants never nest.
	public List<ArenaEnemyBuildPreset>? Variants { get; set; }
}

public sealed class VehicleMountBuild
{
	public string WeaponId { get; set; } = "";
	public string? SelectedAmmoId { get; set; }
}

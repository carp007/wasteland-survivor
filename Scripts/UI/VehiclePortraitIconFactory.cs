// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/UI/VehiclePortraitIconFactory.cs
// Purpose: Frozen 3D showroom thumbnails for list rows. Renders the shared VehiclePortraitViewport
//          rig once per (vehicle def, body color) off-screen, freezes a frame, and caches the
//          texture for the session — so store/garage rows get the real car instead of the flat
//          vector silhouettes without paying a live viewport per row.
// -------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using WastelandSurvivor.Core.Defs;
using WastelandSurvivor.Core.IO;
using WastelandSurvivor.Core.State;

namespace WastelandSurvivor.Game.UI;

public static class VehiclePortraitIconFactory
{
	private static readonly Dictionary<string, Texture2D> Cache = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>Synchronous cache probe so rebuild-heavy screens can skip the async hop.</summary>
	public static Texture2D? GetCached(string defId, Color bodyColor)
		=> Cache.TryGetValue(MakeKey(defId, bodyColor), out var tex) ? tex : null;

	/// <summary>
	/// Returns (building on miss) the frozen showroom thumbnail for a vehicle def. The builder
	/// portrait is parked far off-screen under <paramref name="host"/> for the ~7 frames the rig
	/// needs to load the model, fit its camera, and render. Returns null when the pawn fails to
	/// build (callers keep their silhouette fallback).
	/// </summary>
	public static async Task<Texture2D?> GetOrCreateAsync(Node host, DefDatabase defs, VehicleDefinition def, Color bodyColor)
	{
		var key = MakeKey(def.Id, bodyColor);
		if (Cache.TryGetValue(key, out var hit))
			return hit;
		if (host == null || !GodotObject.IsInstanceValid(host) || host.GetTree() == null)
			return null;

		var builder = new VehiclePortraitViewport
		{
			Name = "PortraitIconBuilder",
			TurntableDegPerSec = 0f, // hold the showroom angle
			InitialYawDegrees = 148f, // front-3/4: class identity reads at list size
			Position = new Vector2(-8000f, -8000f),
			CustomMinimumSize = new Vector2(4f, 4f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		host.AddChild(builder);
		try
		{
			builder.ShowVehicle(defs, def, CreateFactoryFreshInstance(def), bodyColor);
			if (!builder.HasPortrait)
				return null;

			// Model load + deferred camera fit happen across the first frames; give it headroom.
			for (var i = 0; i < 7; i++)
				await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);

			if (!GodotObject.IsInstanceValid(host) || host.GetTree() == null)
				return null;

			var img = builder.CaptureImage();
			if (img == null)
				return null;

			// Half-res of the 460x300 rig frame is plenty for 50-90 px tiles and keeps VRAM tiny.
			img.Resize(230, 150, Image.Interpolation.Lanczos);
			var tex = ImageTexture.CreateFromImage(img);
			Cache[key] = tex;
			return tex;
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[VehiclePortraitIconFactory] Thumbnail build failed for '{def.Id}': {ex.Message}");
			return null;
		}
		finally
		{
			if (GodotObject.IsInstanceValid(builder))
				builder.QueueFree();
		}
	}

	/// <summary>Factory-fresh preview state: healthy tires/sections, no weapons (chassis sell bare).</summary>
	public static VehicleInstanceState CreateFactoryFreshInstance(VehicleDefinition def)
	{
		return new VehicleInstanceState
		{
			DefinitionId = def.Id,
			CurrentArmorBySection = new(),
			CurrentHpBySection = new(),
			CurrentTireArmor = Enumerable.Repeat(Math.Max(1, def.BaseTireArmor), Math.Max(1, def.TireCount)).ToArray(),
			CurrentTireHp = Enumerable.Repeat(Math.Max(1, def.BaseTireHp), Math.Max(1, def.TireCount)).ToArray(),
			InstalledWeaponsByMountId = new(),
			AmmoInventory = new(),
			CargoInventory = new(),
			Towing = new TowingState(),
		};
	}

	private static string MakeKey(string defId, Color bodyColor) => $"{defId}|{bodyColor.ToHtml(false)}";
}

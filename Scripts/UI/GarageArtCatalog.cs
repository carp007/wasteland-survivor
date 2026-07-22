using System;
using System.Collections.Generic;
using Godot;
using WastelandSurvivor.Core.Defs;

namespace WastelandSurvivor.Game.UI;

/// <summary>
/// Centralized authored garage art lookup. Falls back cleanly when a specific vehicle class does not have a bespoke mockup crop yet.
/// </summary>
public static class GarageArtCatalog
{
    private static readonly Dictionary<string, Texture2D?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static Texture2D? GetListArt(VehicleDefinition? definition)
        => Load(ResolveListArt(definition), liftShadows: true);

    public static Texture2D? GetHeroArt(VehicleDefinition? definition)
        => Load(ResolveHeroArt(definition));

    private static Texture2D? Load(string? assetName, bool liftShadows = false)
    {
        if (string.IsNullOrWhiteSpace(assetName))
            return null;

        var cacheKey = liftShadows ? assetName + "#lift" : assetName;
        if (Cache.TryGetValue(cacheKey, out var cached))
            return cached;

        var texture = GeneratedUiArt.Load(assetName);
        if (liftShadows && texture != null)
            texture = LiftShadows(texture) ?? texture;
        Cache[cacheKey] = texture;
        return texture;
    }

    /// <summary>
    /// One-time gamma lift for the authored list crops: the source mockup art is exposed for a
    /// large hero frame, and at 72x56 in a dark inset the roster thumbnails rendered near-black
    /// (round 10 P2-10c). A shadow-weighted gamma (x^(1/1.55)) raises the car body into
    /// legibility while leaving highlights alone; cached so it runs once per asset.
    /// </summary>
    private static Texture2D? LiftShadows(Texture2D source)
    {
        try
        {
            var img = source.GetImage();
            if (img == null || img.IsEmpty())
                return null;
            if (img.IsCompressed() && img.Decompress() != Error.Ok)
                return null;
            img.Convert(Image.Format.Rgba8);

            const float invGamma = 1f / 1.55f;
            for (var y = 0; y < img.GetHeight(); y++)
            {
                for (var x = 0; x < img.GetWidth(); x++)
                {
                    var c = img.GetPixel(x, y);
                    img.SetPixel(x, y, new Color(
                        Mathf.Pow(c.R, invGamma),
                        Mathf.Pow(c.G, invGamma),
                        Mathf.Pow(c.B, invGamma),
                        c.A));
                }
            }
            return ImageTexture.CreateFromImage(img);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GarageArtCatalog] Shadow lift failed: {ex.Message}");
            return null;
        }
    }

    private static string? ResolveListArt(VehicleDefinition? definition)
    {
        if (definition == null)
            return null;

        if (definition.Id.Equals("veh_compact", StringComparison.OrdinalIgnoreCase))
            return "garage_vehicle_compact_list";
        if (definition.Id.Equals("veh_sedan", StringComparison.OrdinalIgnoreCase))
            return "garage_vehicle_sedan_list";
        if (definition.Id.Equals("veh_light_truck", StringComparison.OrdinalIgnoreCase))
            return "garage_vehicle_truck_list";
        if (definition.Id.Equals("veh_sports", StringComparison.OrdinalIgnoreCase))
            return "garage_vehicle_sports_list";

        return definition.Class switch
        {
            VehicleClass.Compact => "garage_vehicle_compact_list",
            VehicleClass.Sedan => "garage_vehicle_sedan_list",
            VehicleClass.LightTruck => "garage_vehicle_truck_list",
            VehicleClass.Sports => "garage_vehicle_sports_list",
            _ => null,
        };
    }

    private static string? ResolveHeroArt(VehicleDefinition? definition)
    {
        if (definition == null)
            return null;

        if (definition.Id.Equals("veh_compact", StringComparison.OrdinalIgnoreCase))
            return "garage_vehicle_compact_hero";
        if (definition.Id.Equals("veh_sports", StringComparison.OrdinalIgnoreCase))
            return "garage_vehicle_sports_hero";

        return definition.Class switch
        {
            VehicleClass.Compact => "garage_vehicle_compact_hero",
            VehicleClass.Sports => "garage_vehicle_sports_hero",
            _ => null,
        };
    }
}

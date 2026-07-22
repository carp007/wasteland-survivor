// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/UI/CityBackdropArt.cs
// Purpose: Per-city backdrop resolution for hub screens. Prefers the def-driven BackdropPath art;
//          falls back to a deterministic procedurally generated skyline (dark gradient sky, layered
//          silhouettes, warm horizon glow) so city screens never render a bare color even without
//          the optional Assets/ art on disk.
// -------------------------------------------------------------------------------------------------
using System.Collections.Generic;
using Godot;
using WastelandSurvivor.Core.Defs;

namespace WastelandSurvivor.Game.UI;

public static class CityBackdropArt
{
	private const int Width = 1280;
	private const int Height = 720;

	private static readonly Dictionary<string, Texture2D> GeneratedCache = new(System.StringComparer.OrdinalIgnoreCase);

	/// <summary>Accent palette + skyline scale per city. Flat, desaturated, command-console-adjacent.</summary>
	private sealed record CityPalette(Color SkyTop, Color SkyHorizon, Color Glow, Color Window, float HeightScale);

	private static readonly CityPalette DefaultPalette = new(
		new Color(0.035f, 0.045f, 0.065f), new Color(0.16f, 0.12f, 0.10f),
		new Color(0.72f, 0.48f, 0.26f), new Color(0.95f, 0.72f, 0.40f), 0.8f);

	private static readonly Dictionary<string, CityPalette> Palettes = new(System.StringComparer.OrdinalIgnoreCase)
	{
		// Motor capital: ember orange over a dense high skyline.
		["detroit"] = new(new Color(0.040f, 0.045f, 0.060f), new Color(0.18f, 0.11f, 0.08f),
			new Color(0.86f, 0.45f, 0.20f), new Color(1.00f, 0.74f, 0.42f), 1.0f),
		// Northern outpost: cold pine green, low sparse structures.
		["saginaw"] = new(new Color(0.030f, 0.045f, 0.055f), new Color(0.10f, 0.14f, 0.13f),
			new Color(0.45f, 0.68f, 0.58f), new Color(0.80f, 0.92f, 0.78f), 0.55f),
		// Refinery town: burning-stack amber.
		["toledo"] = new(new Color(0.045f, 0.040f, 0.050f), new Color(0.19f, 0.13f, 0.06f),
			new Color(0.94f, 0.62f, 0.18f), new Color(1.00f, 0.80f, 0.44f), 0.75f),
		// Lakeside salvage port: cold teal.
		["cleveland"] = new(new Color(0.030f, 0.050f, 0.060f), new Color(0.08f, 0.15f, 0.15f),
			new Color(0.26f, 0.66f, 0.60f), new Color(0.72f, 0.94f, 0.86f), 0.85f),
		// Windswept waystation: storm slate blue.
		["erie"] = new(new Color(0.035f, 0.040f, 0.060f), new Color(0.11f, 0.13f, 0.18f),
			new Color(0.50f, 0.60f, 0.78f), new Color(0.82f, 0.88f, 1.00f), 0.55f),
		// Walled trade fort: ochre sand.
		["fort_wayne"] = new(new Color(0.045f, 0.040f, 0.045f), new Color(0.17f, 0.13f, 0.08f),
			new Color(0.82f, 0.60f, 0.30f), new Color(0.98f, 0.82f, 0.52f), 0.7f),
		// Western metropolis: steel cyan, tallest skyline.
		["chicago"] = new(new Color(0.030f, 0.040f, 0.060f), new Color(0.08f, 0.13f, 0.18f),
			new Color(0.28f, 0.62f, 0.86f), new Color(0.70f, 0.90f, 1.00f), 1.05f),
		// Steel city: furnace red.
		["pittsburgh"] = new(new Color(0.040f, 0.035f, 0.050f), new Color(0.18f, 0.09f, 0.07f),
			new Color(0.88f, 0.32f, 0.16f), new Color(1.00f, 0.62f, 0.36f), 0.95f),
	};

	/// <summary>
	/// Resolves the backdrop for a city: def-driven art when BackdropPath is set and present,
	/// otherwise the generated skyline seeded by the city id. Never returns null.
	/// </summary>
	public static Texture2D Resolve(CityDefinition? city, string? cityIdFallback = null)
	{
		var path = city?.BackdropPath;
		if (!string.IsNullOrWhiteSpace(path) && ResourceLoader.Exists(path))
		{
			if (ResourceLoader.Load<Texture2D>(path) is { } loaded)
				return loaded;
		}

		var cityId = !string.IsNullOrWhiteSpace(city?.Id) ? city!.Id
			: !string.IsNullOrWhiteSpace(cityIdFallback) ? cityIdFallback!
			: "wasteland";
		return GetGenerated(cityId);
	}

	public static Texture2D GetGenerated(string cityId)
	{
		if (GeneratedCache.TryGetValue(cityId, out var cached))
			return cached;

		var texture = Generate(cityId);
		GeneratedCache[cityId] = texture;
		return texture;
	}

	private static Texture2D Generate(string cityId)
	{
		var palette = Palettes.TryGetValue(cityId, out var p) ? p : DefaultPalette;
		var rng = new RandomNumberGenerator { Seed = HashId(cityId) };

		var img = Image.CreateEmpty(Width, Height, false, Image.Format.Rgb8);
		var horizonY = (int)(Height * 0.62f);
		var groundColor = new Color(0.012f, 0.016f, 0.022f);

		// Sky: vertical gradient with a warm glow band hugging the horizon, fading to ground haze.
		for (var y = 0; y < Height; y++)
		{
			var t = y / (float)(Height - 1);
			var col = palette.SkyTop.Lerp(palette.SkyHorizon, Mathf.Clamp(t / 0.72f, 0f, 1f));
			var d = Mathf.Abs(y - horizonY) / (Height * 0.20f);
			col = col.Lerp(palette.Glow, Mathf.Exp(-d * d * 3f) * 0.5f);
			if (y > horizonY)
				col = col.Lerp(groundColor, Mathf.Clamp((y - horizonY) / (Height * 0.30f), 0f, 1f));
			img.FillRect(new Rect2I(0, y, Width, 1), col);
		}

		// Three silhouette layers, far (hazy, short) to near (near-black, tall).
		var silNear = new Color(0.020f, 0.026f, 0.034f);
		var nearBuildings = new List<Rect2I>();
		for (var layer = 0; layer < 3; layer++)
		{
			var color = layer switch
			{
				0 => silNear.Lerp(palette.SkyHorizon, 0.30f).Lerp(palette.Glow, 0.10f),
				1 => silNear.Lerp(palette.Glow, 0.06f),
				_ => silNear,
			};
			var baseline = horizonY + layer switch { 0 => 8, 1 => 34, _ => 72 };
			var (minH, maxH) = layer switch
			{
				0 => (20, (int)(90 * palette.HeightScale)),
				1 => (34, (int)(160 * palette.HeightScale)),
				_ => (50, (int)(240 * palette.HeightScale)),
			};
			maxH = Mathf.Max(maxH, minH + 12);
			var gapMax = palette.HeightScale >= 0.9f ? 6 : 16;

			var x = -rng.RandiRange(0, 24);
			while (x < Width)
			{
				var w = rng.RandiRange(22, 68);
				var h = rng.Randf() < 0.20f
					? rng.RandiRange(Mathf.Max(10, minH / 2), minH) // low warehouse block
					: rng.RandiRange(minH, maxH);
				var top = baseline - h;
				FillClamped(img, x, top, w, Height - top, color);

				// Spire / antenna mast on some taller blocks.
				if (rng.Randf() < 0.30f && h > minH)
				{
					var sw = rng.RandiRange(1, 3);
					var sx = x + rng.RandiRange(2, Mathf.Max(3, w - 5));
					FillClamped(img, sx, top - rng.RandiRange(10, 36), sw, 40, color);
				}

				// Stepped shoulder so rooflines are not all flat slabs.
				if (rng.Randf() < 0.35f)
				{
					var shoulderW = rng.RandiRange(5, Mathf.Max(6, w / 2));
					var shoulderH = rng.RandiRange(6, 22);
					var sx = rng.Randf() < 0.5f ? x : x + w - shoulderW;
					FillClamped(img, sx, top - shoulderH, shoulderW, shoulderH, color);
				}

				if (layer == 2)
					nearBuildings.Add(new Rect2I(x, top, w, Height - top));

				x += w + rng.RandiRange(1, gapMax);
			}
		}

		// Sparse lit windows on the near layer only — most blocks stay dark (post-collapse grid).
		foreach (var b in nearBuildings)
		{
			if (rng.Randf() > 0.55f)
				continue;
			var windowFloorLimit = Mathf.Min(horizonY + 56, Height - 3);
			for (var wy = b.Position.Y + 6; wy < windowFloorLimit; wy += 7)
			{
				for (var wx = b.Position.X + 3; wx < b.Position.X + b.Size.X - 4; wx += 5)
				{
					if (rng.Randf() < 0.06f)
						FillClamped(img, wx, wy, 2, 2, palette.Window.Darkened(rng.Randf() * 0.4f));
				}
			}
		}

		return ImageTexture.CreateFromImage(img);
	}

	private static void FillClamped(Image img, int x, int y, int w, int h, Color color)
	{
		var x0 = Mathf.Clamp(x, 0, Width);
		var y0 = Mathf.Clamp(y, 0, Height);
		var x1 = Mathf.Clamp(x + w, 0, Width);
		var y1 = Mathf.Clamp(y + h, 0, Height);
		if (x1 <= x0 || y1 <= y0)
			return;
		img.FillRect(new Rect2I(x0, y0, x1 - x0, y1 - y0), color);
	}

	/// <summary>Stable FNV-1a hash — string.GetHashCode is randomized per process in .NET.</summary>
	private static ulong HashId(string id)
	{
		var hash = 14695981039346656037UL;
		foreach (var ch in id.ToLowerInvariant())
		{
			hash ^= ch;
			hash *= 1099511628211UL;
		}
		return hash;
	}
}

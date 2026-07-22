using System.Collections.Generic;
using Godot;
using WastelandSurvivor.Core.Defs;

namespace WastelandSurvivor.Game.UI;

public static class GeneratedUiArt
{
    private const string BasePath = "res://Resources/UI/Generated";
    private static readonly Dictionary<string, Texture2D?> Cache = new(System.StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Texture2D?> SizedCache = new(System.StringComparer.OrdinalIgnoreCase);

    public static Texture2D? Load(string assetName)
    {
        if (string.IsNullOrWhiteSpace(assetName))
            return null;

        if (Cache.TryGetValue(assetName, out var cached))
            return cached;

        Texture2D? texture = null;
        foreach (var ext in new[] { ".svg", ".png", ".webp", ".jpg" })
        {
            var path = $"{BasePath}/{assetName}{ext}";
            if (!ResourceLoader.Exists(path))
                continue;

            texture = ResourceLoader.Load<Texture2D>(path);
            if (texture != null)
                break;
        }
        Cache[assetName] = texture;
        return texture;
    }

    public static Texture2D? LoadSized(string assetName, int maxWidth)
    {
        var safeMaxWidth = Mathf.Max(12, maxWidth);
        var cacheKey = $"{assetName}:{safeMaxWidth}";
        if (SizedCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var baseTexture = Load(assetName);
        if (baseTexture == null)
        {
            SizedCache[cacheKey] = null;
            return null;
        }

        var image = baseTexture.GetImage();
        if (image == null || image.IsEmpty())
        {
            SizedCache[cacheKey] = baseTexture;
            return baseTexture;
        }

        var originalWidth = Mathf.Max(1, image.GetWidth());
        var originalHeight = Mathf.Max(1, image.GetHeight());
        if (originalWidth <= safeMaxWidth)
        {
            SizedCache[cacheKey] = baseTexture;
            return baseTexture;
        }

        var scale = safeMaxWidth / (float)originalWidth;
        var targetWidth = Mathf.Max(1, Mathf.RoundToInt(originalWidth * scale));
        var targetHeight = Mathf.Max(1, Mathf.RoundToInt(originalHeight * scale));

        var resized = image.Duplicate() as Image ?? image;
        resized.Resize(targetWidth, targetHeight, Image.Interpolation.Lanczos);
        var resizedTexture = ImageTexture.CreateFromImage(resized);
        SizedCache[cacheKey] = resizedTexture;
        return resizedTexture;
    }

    public static void ApplyBanner(TextureRect? rect, string assetName)
    {
        if (rect == null)
            return;

        rect.Texture = Load(assetName);
        rect.Visible = rect.Texture != null;
        rect.Set("expand_mode", 1);
        rect.Set("stretch_mode", 6);
    }

    public static void ApplyIcon(GodotObject? target, string assetName, int maxWidth = 24)
    {
        if (target == null)
            return;

        var requestedWidth = Mathf.Max(12, maxWidth);
        var appliedWidth = target is Button
            ? Mathf.Max(12, Mathf.RoundToInt(requestedWidth * 1.05f))
            : requestedWidth;
        var texture = LoadSized(assetName, appliedWidth);
        if (texture == null)
            return;

        if (target is Button button)
        {
            button.Set("icon", texture);
            ConfigureButtonIcon(button, appliedWidth);
            return;
        }

        target.Set("icon", texture);
        if (target is Control control)
            control.AddThemeConstantOverride("icon_max_width", appliedWidth);
    }

    public static Texture2D? LoadMountIcon(MountLocation location)
        => location switch
        {
            MountLocation.Left or MountLocation.Right => Load("icon_weapon_side"),
            _ => Load("icon_weapon_front"),
        };

    private static void ConfigureButtonIcon(Button button, int maxWidth)
    {
        var safeMaxWidth = Mathf.Max(12, maxWidth);
        button.Set("expand_icon", false);
        button.Alignment = HorizontalAlignment.Left;
        button.AddThemeConstantOverride("icon_max_width", safeMaxWidth);
        button.AddThemeConstantOverride("h_separation", Mathf.Max(8, safeMaxWidth / 3));
        button.Set("icon_alignment", (int)HorizontalAlignment.Left);
        button.Set("vertical_icon_alignment", (int)VerticalAlignment.Center);
    }


    public static StyleBoxFlat CreatePremiumShellStyle()
    {
        // Flat, near-opaque command-console shell: quiet hairline border, accents reserved for
        // states/CTAs (the old translucent cyan-glow-everything read as dev tooling).
        return new StyleBoxFlat
        {
            BgColor = new Color(0.043f, 0.055f, 0.071f, 0.96f),
            BorderColor = new Color(1f, 1f, 1f, 0.08f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12,
            ContentMarginLeft = 18,
            ContentMarginRight = 18,
            ContentMarginTop = 16,
            ContentMarginBottom = 16,
            ShadowColor = new Color(0f, 0f, 0f, 0.55f),
            ShadowSize = 18,
        };
    }

    public static StyleBoxFlat CreatePremiumSectionStyle(bool emphasized = false, bool gold = false)
    {
        // Sections separate by tone, not glow. A 3px left accent bar carries emphasis/gold state.
        var accent = gold
            ? new Color(0.90f, 0.74f, 0.28f, emphasized ? 0.95f : 0.70f)
            : new Color(0.24f, 0.80f, 0.98f, emphasized ? 0.85f : 0.0f);

        return new StyleBoxFlat
        {
            BgColor = new Color(1f, 1f, 1f, emphasized ? 0.055f : 0.035f),
            BorderColor = accent,
            BorderWidthLeft = (gold || emphasized) ? 3 : 0,
            BorderWidthTop = 0,
            BorderWidthRight = 0,
            BorderWidthBottom = 0,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 14,
            ContentMarginRight = 12,
            ContentMarginTop = 10,
            ContentMarginBottom = 10,
        };
    }

    public static StyleBoxFlat CreatePremiumInsetStyle(bool selected = false, bool active = false)
    {
        var bg = selected
            ? new Color(0.90f, 0.74f, 0.28f, 0.14f)
            : new Color(1f, 1f, 1f, 0.030f);
        var border = active
            ? new Color(0.92f, 0.75f, 0.30f, 0.95f)
            : selected
                ? new Color(0.98f, 0.84f, 0.42f, 0.85f)
                : new Color(1f, 1f, 1f, 0.07f);

        return new StyleBoxFlat
        {
            BgColor = bg,
            BorderColor = border,
            BorderWidthLeft = selected || active ? 2 : 1,
            BorderWidthTop = selected || active ? 2 : 1,
            BorderWidthRight = selected || active ? 2 : 1,
            BorderWidthBottom = selected || active ? 2 : 1,
            CornerRadiusTopLeft = 7,
            CornerRadiusTopRight = 7,
            CornerRadiusBottomLeft = 7,
            CornerRadiusBottomRight = 7,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 9,
            ContentMarginBottom = 9,
        };
    }

    public static StyleBoxFlat CreatePremiumMetricTileStyle()
    {
        return new StyleBoxFlat
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
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 10,
            ContentMarginBottom = 10,
        };
    }

    public static StyleBoxFlat CreatePremiumBadgeStyle(bool gold = false, bool success = false, bool warning = false)
    {
        var border = success
            ? new Color(0.24f, 0.86f, 0.60f, 0.75f)
            : warning || gold
                ? new Color(0.92f, 0.75f, 0.30f, 0.75f)
                : new Color(0.24f, 0.80f, 0.98f, 0.50f);
        var bg = success
            ? new Color(0.03f, 0.16f, 0.12f, 0.82f)
            : warning || gold
                ? new Color(0.18f, 0.12f, 0.03f, 0.84f)
                : new Color(0.02f, 0.07f, 0.12f, 0.82f);

        return new StyleBoxFlat
        {
            BgColor = bg,
            BorderColor = border,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 999,
            CornerRadiusTopRight = 999,
            CornerRadiusBottomLeft = 999,
            CornerRadiusBottomRight = 999,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 4,
            ContentMarginBottom = 4,
        };
    }

    public static StyleBoxFlat CreateGarageShellStyle()
        => CreatePremiumShellStyle();

    public static StyleBoxFlat CreateGarageSectionStyle(bool emphasized = false, bool gold = false)
        => CreatePremiumSectionStyle(emphasized, gold);

    public static StyleBoxFlat CreateGarageInsetStyle(bool selected = false, bool active = false)
        => CreatePremiumInsetStyle(selected, active);

    public static StyleBoxFlat CreateGarageMetricTileStyle()
        => CreatePremiumMetricTileStyle();

    public static StyleBoxFlat CreateCardStyle(bool selected = false, bool active = false)
    {
        var style = new StyleBoxFlat
        {
            BgColor = selected
                ? WithAlpha(GameUiTheme.AccentGoldColor, 0.22f)
                : WithAlpha(GameUiTheme.PanelAltColor, 0.88f),
            BorderColor = active
                ? WithAlpha(GameUiTheme.AccentGoldColor, 0.82f)
                : selected
                    ? WithAlpha(GameUiTheme.AccentCyanColor, 0.95f)
                    : WithAlpha(GameUiTheme.AccentCyanColor, 0.35f),
            BorderWidthLeft = selected ? 2 : 1,
            BorderWidthTop = selected ? 2 : 1,
            BorderWidthRight = selected ? 2 : 1,
            BorderWidthBottom = selected ? 2 : 1,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 10,
            ContentMarginBottom = 10,
            ShadowColor = new Color(0f, 0f, 0f, 0.24f),
            ShadowSize = 4,
        };
        return style;
    }

    private static Color WithAlpha(Color color, float alpha)
        => new(color.R, color.G, color.B, alpha);
}

// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/UI/BootSplashConfigStore.cs
// Purpose: JSON-backed splash-sequence config so the boot screen uses the shared config-store path.
// -------------------------------------------------------------------------------------------------
using System.Collections.Generic;
using WastelandSurvivor.Core.IO;

namespace WastelandSurvivor.Game.UI;

public sealed class BootSplashConfigStore : JsonConfigStore<BootSplashConfig>
{
	public const string DefaultConfigPath = "res://Data/Config/boot_splash.json";

	public BootSplashConfigStore(string? configPath = null)
		: base(string.IsNullOrWhiteSpace(configPath) ? DefaultConfigPath : configPath!)
	{
	}

	protected override BootSplashConfig CreateDefault() => BootSplashConfig.CreateDefault();

	protected override BootSplashConfig Normalize(BootSplashConfig config)
	{
		config.Items ??= new List<BootSplashItem>();

		var normalizedItems = new List<BootSplashItem>();
		foreach (var item in config.Items)
		{
			if (item == null || string.IsNullOrWhiteSpace(item.Path))
				continue;
			normalizedItems.Add(item);
		}

		config.Items = normalizedItems;
		return config.Items.Count == 0 ? CreateDefault() : config;
	}
}

public sealed class BootSplashConfig
{
	public List<BootSplashItem> Items { get; set; } = new();
	public float? DefaultSeconds { get; set; }
	public float? FadeInSeconds { get; set; }
	public float? FadeOutSeconds { get; set; }
	public float? GapSeconds { get; set; }
	public string? Background { get; set; }
	public string? DefaultOpenSound { get; set; }

	public static BootSplashConfig CreateDefault()
	{
		return new BootSplashConfig
		{
			Items = new List<BootSplashItem>
			{
				new() { Path = "res://Assets/Images/title.png" }
			}
		};
	}
}

public sealed class BootSplashItem
{
	public string Path { get; set; } = string.Empty;
	public float? Seconds { get; set; }
	public string? OpenSound { get; set; }
}

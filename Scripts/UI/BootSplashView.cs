// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/UI/BootSplashView.cs
// Purpose: UI view/controller code for scenes under Scenes/UI.
// -------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using Godot;
using WastelandSurvivor.Framework.SceneBinding;

namespace WastelandSurvivor.Game.UI;

/// <summary>
/// Displays a configurable startup splash sequence (one or more images) and then exits.
/// Skip: Enter or Escape.
///
/// Config: res://Data/Config/boot_splash.json
/// </summary>
public partial class BootSplashView : Control
{
	public event Action? Completed;

	[Export] public string ConfigPath { get; set; } = "res://Data/Config/boot_splash.json";
	[Export] public float DefaultSecondsPerItem { get; set; } = 2.5f;
	[Export] public float FadeInSeconds { get; set; } = 0.20f;
	[Export] public float FadeOutSeconds { get; set; } = 0.20f;
	/// <summary>Optional black gap between items (seconds). Applies after fade out and before next fade in.</summary>
	[Export] public float InterItemGapSeconds { get; set; } = 0.12f;
	[Export] public Color BackgroundColor { get; set; } = new Color(0, 0, 0, 1);

	private ColorRect _bg = null!;
	private TextureRect _image = null!;
	private AudioStreamPlayer? _sfx;
	private bool _skipRequested;
	private bool _running;
	private Tween? _tween;

	private readonly Dictionary<string, AudioStream> _soundCache = new(StringComparer.OrdinalIgnoreCase);

	
	private void EnsureBound()
	{
		var b = new SceneBinder(this, nameof(BootSplashView));
		_bg = b.Req<ColorRect>("Bg");
		_image = b.Req<TextureRect>("Image");
		_sfx = b.Opt<AudioStreamPlayer>("Sfx");
	}
	
	private sealed class SplashConfig
	{
		public List<SplashItem> items { get; set; } = new();
		public float? defaultSeconds { get; set; }
		public float? fadeInSeconds { get; set; }
		public float? fadeOutSeconds { get; set; }
		public float? gapSeconds { get; set; }
		public string? background { get; set; }
		public string? defaultOpenSound { get; set; }
	}

	private sealed class SplashItem
	{
		public string path { get; set; } = "";
		public float? seconds { get; set; }
		public string? openSound { get; set; }
	}

	public override void _Ready()
	{

		GameUiTheme.ApplyToTree(this);
		// Default splash background to the UI theme background (config can override).
		BackgroundColor = GameUiTheme.BackgroundColor;
		EnsureBound();

		_bg.Color = BackgroundColor;
		_image.Modulate = new Color(1, 1, 1, 0);
		if (_sfx != null) _sfx.Bus = "SFX";

		// Run next frame so we are definitely in the tree.
		CallDeferred(nameof(Run));
	}

	private void Run()
	{
		if (_running) return;
		_running = true;
		_ = RunSequenceAsync();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is not InputEventKey key || !key.Pressed || key.Echo) return;
		if (key.Keycode is Key.Enter or Key.KpEnter or Key.Escape)
		{
			RequestSkip();
			GetViewport().SetInputAsHandled();
		}
	}

	private void RequestSkip()
	{
		_skipRequested = true;
		_tween?.Kill();
		if (_sfx != null && _sfx.Playing) _sfx.Stop();
	}

	private async System.Threading.Tasks.Task RunSequenceAsync()
	{
		var cfg = LoadConfig();
		ApplyConfigOverrides(cfg);

		var items = cfg.items;
		if (items.Count == 0)
		{
			Finish();
			return;
		}

		for (var i = 0; i < items.Count; i++)
		{
			if (_skipRequested) break;
			var item = items[i];

			if (!TrySetTexture(item.path))
				continue; // skip missing textures

			TryPlayOpenSound(item.openSound ?? cfg.defaultOpenSound);

			await FadeToAsync(1f, FadeInSeconds);
			if (_skipRequested) break;

			var seconds = item.seconds ?? DefaultSecondsPerItem;
			if (seconds > 0)
				await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

			if (_skipRequested) break;
			await FadeToAsync(0f, FadeOutSeconds);

			if (_skipRequested) break;
			if (i < items.Count - 1)
			{
				var gap = InterItemGapSeconds;
				if (gap > 0.0001f)
					await ToSignal(GetTree().CreateTimer(gap), SceneTreeTimer.SignalName.Timeout);
			}
		}

		Finish();
	}

	private SplashConfig LoadConfig()
	{
		try
		{
			if (!FileAccess.FileExists(ConfigPath))
				return DefaultConfig();

			var f = FileAccess.Open(ConfigPath, FileAccess.ModeFlags.Read);
			if (f == null) return DefaultConfig();
			var json = f.GetAsText();
			var parsed = Json.ParseString(json);
			if (parsed.VariantType != Variant.Type.Dictionary)
				return DefaultConfig();

			var dict = parsed.AsGodotDictionary();
			var cfg = new SplashConfig();

			if (dict.TryGetValue("defaultSeconds", out var dSec) && (dSec.VariantType == Variant.Type.Float || dSec.VariantType == Variant.Type.Int))
				cfg.defaultSeconds = (float)dSec;
			if (dict.TryGetValue("fadeInSeconds", out var fi) && (fi.VariantType == Variant.Type.Float || fi.VariantType == Variant.Type.Int))
				cfg.fadeInSeconds = (float)fi;
			if (dict.TryGetValue("fadeOutSeconds", out var fo) && (fo.VariantType == Variant.Type.Float || fo.VariantType == Variant.Type.Int))
				cfg.fadeOutSeconds = (float)fo;
			if (dict.TryGetValue("gapSeconds", out var gap) && (gap.VariantType == Variant.Type.Float || gap.VariantType == Variant.Type.Int))
				cfg.gapSeconds = (float)gap;
			if (dict.TryGetValue("background", out var bg) && bg.VariantType == Variant.Type.String)
				cfg.background = (string)bg;
			if (dict.TryGetValue("defaultOpenSound", out var ds) && ds.VariantType == Variant.Type.String)
				cfg.defaultOpenSound = (string)ds;

			if (dict.TryGetValue("items", out var itemsVar) && itemsVar.VariantType == Variant.Type.Array)
			{
				var arr = itemsVar.AsGodotArray();
				foreach (var v in arr)
				{
					if (v.VariantType != Variant.Type.Dictionary) continue;
					var it = v.AsGodotDictionary();
					var item = new SplashItem();
					if (it.TryGetValue("path", out var p) && p.VariantType == Variant.Type.String)
						item.path = (string)p;
					if (it.TryGetValue("seconds", out var s) && (s.VariantType == Variant.Type.Float || s.VariantType == Variant.Type.Int))
						item.seconds = (float)s;
					if (it.TryGetValue("openSound", out var os) && os.VariantType == Variant.Type.String)
						item.openSound = (string)os;
					if (!string.IsNullOrWhiteSpace(item.path))
						cfg.items.Add(item);
				}
			}

			return cfg.items.Count == 0 ? DefaultConfig() : cfg;
		}
		catch (Exception ex)
		{
			GD.PrintErr($"BootSplashView: Failed to load config '{ConfigPath}': {ex.Message}");
			return DefaultConfig();
		}
	}

	private SplashConfig DefaultConfig()
	{
		return new SplashConfig
		{
			items = new List<SplashItem>
			{
				new SplashItem { path = "res://Assets/Images/title.png", seconds = DefaultSecondsPerItem }
			}
		};
	}

	private void ApplyConfigOverrides(SplashConfig cfg)
	{
		if (cfg.defaultSeconds.HasValue) DefaultSecondsPerItem = cfg.defaultSeconds.Value;
		if (cfg.fadeInSeconds.HasValue) FadeInSeconds = cfg.fadeInSeconds.Value;
		if (cfg.fadeOutSeconds.HasValue) FadeOutSeconds = cfg.fadeOutSeconds.Value;
		if (cfg.gapSeconds.HasValue) InterItemGapSeconds = cfg.gapSeconds.Value;
		if (!string.IsNullOrWhiteSpace(cfg.background))
		{
			try
			{
				BackgroundColor = new Color(cfg.background);
				if (_bg != null) _bg.Color = BackgroundColor;
			}
			catch { /* ignore */ }
		}
	}

	private bool TrySetTexture(string path)
	{
		if (_image == null) return false;
		if (!ResourceLoader.Exists(path))
		{
			GD.PrintErr($"BootSplashView: Missing splash texture: {path}");
			return false;
		}

		var tex = GD.Load<Texture2D>(path);
		if (tex == null)
		{
			GD.PrintErr($"BootSplashView: Failed to load splash texture: {path}");
			return false;
		}
		_image.Texture = tex;
		return true;
	}

	private void TryPlayOpenSound(string? path)
	{
		if (_sfx == null) return;
		if (string.IsNullOrWhiteSpace(path)) return;
		if (!ResourceLoader.Exists(path))
		{
			GD.PrintErr($"BootSplashView: Missing open sound: {path}");
			return;
		}

		try
		{
			if (!_soundCache.TryGetValue(path, out var stream))
			{
				stream = GD.Load<AudioStream>(path);
				if (stream == null)
				{
					GD.PrintErr($"BootSplashView: Failed to load open sound: {path}");
					return;
				}
				_soundCache[path] = stream;
			}

			_sfx.Stream = stream;
			_sfx.Play();
		}
		catch (Exception ex)
		{
			GD.PrintErr($"BootSplashView: Failed to play open sound '{path}': {ex.Message}");
		}
	}

	private async System.Threading.Tasks.Task FadeToAsync(float alpha, float seconds)
	{
		if (_image == null) return;
		_tween?.Kill();
		_tween = CreateTween();
		_tween.SetTrans(Tween.TransitionType.Sine);
		_tween.SetEase(Tween.EaseType.InOut);
		var start = _image.Modulate;
		var end = new Color(start.R, start.G, start.B, alpha);
		if (seconds <= 0.0001f)
		{
			_image.Modulate = end;
			return;
		}

		_tween.TweenProperty(_image, "modulate", end, seconds);
		await ToSignal(_tween, Tween.SignalName.Finished);
	}

	private void Finish()
	{
		if (!_running) return;
		_running = false;
		Completed?.Invoke();
	}
}
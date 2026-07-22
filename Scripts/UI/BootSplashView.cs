// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/UI/BootSplashView.cs
// Purpose: UI view/controller code for scenes under Scenes/UI.
// -------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using Godot;
using GameUiKit.SceneBinding;
using GameUiKit.UI;

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

	[Export] public string ConfigPath { get; set; } = BootSplashConfigStore.DefaultConfigPath;
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
	


	public override void _Ready()
	{

		GameUiTheme.ApplyToTree(this);
		// Default splash background to the UI theme background (config can override).
		BackgroundColor = GameUiTheme.BackgroundColor;
		EnsureBound();

		_bg.Color = BackgroundColor;
		FullscreenTextureRectUtil.ConfigureCover(_image);
		_image.Modulate = new Color(1, 1, 1, 0);
		if (_sfx != null) _sfx.Bus = "SFX";

		BuildMusicCreditLine();

		// Run next frame so we are definitely in the tree.
		CallDeferred(nameof(Run));
	}

	private void Run()
	{
		if (_running) return;
		_running = true;
		_ = RunSequenceAsync();
	}

	/// <summary>
	/// CC BY 4.0 attribution for the Kevin MacLeod music tracks (required in-game credit; see
	/// Docs/Audio/ATTRIBUTION_AND_LICENSES.md). Rendered as a small muted line pinned to the bottom
	/// of the screen for the duration of the splash sequence, above the splash images.
	/// </summary>
	private void BuildMusicCreditLine()
	{
		var color = GameUiTheme.TextMutedColor;
		color.A = 0.85f;

		var credit = new Label
		{
			Name = "MusicCredit",
			Text = "Music: Kevin MacLeod (incompetech.com) — CC BY 4.0",
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		credit.AddThemeFontSizeOverride("font_size", 12);
		credit.AddThemeColorOverride("font_color", color);
		AddChild(credit);
		credit.SetAnchorsPreset(LayoutPreset.BottomWide);
		credit.OffsetTop = -34f;
		credit.OffsetBottom = -14f;
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

		var items = cfg.Items;
		if (items.Count == 0)
		{
			Finish();
			return;
		}

		for (var i = 0; i < items.Count; i++)
		{
			if (_skipRequested) break;
			var item = items[i];

			if (!TrySetTexture(item.Path))
				continue; // skip missing textures

			TryPlayOpenSound(item.OpenSound ?? cfg.DefaultOpenSound);

			await FadeToAsync(1f, FadeInSeconds);
			if (_skipRequested) break;

			var seconds = item.Seconds ?? DefaultSecondsPerItem;
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

	private BootSplashConfig LoadConfig()
	{
		return new BootSplashConfigStore(ConfigPath).Get();
	}

	private void ApplyConfigOverrides(BootSplashConfig cfg)
	{
		if (cfg.DefaultSeconds.HasValue) DefaultSecondsPerItem = cfg.DefaultSeconds.Value;
		if (cfg.FadeInSeconds.HasValue) FadeInSeconds = cfg.FadeInSeconds.Value;
		if (cfg.FadeOutSeconds.HasValue) FadeOutSeconds = cfg.FadeOutSeconds.Value;
		if (cfg.GapSeconds.HasValue) InterItemGapSeconds = cfg.GapSeconds.Value;
		if (!string.IsNullOrWhiteSpace(cfg.Background))
		{
			try
			{
				BackgroundColor = new Color(cfg.Background);
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

		FullscreenTextureRectUtil.ConfigureCover(_image);

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
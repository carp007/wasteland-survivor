// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Game/GameConsole.cs
// Purpose: Game-layer services and facades (session, balance, logging).
// -------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace WastelandSurvivor.Game;

public enum GameConsoleLineKind
{
	Debug = 0,
	Status = 1,
	Input = 2,
	Error = 3,
}

public readonly record struct GameConsoleLine(GameConsoleLineKind Kind, string Text, DateTime Utc);

/// <summary>
/// Simple in-memory console log (global UI overlay consumes this).
/// Designed to stay safe for long-running sessions (bounded history + trim events).
/// </summary>
public sealed class GameConsole
{
	private readonly List<GameConsoleLine> _lines = new();

	/// <summary>
	/// Raised when a new line is appended (no trimming occurred).
	/// </summary>
	public event Action<GameConsoleLine>? LineAdded;

	/// <summary>
	/// Raised when the log is cleared or trimmed (UI should rebuild from <see cref="Lines"/>).
	/// </summary>
	public event Action? LinesReset;

	public IReadOnlyList<GameConsoleLine> Lines => _lines;

	/// <summary>
	/// Hard cap on stored lines (prevents unbounded growth).
	/// </summary>
	public int MaxLines { get; set; } = 500;

	/// <summary>
	/// When exceeding <see cref="MaxLines"/>, trim at least this many lines at once.
	/// (Batch trimming avoids frequent O(n) removals.)
	/// </summary>
	public int TrimBatchSize { get; set; } = 75;

	/// <summary>
	/// Clears the log. Optionally preserves the last N lines (useful for "clear" command echo).
	/// </summary>
	public void Clear(int preserveTailLines = 0)
	{
		if (_lines.Count == 0) return;

		if (preserveTailLines <= 0 || preserveTailLines >= _lines.Count)
		{
			_lines.Clear();
			LinesReset?.Invoke();
			return;
		}

		// Keep only the most recent tail lines.
		var keepStart = _lines.Count - preserveTailLines;
		_lines.RemoveRange(0, keepStart);
		LinesReset?.Invoke();
	}

	public void Write(GameConsoleLineKind kind, string text)
	{
		if (string.IsNullOrWhiteSpace(text))
			return;

		var line = new GameConsoleLine(kind, text.Trim(), DateTime.UtcNow);
		_lines.Add(line);

		// Trim in batches to keep long sessions stable.
		if (_lines.Count > MaxLines)
		{
			var over = _lines.Count - MaxLines;
			var remove = Math.Max(TrimBatchSize, over);
			if (remove > _lines.Count)
				remove = _lines.Count;
			_lines.RemoveRange(0, remove);
			LinesReset?.Invoke();
			return; // UI will rebuild, which includes this line.
		}

		LineAdded?.Invoke(line);
	}

	public void Debug(string text) => Write(GameConsoleLineKind.Debug, text);
	public void Status(string text) => Write(GameConsoleLineKind.Status, text);
	public void Input(string text) => Write(GameConsoleLineKind.Input, text);
	public void Error(string text) => Write(GameConsoleLineKind.Error, text);
}

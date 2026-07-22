// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Arena/ArenaHazardTelemetry.cs
// Purpose: Lightweight shared registry of active arena hazards (mines/oil/smoke) for HUD consumers.
// -------------------------------------------------------------------------------------------------
using System.Collections.Generic;
using Godot;

namespace WastelandSurvivor.Game.Arena;

public enum ArenaHazardKind
{
	Mine,
	Oil,
	Smoke,
}

public readonly record struct ArenaHazardBlip(Vector3 Position, ArenaHazardKind Kind);

/// <summary>
/// ArenaRealtimeView republishes the active hazard set here (10 Hz, main thread only) so HUD widgets
/// like the radar can render hazard blips without holding references into the combat view's private
/// runtime lists. Only hazards the player should know about belong here (e.g. the player's own mines,
/// but not hidden enemy mines).
/// </summary>
public static class ArenaHazardTelemetry
{
	private static readonly List<ArenaHazardBlip> _blips = new();

	public static IReadOnlyList<ArenaHazardBlip> Blips => _blips;

	public static void BeginUpdate() => _blips.Clear();

	public static void Add(Vector3 position, ArenaHazardKind kind) => _blips.Add(new ArenaHazardBlip(position, kind));

	public static void Clear() => _blips.Clear();
}

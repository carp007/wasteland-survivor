// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Game/Session/SessionContext.cs
// Purpose: Owns the in-memory SaveGameState and is the only component allowed to replace/persist it. Provides safe mutation helpers and console logging.
// -------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using WastelandSurvivor.Core.IO;
using WastelandSurvivor.Core.State;

namespace WastelandSurvivor.Game.Session;

/// <summary>
/// Owns the mutable in-memory SaveGameState and is the only place allowed to replace/persist it.
/// This is the backbone that lets higher-level services stay small and focused.
/// </summary>
internal sealed class SessionContext
{
    private readonly SaveGameStore _store;
    private readonly Func<GameConsole?> _console;

    public SaveGameState Save { get; private set; }

    public event Action<SaveGameState>? SaveChanged;

    public SessionContext(SaveGameStore store, SaveGameState save, Func<GameConsole?> consoleGetter)
    {
        _store = store;
        Save = save;
        _console = consoleGetter;
    }

    public void Persist() => _store.Save(Save);

    public void Replace(SaveGameState next, bool persist = true)
    {
        Save = next;
        if (persist)
            _store.Save(Save);
        SaveChanged?.Invoke(Save);
    }

    public void Debug(string text) => _console()?.Debug(text);
    public void Status(string text) => _console()?.Status(text);
    public void Input(string text) => _console()?.Input(text);
    public void Error(string text) => _console()?.Error(text);

    /// <summary>
    /// Helper: find a vehicle in Save.Vehicles, apply a mutation, optionally mutate Player, then Persist().
    /// Intended to reduce repeated "find -> mutate -> replace -> persist" boilerplate.
    /// </summary>
    public bool TryMutateVehicleAndPlayer(
        string vehicleInstanceId,
        string missingVehicleError,
        Func<VehicleInstanceState, VehicleInstanceState> vehicleMutator,
        Func<PlayerProfileState, PlayerProfileState>? playerMutator,
        out VehicleInstanceState updatedVehicle,
        out string error)
    {
        error = "";
        updatedVehicle = default!;

        if (string.IsNullOrWhiteSpace(vehicleInstanceId))
        {
            error = missingVehicleError;
            return false;
        }
        if (vehicleMutator is null)
        {
            error = "Internal error: vehicle mutator is null.";
            return false;
        }

        // Clone the list so we never mutate Save.Vehicles in place.
        var vehicles = new List<VehicleInstanceState>(Save.Vehicles);
        var idx = vehicles.FindIndex(v => string.Equals(v.InstanceId, vehicleInstanceId, StringComparison.Ordinal));
        if (idx < 0)
        {
            error = missingVehicleError;
            return false;
        }

        var baseVeh = vehicles[idx];
        updatedVehicle = vehicleMutator(baseVeh);
        vehicles[idx] = updatedVehicle;

        var player = playerMutator is null ? Save.Player : playerMutator(Save.Player);

        Replace(Save with
        {
            Vehicles = vehicles,
            Player = player
        });
        return true;
    }

    /// <summary>
    /// Convenience helper: mutate the active vehicle + optionally player, then Persist().
    /// </summary>
    public bool TryMutateActiveVehicleAndPlayer(
        Func<VehicleInstanceState, VehicleInstanceState> vehicleMutator,
        Func<PlayerProfileState, PlayerProfileState>? playerMutator,
        out VehicleInstanceState updatedVehicle,
        out string error)
    {
        error = "";
        updatedVehicle = default!;

        var activeId = Save.Player.ActiveVehicleId;
        if (string.IsNullOrWhiteSpace(activeId))
        {
            error = "No active vehicle.";
            return false;
        }

        return TryMutateVehicleAndPlayer(
            activeId,
            missingVehicleError: "Active vehicle missing.",
            vehicleMutator: vehicleMutator,
            playerMutator: playerMutator,
            out updatedVehicle,
            out error);
    }
}

// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Core/State/FreightContractState.cs
// Purpose: Serializable runtime/persisted state records (SaveGameState and related sub-records).
// -------------------------------------------------------------------------------------------------

namespace WastelandSurvivor.Core.State;

/// <summary>
/// Active freight-hauling contract (spec: cargo/logistics pillar — storage capacity, cargo weight,
/// trailers). Accepted at a city freight office; the cargo units live in vehicle CargoInventory
/// (so they weigh the chain down through the normal mass math) and pay out on arrival at the
/// destination city. One active contract at a time (v1).
/// </summary>
public sealed record FreightContractState
{
    public string ContractId { get; init; } = "";
    public string OriginCityId { get; init; } = "";
    public string DestCityId { get; init; } = "";

    /// <summary>CargoInventory key for the hauled commodity (always "freight_*" prefixed).</summary>
    public string CargoId { get; init; } = "";
    public string CargoDisplayName { get; init; } = "";
    public int CargoUnits { get; init; }
    public int PayoutUsd { get; init; }
}

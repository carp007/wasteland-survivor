using System;

namespace WastelandSurvivor.Game.Session;

/// <summary>
/// World/session-level state (city, world flags, etc.).
/// </summary>
internal sealed class SessionWorld
{
    private readonly SessionContext _ctx;

    public SessionWorld(SessionContext ctx)
    {
        _ctx = ctx;
    }

    public void SetCurrentCity(string cityId)
    {
        cityId = (cityId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(cityId))
            return;

        _ctx.Replace(_ctx.Save with { Player = _ctx.Save.Player with { CurrentCityId = cityId } });
        _ctx.Status($"City: {cityId}");
    }

	public (int missingPoints, int costUsd) ComputeDriverArmorRepairCost()
	{
		var p = _ctx.Save.Player;
		var max = p.DriverArmorMax;
		if (max <= 0) max = GameBalance.DefaultDriverArmorMax;
		var cur = Math.Clamp(p.DriverArmor, 0, max);
		var missing = Math.Max(0, max - cur);
		var cost = missing * GameBalance.DriverArmorRepairCostPerPointUsd;
		return (missing, cost);
	}

	public bool TryRepairDriverArmorToFull(out string error)
	{
		error = "";
		var p = _ctx.Save.Player;
		var max = p.DriverArmorMax;
		if (max <= 0) max = GameBalance.DefaultDriverArmorMax;
		var cur = Math.Clamp(p.DriverArmor, 0, max);
		var missing = Math.Max(0, max - cur);
		if (missing <= 0)
		{
			error = "Armor already full.";
			return false;
		}

		var cost = missing * GameBalance.DriverArmorRepairCostPerPointUsd;
		if (p.MoneyUsd < cost)
		{
			error = $"Not enough money. Need ${cost}.";
			return false;
		}

		_ctx.Replace(_ctx.Save with
		{
			Player = p with
			{
				MoneyUsd = p.MoneyUsd - cost,
				DriverArmorMax = max,
				DriverArmor = max,
			}
		});
		_ctx.Status($"Driver armor repaired to full (-${cost}).");
		return true;
	}
}

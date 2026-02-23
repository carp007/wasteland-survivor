using System;
using System.Collections.Generic;
using WastelandSurvivor.Core.Defs;
using WastelandSurvivor.Core.State;

namespace WastelandSurvivor.Game.Systems;

internal static class VehicleCombatMath
{
    /// <summary>
    /// Prototype helper: sums structural HP across all sections + tires.
    /// Note: gameplay is moving away from a single vehicle HP pool; this is mostly for debug/UI.
    /// </summary>
    public static int ComputeVehicleHp(VehicleInstanceState v)
    {
        var hpBySection = v.CurrentHpBySection;
        var hpSum = 0;
        if (hpBySection is not null)
        {
            foreach (var kv in hpBySection)
                hpSum += Math.Max(0, kv.Value);
        }

        var tires = v.CurrentTireHp ?? Array.Empty<int>();
        var tireSum = 0;
        for (var i = 0; i < tires.Length; i++)
            tireSum += Math.Max(0, tires[i]);

        return hpSum + tireSum;
    }

    public static VehicleInstanceState EnsureDamageState(VehicleInstanceState v, VehicleDefinition def)
    {
        var armor = v.CurrentArmorBySection is not null
            ? new Dictionary<ArmorSection, int>(v.CurrentArmorBySection)
            : new Dictionary<ArmorSection, int>();

        var hp = v.CurrentHpBySection is not null
            ? new Dictionary<ArmorSection, int>(v.CurrentHpBySection)
            : new Dictionary<ArmorSection, int>();

        var armorLevel = Math.Max(0, v.ArmorPlatingLevel);
        foreach (var s in Enum.GetValues<ArmorSection>())
        {
            def.BaseArmorBySection.TryGetValue(s, out var baseArmor);
            def.BaseHpBySection.TryGetValue(s, out var baseHp);

            var maxArmor = Math.Max(0, baseArmor + armorLevel);
            var maxHp = Math.Max(0, baseHp);

            if (!armor.TryGetValue(s, out var curArmor)) curArmor = maxArmor;
            if (!hp.TryGetValue(s, out var curHp)) curHp = maxHp;

            armor[s] = Math.Clamp(curArmor, 0, maxArmor);
            hp[s] = Math.Clamp(curHp, 0, maxHp);
        }

        var tireCount = Math.Max(0, def.TireCount);
        var tireArmorLevel = Math.Max(0, v.TirePlatingLevel);
        var maxTireArmor = Math.Max(0, def.BaseTireArmor + tireArmorLevel);
        var maxTireHp = Math.Max(0, def.BaseTireHp);

        var initTireArmor = v.CurrentTireArmor is not { Length: > 0 } || v.CurrentTireArmor.Length != tireCount;
        var tiresArmor = !initTireArmor ? (int[])v.CurrentTireArmor.Clone() : new int[tireCount];
        if (tiresArmor.Length != tireCount)
        {
            var resized = new int[tireCount];
            var copyLen = Math.Min(tireCount, tiresArmor.Length);
            for (var i = 0; i < copyLen; i++) resized[i] = tiresArmor[i];
            tiresArmor = resized;
        }

        var initTireHp = v.CurrentTireHp is not { Length: > 0 } || v.CurrentTireHp.Length != tireCount;
        var tiresHp = !initTireHp ? (int[])v.CurrentTireHp.Clone() : new int[tireCount];
        if (tiresHp.Length != tireCount)
        {
            var resized = new int[tireCount];
            var copyLen = Math.Min(tireCount, tiresHp.Length);
            for (var i = 0; i < copyLen; i++) resized[i] = tiresHp[i];
            tiresHp = resized;
        }

        for (var i = 0; i < tireCount; i++)
        {
            if (initTireArmor) tiresArmor[i] = maxTireArmor;
            if (initTireHp) tiresHp[i] = maxTireHp;
            tiresArmor[i] = Math.Clamp(tiresArmor[i], 0, maxTireArmor);
            tiresHp[i] = Math.Clamp(tiresHp[i], 0, maxTireHp);
        }

        return v with
        {
            CurrentArmorBySection = armor,
            CurrentHpBySection = hp,
            CurrentTireArmor = tiresArmor,
            CurrentTireHp = tiresHp
        };
    }

    public static int GetMaxArmorForSection(VehicleInstanceState v, VehicleDefinition def, ArmorSection section)
    {
        def.BaseArmorBySection.TryGetValue(section, out var baseArmor);
        return Math.Max(0, baseArmor + Math.Max(0, v.ArmorPlatingLevel));
    }

    public static int GetMaxHpForSection(VehicleDefinition def, ArmorSection section)
    {
        def.BaseHpBySection.TryGetValue(section, out var baseHp);
        return Math.Max(0, baseHp);
    }

    public static int GetMaxTireArmor(VehicleInstanceState v, VehicleDefinition def)
        => Math.Max(0, def.BaseTireArmor + Math.Max(0, v.TirePlatingLevel));

    public static int GetMaxTireHp(VehicleDefinition def)
        => Math.Max(0, def.BaseTireHp);

    public static VehicleInstanceState ApplyDamageToSection(
        VehicleInstanceState v,
        VehicleDefinition def,
        ArmorSection section,
        int damage,
        out int remainingDamage)
    {
        remainingDamage = Math.Max(0, damage);
        if (remainingDamage <= 0) return v;

        v = EnsureDamageState(v, def);

        var armor = new Dictionary<ArmorSection, int>(v.CurrentArmorBySection);
        var hp = new Dictionary<ArmorSection, int>(v.CurrentHpBySection);

        armor.TryGetValue(section, out var aCur);
        hp.TryGetValue(section, out var hCur);

        var absorbedArmor = Math.Min(Math.Max(0, aCur), remainingDamage);
        aCur = Math.Max(0, aCur - absorbedArmor);
        remainingDamage -= absorbedArmor;

        var absorbedHp = 0;
        if (remainingDamage > 0)
        {
            absorbedHp = Math.Min(Math.Max(0, hCur), remainingDamage);
            hCur = Math.Max(0, hCur - absorbedHp);
            remainingDamage -= absorbedHp;
        }

        armor[section] = aCur;
        hp[section] = hCur;

        return v with { CurrentArmorBySection = armor, CurrentHpBySection = hp };
    }

    public static VehicleInstanceState ApplyDamageToTire(
        VehicleInstanceState v,
        VehicleDefinition def,
        int tireIndex,
        int damage,
        out int remainingDamage)
    {
        remainingDamage = Math.Max(0, damage);
        if (remainingDamage <= 0) return v;

        v = EnsureDamageState(v, def);

        var tireCount = Math.Max(0, def.TireCount);
        if (tireCount <= 0) return v;
        tireIndex = Math.Clamp(tireIndex, 0, tireCount - 1);

        var tiresArmor = (int[])v.CurrentTireArmor.Clone();
        var tiresHp = (int[])v.CurrentTireHp.Clone();

        var aCur = Math.Max(0, tiresArmor[tireIndex]);
        var hCur = Math.Max(0, tiresHp[tireIndex]);

        var absorbedArmor = Math.Min(aCur, remainingDamage);
        aCur -= absorbedArmor;
        remainingDamage -= absorbedArmor;

        if (remainingDamage > 0)
        {
            var absorbedHp = Math.Min(hCur, remainingDamage);
            hCur -= absorbedHp;
            remainingDamage -= absorbedHp;
        }

        tiresArmor[tireIndex] = Math.Max(0, aCur);
        tiresHp[tireIndex] = Math.Max(0, hCur);

        return v with { CurrentTireArmor = tiresArmor, CurrentTireHp = tiresHp };
    }

    /// <summary>
    /// Back-compat: apply damage in a deterministic order across sections and tires.
    /// This version does not need defs; it only reduces whatever pools exist on the instance.
    /// </summary>
    public static VehicleInstanceState ApplyDamageToVehicle(VehicleInstanceState v, int damage)
    {
        var remaining = Math.Max(0, damage);
        if (remaining <= 0) return v;

        var armorBySection = v.CurrentArmorBySection is not null
            ? new Dictionary<ArmorSection, int>(v.CurrentArmorBySection)
            : new Dictionary<ArmorSection, int>();

        var hpBySection = v.CurrentHpBySection is not null
            ? new Dictionary<ArmorSection, int>(v.CurrentHpBySection)
            : new Dictionary<ArmorSection, int>();

        var order = new[]
        {
            ArmorSection.Front,
            ArmorSection.Left,
            ArmorSection.Right,
            ArmorSection.Rear,
            ArmorSection.Top,
            ArmorSection.Undercarriage
        };

        foreach (var s in order)
        {
            if (remaining <= 0) break;

            armorBySection.TryGetValue(s, out var aCur);
            aCur = Math.Max(0, aCur);
            var absorbed = Math.Min(aCur, remaining);
            aCur -= absorbed;
            remaining -= absorbed;
            armorBySection[s] = aCur;

            if (remaining <= 0) break;

            hpBySection.TryGetValue(s, out var hCur);
            hCur = Math.Max(0, hCur);
            absorbed = Math.Min(hCur, remaining);
            hCur -= absorbed;
            remaining -= absorbed;
            hpBySection[s] = hCur;
        }

        var tiresArmor = v.CurrentTireArmor is { Length: > 0 } ? (int[])v.CurrentTireArmor.Clone() : new[] { 0, 0, 0, 0 };
        var tiresHp = v.CurrentTireHp is { Length: > 0 } ? (int[])v.CurrentTireHp.Clone() : new[] { 0, 0, 0, 0 };

        for (var i = 0; i < tiresArmor.Length && remaining > 0; i++)
        {
            var aCur = Math.Max(0, tiresArmor[i]);
            var absorbed = Math.Min(aCur, remaining);
            tiresArmor[i] = aCur - absorbed;
            remaining -= absorbed;

            if (remaining <= 0) break;

            var hCur = (i < tiresHp.Length) ? Math.Max(0, tiresHp[i]) : 0;
            absorbed = Math.Min(hCur, remaining);
            tiresHp[i] = hCur - absorbed;
            remaining -= absorbed;
        }

        return v with
        {
            CurrentArmorBySection = armorBySection,
            CurrentHpBySection = hpBySection,
            CurrentTireArmor = tiresArmor,
            CurrentTireHp = tiresHp
        };
    }
}

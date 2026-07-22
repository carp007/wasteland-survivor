// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Arena/VehicleOutfitting.cs
// Purpose: Procedural "wasteland outfitting" greebles for live combat vehicles. The Kenney Car Kit
//          bodies are single smooth meshes, so even with the grounded hull shader they read as
//          clean plastic toys at combat zoom. This attaches small cheap primitive-mesh add-ons
//          (bullbar, roof cargo, exhaust stacks, whip antenna, skirt plates, spare tire) anchored
//          to the measured hull AABB so the silhouette reads "outfitted survivor machine".
// -------------------------------------------------------------------------------------------------
using System;
using Godot;
using WastelandSurvivor.Core.Defs;

namespace WastelandSurvivor.Game.Arena;

/// <summary>
/// Static builder for the wasteland outfitting layer on live combat vehicles.
///
/// Design constraints (loop 6 greeble pass):
/// - PRIMITIVE meshes only (Box/Cylinder/Capsule, low segment counts), CastShadow off.
/// - Matte grounded palette matching <see cref="VehicleHullDetailer"/> rust/steel tones; NO emissive.
/// - Deterministic per (definition id + model path): FNV-1a seed, one System.Random, fixed call order.
/// - Everything anchors to the measured hull AABB in "Visual"-space (hull shapes vary per class)
///   and scales with the def's visual footprint.
/// - Subtle over loud: the goal is silhouette articulation at the RTS zoom, not Mad Max cosplay.
///
/// The greeble root is parented under the pawn's "Visual" node so a def-model rebuild
/// (ClearChildren) wipes it together with the hull. Trailers and salvage-yard derelicts never get
/// outfitting (derelicts are dressed by ArenaWorld, which doesn't run this path at all).
/// Weapon-mount clearance: top mounts sit at (0, 0.95, 0) and front/rear at (0, 0.62, -/+1.68) in
/// pawn space, so roof cargo stays low and biased to the rear half of the roof, the bullbar stays
/// below y=0.55, and the rear spare is offset to one side.
/// </summary>
public static class VehicleOutfitting
{
    /// <summary>Node name of the greeble root. AABB measurement in VehiclePawn skips this subtree
    /// so antennas/stacks never inflate identity-VFX or HUD anchor heights.</summary>
    public const string NodeName = "WsOutfitting";

    // ---- grounded palette (matches VehicleHullDetailer rust/steel tones) ----
    private static readonly Color DarkSteel = new(0.175f, 0.18f, 0.19f);
    private static readonly Color Gunmetal = new(0.155f, 0.16f, 0.17f);
    private static readonly Color RustBrown = new(0.30f, 0.185f, 0.11f);
    private static readonly Color RustDark = new(0.24f, 0.14f, 0.075f);
    private static readonly Color OliveFaded = new(0.30f, 0.31f, 0.21f);
    private static readonly Color CanvasTan = new(0.42f, 0.365f, 0.26f);
    private static readonly Color Rubber = new(0.085f, 0.088f, 0.095f);
    private static readonly Color SteelWorn = new(0.33f, 0.34f, 0.36f);

    private static readonly System.Collections.Generic.Dictionary<ulong, StandardMaterial3D> MatCache = new();

    /// <summary>
    /// FNV-1a seed over (definition id + "|" + model path). string.GetHashCode is randomized per
    /// process — never use it for deterministic art (same rule as VehicleHullDetailer).
    /// </summary>
    public static int ComputeSeed(string? defId, string? modelPath)
    {
        var seed = unchecked((int)2166136261u);
        foreach (var c in (defId ?? string.Empty) + "|" + (modelPath ?? string.Empty))
            seed = unchecked((seed ^ c) * 16777619);
        return seed;
    }

    /// <summary>
    /// Attaches the outfitting layer under <paramref name="visualRoot"/>. All positions are in
    /// visual-root space; <paramref name="hullBounds"/> is the def model's measured AABB in that
    /// same space (bottom resting at y=0, XZ centered, forward = -Z).
    /// </summary>
    public static void Attach(
        Node3D visualRoot, VehicleDefinition? def, string archetype, int seed,
        float hullLengthMeters, Aabb hullBounds)
    {
        if (visualRoot == null || !GodotObject.IsInstanceValid(visualRoot)) return;
        if (def?.Class == VehicleClass.Trailer) return;               // trailers get NO outfitting
        if (archetype == "boxtop") return;                            // trailer-ish box bodies
        if (hullBounds.Size.X < 0.4f || hullBounds.Size.Z < 1.0f || hullBounds.Size.Y < 0.3f) return;

        // Re-entrant safety: a rebuild clears "Visual" wholesale, but ApplyVisualPreset can re-run
        // in the same frame before queued frees land.
        if (visualRoot.GetNodeOrNull<Node3D>(NodeName) is { } stale && GodotObject.IsInstanceValid(stale))
        {
            visualRoot.RemoveChild(stale);
            stale.QueueFree();
        }

        var root = new Node3D { Name = NodeName };
        visualRoot.AddChild(root);

        var rng = new Random(seed);

        var len = hullBounds.Size.Z;
        var frontZ = hullBounds.Position.Z;
        var rearZ = hullBounds.End.Z;
        var topY = hullBounds.End.Y;
        var halfW = hullBounds.Size.X * 0.5f;
        var cx = hullBounds.Position.X + halfW;
        var s = Mathf.Clamp((hullLengthMeters > 0.1f ? hullLengthMeters : len) / 3.25f, 0.7f, 1.8f);

        var cls = def?.Class ?? VehicleClass.Sedan;
        var heavyNose = archetype is "truck" or "industrial" or "van";

        // 1) Front bullbar / ram guard (class-scaled; below the y=0.62 front-mount line).
        if (heavyNose)
            BuildRamPlate(root, rng, cx, halfW, topY, frontZ, s);
        else
            BuildTubeBullbar(root, rng, cx, halfW, topY, frontZ, s);

        // 2) Roof rack with strapped cargo (wagon-ish classes) OR exhaust stacks (trucks/semi).
        var wantsStacks = archetype is "truck" or "industrial";
        if (wantsStacks)
        {
            var stacks = cls == VehicleClass.SemiTruck || archetype == "industrial" ? 2 : 1;
            BuildExhaustStacks(root, rng, cx, halfW, topY, frontZ, len, s, stacks,
                cabRearFrac: archetype == "industrial" ? 0.36f : 0.44f);
        }
        else
        {
            var (r0, r1, rw) = archetype switch
            {
                "suv" => (0.40f, 0.80f, 0.60f),
                "van" => (0.32f, 0.88f, 0.62f),
                "sports" => (0.48f, 0.64f, 0.52f),
                _ => (0.46f, 0.64f, 0.56f), // sedan/taxi/police
            };
            // Sports-class racers keep a clean roofline; compacts (hatchback bodies resolve as
            // "sports" archetype) still get the small survival basket.
            var wantsRack = cls != VehicleClass.Sports;
            if (wantsRack)
                BuildRoofRack(root, rng, cx, halfW * rw, topY, frontZ + r0 * len, frontZ + r1 * len, s,
                    bigTarp: archetype == "van");
        }

        // 3) Whip antenna (rear roof corner, raked back; thin enough to vanish at 1x zoom).
        BuildAntenna(root, rng, cx, halfW, topY, frontZ, rearZ, len, s, archetype);

        // 4) Skirt plates + rear mud flaps where the hull AABB allows. Sports-class bodies are
        //    open-wheeled (race/race-future): the hull AABB spans the exposed wheels, so side
        //    plates anchored to it would float in mid-air beside the cockpit — skip them.
        if (cls != VehicleClass.Sports)
            BuildSkirtsAndFlaps(root, rng, cx, halfW, topY, frontZ, rearZ, len, s);

        // 5) Per-archetype extras.
        if (cls is VehicleClass.Compact or VehicleClass.Suv)
            BuildRearSpare(root, rng, cx, halfW, topY, rearZ, s);
        if (!heavyNose)
            BuildTowHooks(root, rng, cx, halfW, topY, frontZ, s);
    }

    // ---------------------------------------------------------------- pieces

    /// <summary>Two horizontal steel tubes + uprights + hull struts ahead of the nose.</summary>
    private static void BuildTubeBullbar(Node3D root, Random rng, float cx, float halfW, float topY, float frontZ, float s)
    {
        var mat = Steel(rng.NextDouble() < 0.35 ? RustBrown : DarkSteel, metallic: 0.30f, rough: 0.72f);
        var barZ = frontZ - 0.07f * s;
        var barLen = halfW * 1.44f;
        var y0 = topY * 0.28f;
        var y1 = topY * 0.45f;
        var tubeR = 0.032f * s;

        AddCylinder(root, mat, tubeR, barLen, new Vector3(cx, y0, barZ), new Vector3(0f, 0f, 90f));
        AddCylinder(root, mat, tubeR, barLen, new Vector3(cx, y1, barZ), new Vector3(0f, 0f, 90f));
        foreach (var sx in stackalloc float[] { -1f, 1f })
        {
            var x = cx + sx * halfW * 0.46f;
            AddCylinder(root, mat, tubeR * 0.85f, y1 - y0 + 0.14f * s, new Vector3(x, (y0 + y1) * 0.5f, barZ), Vector3.Zero);
            // strut back to the hull nose
            AddBox(root, mat, new Vector3(0.045f * s, 0.045f * s, 0.14f * s),
                new Vector3(x, (y0 + y1) * 0.5f, frontZ - 0.005f * s), Vector3.Zero);
        }
    }

    /// <summary>Heavy leaned-back ram plate with vertical ribs for trucks/vans/industrial noses.</summary>
    private static void BuildRamPlate(Node3D root, Random rng, float cx, float halfW, float topY, float frontZ, float s)
    {
        var plateMat = Steel(DarkSteel, metallic: 0.28f, rough: 0.78f);
        var ribMat = Steel(rng.NextDouble() < 0.5 ? RustBrown : RustDark, metallic: 0.10f, rough: 0.92f);
        var h = topY * 0.34f;
        var y = topY * 0.30f;
        var z = frontZ - 0.055f * s;

        var plate = AddBox(root, plateMat, new Vector3(halfW * 1.66f, h, 0.05f * s), new Vector3(cx, y, z), Vector3.Zero);
        plate.RotationDegrees = new Vector3(-8f, 0f, 0f); // top edge leans back onto the hull
        // top edge tube so the plate doesn't read as a flat sticker
        AddCylinder(root, plateMat, 0.030f * s, halfW * 1.70f,
            new Vector3(cx, y + h * 0.52f, z - 0.02f * s), new Vector3(0f, 0f, 90f));
        foreach (var sx in stackalloc float[] { -1f, 1f })
            AddBox(root, ribMat, new Vector3(0.06f * s, h * 0.92f, 0.025f * s),
                new Vector3(cx + sx * halfW * 0.32f, y, z - 0.028f * s), new Vector3(-8f, 0f, 0f));
        // rust tow shackles on the plate face
        foreach (var sx in stackalloc float[] { -1f, 1f })
            AddBox(root, ribMat, new Vector3(0.05f * s, 0.05f * s, 0.05f * s),
                new Vector3(cx + sx * halfW * 0.34f, y - h * 0.30f, z - 0.035f * s), Vector3.Zero);
    }

    /// <summary>Side rails + cross bars with a few strapped cargo lumps, biased to the rear half of
    /// the roof so the (0, 0.95, 0) top weapon mount stays clear.</summary>
    private static void BuildRoofRack(Node3D root, Random rng, float cx, float rackHalfW, float topY,
        float z0, float z1, float s, bool bigTarp)
    {
        var railMat = Steel(Gunmetal, metallic: 0.30f, rough: 0.70f);
        var rackLen = z1 - z0;
        if (rackLen < 0.4f * s) return;
        var railY = topY + 0.045f * s;
        var zc = (z0 + z1) * 0.5f;

        var footZ0 = z0 + rackLen * 0.12f;
        var footZ1 = z1 - rackLen * 0.12f;
        foreach (var sx in stackalloc float[] { -1f, 1f })
        {
            AddBox(root, railMat, new Vector3(0.035f * s, 0.030f * s, rackLen), new Vector3(cx + sx * rackHalfW, railY, zc), Vector3.Zero);
            // feet
            AddBox(root, railMat, new Vector3(0.028f * s, 0.06f * s, 0.028f * s), new Vector3(cx + sx * rackHalfW, topY + 0.015f * s, footZ0), Vector3.Zero);
            AddBox(root, railMat, new Vector3(0.028f * s, 0.06f * s, 0.028f * s), new Vector3(cx + sx * rackHalfW, topY + 0.015f * s, footZ1), Vector3.Zero);
        }
        var crossCount = rackLen > 1.4f * s ? 3 : 2;
        for (var i = 0; i < crossCount; i++)
        {
            var fz = Mathf.Lerp(z0 + rackLen * 0.10f, z1 - rackLen * 0.10f, crossCount == 1 ? 0.5f : i / (float)(crossCount - 1));
            AddBox(root, railMat, new Vector3(rackHalfW * 2f, 0.024f * s, 0.035f * s), new Vector3(cx, railY, fz), Vector3.Zero);
        }

        // Cargo lumps sit on the REAR HALF of the rack (top turret clearance) and stay low.
        var cargoY = railY + 0.02f * s;
        var strapMat = Steel(Rubber, metallic: 0.0f, rough: 0.95f);
        if (bigTarp)
        {
            // One long strapped tarp roll along the van roof.
            var tarpMat = Matte(rng.NextDouble() < 0.5 ? OliveFaded : CanvasTan);
            var tarpLen = rackLen * 0.52f;
            var tz = zc + rackLen * 0.18f;
            AddCapsule(root, tarpMat, 0.13f * s, tarpLen, new Vector3(cx + 0.08f * s, cargoY + 0.10f * s, tz), new Vector3(90f, 0f, 0f));
            foreach (var fz in stackalloc float[] { tz - tarpLen * 0.28f, tz + tarpLen * 0.28f })
                AddBox(root, strapMat, new Vector3(0.30f * s, 0.012f * s, 0.03f * s), new Vector3(cx + 0.08f * s, cargoY + 0.21f * s, fz), Vector3.Zero);
        }

        var lumpCount = bigTarp ? 1 : 2 + (rng.NextDouble() < 0.5 ? 1 : 0);
        for (var i = 0; i < lumpCount; i++)
        {
            var fz = Mathf.Lerp(zc + rackLen * 0.02f, z1 - rackLen * 0.16f, lumpCount == 1 ? 0.5f : i / (float)Math.Max(1, lumpCount - 1));
            var fx = cx + ((i % 2 == 0) ? -1f : 1f) * rackHalfW * (0.28f + 0.18f * (float)rng.NextDouble());
            var yaw = ((float)rng.NextDouble() - 0.5f) * 18f;
            if (rng.NextDouble() < 0.55)
            {
                // strapped crate
                var cmat = Matte(rng.NextDouble() < 0.5 ? OliveFaded : (rng.NextDouble() < 0.5 ? CanvasTan : RustBrown));
                var size = new Vector3(0.30f, 0.17f, 0.26f) * s * (0.85f + 0.3f * (float)rng.NextDouble());
                AddBox(root, cmat, size, new Vector3(fx, cargoY + size.Y * 0.5f, fz), new Vector3(0f, yaw, 0f));
                AddBox(root, strapMat, new Vector3(size.X + 0.03f * s, 0.012f * s, 0.035f * s),
                    new Vector3(fx, cargoY + size.Y + 0.004f * s, fz), new Vector3(0f, yaw, 0f));
            }
            else
            {
                // duffel roll
                var dmat = Matte(rng.NextDouble() < 0.5 ? CanvasTan : OliveFaded);
                var dlen = (0.34f + 0.14f * (float)rng.NextDouble()) * s;
                AddCapsule(root, dmat, 0.085f * s, dlen, new Vector3(fx, cargoY + 0.07f * s, fz), new Vector3(90f, yaw, 0f));
                AddBox(root, strapMat, new Vector3(0.19f * s, 0.010f * s, 0.028f * s),
                    new Vector3(fx, cargoY + 0.145f * s, fz), new Vector3(0f, yaw, 0f));
            }
        }
    }

    /// <summary>One or two vertical exhaust stacks just behind the cab, outboard of the bed walls.</summary>
    private static void BuildExhaustStacks(Node3D root, Random rng, float cx, float halfW, float topY,
        float frontZ, float len, float s, int count, float cabRearFrac)
    {
        var stackMat = Steel(Gunmetal, metallic: 0.42f, rough: 0.55f);
        var tipMat = Steel(SteelWorn, metallic: 0.50f, rough: 0.45f);
        var z = frontZ + cabRearFrac * len;
        var baseY = topY * 0.42f;
        var height = topY + 0.24f * s - baseY;
        Span<float> sides = count >= 2 ? stackalloc float[] { -1f, 1f } : stackalloc float[] { rng.NextDouble() < 0.5 ? -1f : 1f };
        foreach (var sx in sides)
        {
            var x = cx + sx * halfW * 0.86f;
            AddCylinder(root, stackMat, 0.037f * s, height, new Vector3(x, baseY + height * 0.5f, z), Vector3.Zero);
            AddCylinder(root, tipMat, 0.042f * s, 0.055f * s, new Vector3(x, baseY + height - 0.02f * s, z), Vector3.Zero);
            // heat shield bracket against the cab
            AddBox(root, stackMat, new Vector3(0.03f * s, 0.16f * s, 0.06f * s),
                new Vector3(x - sx * 0.045f * s, baseY + height * 0.30f, z), Vector3.Zero);
        }
    }

    /// <summary>Thin raked whip antenna on a rear corner (visible in portraits, subpixel at 1x).</summary>
    private static void BuildAntenna(Node3D root, Random rng, float cx, float halfW, float topY,
        float frontZ, float rearZ, float len, float s, string archetype)
    {
        var mat = Steel(Rubber, metallic: 0.15f, rough: 0.80f);
        var side = rng.NextDouble() < 0.5 ? -1f : 1f;
        // Rear of the cab/roof zone; trucks mount it on the cab corner, cars near the C-pillar.
        var zf = archetype switch { "truck" => 0.34f, "industrial" => 0.28f, "van" => 0.30f, _ => 0.60f };
        var pos = new Vector3(cx + side * halfW * 0.58f, topY - 0.02f * s, frontZ + zf * len);
        var h = 0.52f * s;

        var pivot = new Node3D { Position = pos, RotationDegrees = new Vector3(9f, 0f, side * -4f) };
        root.AddChild(pivot);
        AddCylinder(pivot, mat, 0.011f * s, h, new Vector3(0f, h * 0.5f, 0f), Vector3.Zero);
        // base spring block
        AddBox(pivot, mat, new Vector3(0.035f * s, 0.05f * s, 0.035f * s), new Vector3(0f, 0.02f * s, 0f), Vector3.Zero);
    }

    /// <summary>Rocker skirt plates between the axles + rubber mud flaps behind the rear arches.</summary>
    private static void BuildSkirtsAndFlaps(Node3D root, Random rng, float cx, float halfW, float topY,
        float frontZ, float rearZ, float len, float s)
    {
        var skirtMat = Steel(rng.NextDouble() < 0.4 ? RustDark : Gunmetal, metallic: 0.15f, rough: 0.90f);
        var flapMat = Matte(Rubber);

        var skirtLen = len * 0.30f;
        var skirtZ = frontZ + len * 0.52f;
        var skirtH = 0.10f * s;
        var skirtY = Mathf.Max(0.13f * s, topY * 0.17f);
        foreach (var sx in stackalloc float[] { -1f, 1f })
        {
            AddBox(root, skirtMat, new Vector3(0.03f * s, skirtH, skirtLen),
                new Vector3(cx + sx * (halfW - 0.005f * s), skirtY, skirtZ), Vector3.Zero);
            // mud flap tucked behind the rear wheel arch (not past the bumper — reads as jack
            // stands when it hangs off the tail on short-overhang hatchbacks)
            AddBox(root, flapMat, new Vector3(0.13f * s, 0.12f * s, 0.018f * s),
                new Vector3(cx + sx * halfW * 0.80f, 0.13f * s, rearZ - 0.135f * len), Vector3.Zero);
        }
    }

    /// <summary>Side-offset spare tire on the tail (kept clear of the (0,0.62,+1.68) rear mount).</summary>
    private static void BuildRearSpare(Node3D root, Random rng, float cx, float halfW, float topY, float rearZ, float s)
    {
        var side = rng.NextDouble() < 0.5 ? -1f : 1f;
        var r = 0.24f * s;
        var pos = new Vector3(cx + side * halfW * 0.42f, Mathf.Max(r + 0.06f * s, topY * 0.34f), rearZ + 0.045f * s);
        AddCylinder(root, Matte(Rubber), r, 0.10f * s, pos, new Vector3(90f, 0f, 0f));
        AddCylinder(root, Steel(SteelWorn, metallic: 0.35f, rough: 0.60f), r * 0.38f, 0.115f * s, pos, new Vector3(90f, 0f, 0f));
    }

    /// <summary>Small rust tow hooks under the front bumper (garage-portrait detail).</summary>
    private static void BuildTowHooks(Node3D root, Random rng, float cx, float halfW, float topY, float frontZ, float s)
    {
        var mat = Steel(RustBrown, metallic: 0.12f, rough: 0.92f);
        foreach (var sx in stackalloc float[] { -1f, 1f })
            AddBox(root, mat, new Vector3(0.045f * s, 0.04f * s, 0.07f * s),
                new Vector3(cx + sx * halfW * 0.30f, topY * 0.155f, frontZ + 0.015f * s), Vector3.Zero);
    }

    // ---------------------------------------------------------------- primitives + materials

    private static MeshInstance3D AddBox(Node3D parent, StandardMaterial3D mat, Vector3 size, Vector3 pos, Vector3 rotDeg)
    {
        var mi = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            Position = pos,
            RotationDegrees = rotDeg,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        mi.SetSurfaceOverrideMaterial(0, mat);
        parent.AddChild(mi);
        return mi;
    }

    private static MeshInstance3D AddCylinder(Node3D parent, StandardMaterial3D mat, float radius, float height, Vector3 pos, Vector3 rotDeg)
    {
        var mi = new MeshInstance3D
        {
            Mesh = new CylinderMesh
            {
                TopRadius = radius,
                BottomRadius = radius,
                Height = height,
                RadialSegments = 8,
                Rings = 1,
            },
            Position = pos,
            RotationDegrees = rotDeg,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        mi.SetSurfaceOverrideMaterial(0, mat);
        parent.AddChild(mi);
        return mi;
    }

    private static MeshInstance3D AddCapsule(Node3D parent, StandardMaterial3D mat, float radius, float length, Vector3 pos, Vector3 rotDeg)
    {
        var mi = new MeshInstance3D
        {
            Mesh = new CapsuleMesh
            {
                Radius = radius,
                Height = Mathf.Max(length, radius * 2.1f),
                RadialSegments = 8,
                Rings = 4,
            },
            Position = pos,
            RotationDegrees = rotDeg,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        mi.SetSurfaceOverrideMaterial(0, mat);
        parent.AddChild(mi);
        return mi;
    }

    private static StandardMaterial3D Steel(Color c, float metallic, float rough) => GetMat(c, metallic, rough);

    private static StandardMaterial3D Matte(Color c) => GetMat(c, 0.02f, 0.96f);

    /// <summary>Session-cached shared materials (few distinct combos; avoids per-pawn allocations).</summary>
    private static StandardMaterial3D GetMat(Color c, float metallic, float rough)
    {
        var key = ((ulong)c.ToRgba32() << 16)
            | ((ulong)(uint)Mathf.RoundToInt(metallic * 255f) << 8)
            | (uint)Mathf.RoundToInt(rough * 255f);
        if (MatCache.TryGetValue(key, out var cached)) return cached;
        var mat = new StandardMaterial3D
        {
            AlbedoColor = c,
            Metallic = metallic,
            Roughness = rough,
        };
        MatCache[key] = mat;
        return mat;
    }
}

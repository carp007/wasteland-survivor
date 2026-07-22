// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Arena/ArenaRaycastUtil.cs
// Purpose: Shared arena raycast + hitbox interpretation helpers so combat controllers stop duplicating hit parsing.
// -------------------------------------------------------------------------------------------------
using System;
using Godot;
using WastelandSurvivor.Core.Defs;

namespace WastelandSurvivor.Game.Arena;

internal struct ArenaRayHit
{
	public bool Hit;
	public GodotObject? Collider;
	public Vector3 Position;
	public string Part;
	public ArmorSection? Section;
	public int TireIndex;
	public bool DriverHit;
}

internal static class ArenaRaycastUtil
{
	public static ArenaRayHit Raycast(VehiclePawn shooter, Vector3 from, Vector3 to)
		=> RaycastFrom(shooter, from, to);

	/// <summary>
	/// Generalized raycast for any collision-body shooter (vehicle pawn or the on-foot driver):
	/// excludes the shooter's own body + hitbox children, then parses the hit the standard way.
	/// </summary>
	public static ArenaRayHit RaycastFrom(CollisionObject3D shooter, Vector3 from, Vector3 to)
	{
		var hit = new ArenaRayHit
		{
			Hit = false,
			Collider = null,
			Position = to,
			Part = string.Empty,
			Section = null,
			TireIndex = -1,
			DriverHit = false,
		};

		var world = shooter.GetWorld3D();
		if (world == null)
			return hit;

		var space = world.DirectSpaceState;
		var query = PhysicsRayQueryParameters3D.Create(from, to);
		query.CollisionMask = uint.MaxValue;
		query.CollideWithBodies = true;
		query.CollideWithAreas = true;
		query.Exclude = BuildRaycastExcludes(shooter);
		var res = space.IntersectRay(query);
		if (res.Count == 0)
			return hit;

		hit.Hit = true;
		if (res.TryGetValue("collider", out var colVar))
			hit.Collider = colVar.AsGodotObject();
		if (res.TryGetValue("position", out var posVar))
			hit.Position = posVar.AsVector3();

		if (hit.Collider is not Node node)
			return hit;

		if (IsNodeOrParentInGroup(node, "driver_pawn") || IsNodeOrParentInGroup(node, "player_driver"))
		{
			hit.Part = "driver";
			hit.DriverHit = true;
			return hit;
		}

		var cur = node;
		while (cur != null && !cur.IsInGroup("hitbox"))
			cur = cur.GetParent();
		if (cur != null)
			node = cur;

		if (node.IsInGroup("hit_driver"))
		{
			hit.Part = "driver";
			hit.DriverHit = true;
		}
		else if (node.IsInGroup("hit_tire"))
		{
			hit.Part = "tire";
			hit.TireIndex = TireIndexFromHitbox(node);
		}
		else if (node.IsInGroup("hit_section"))
		{
			hit.Section = SectionFromHitbox(node);
			hit.Part = hit.Section?.ToString().ToLowerInvariant() ?? "section";
		}
		else if (node.IsInGroup("hit_body"))
		{
			hit.Part = "body";
		}

		if (!hit.DriverHit && hit.TireIndex < 0 && hit.Section == null)
		{
			var vehiclePawn = ResolveVehiclePawn(node);
			if (vehiclePawn != null)
				InferVehicleImpactPart(vehiclePawn, hit.Position, ref hit);
		}

		return hit;
	}

	public static bool IsHitOnNode(in ArenaRayHit hit, Node3D node)
	{
		if (hit.Collider is not Node hitNode)
			return false;

		Node? cur = hitNode;
		while (cur != null)
		{
			if (cur == node)
				return true;
			cur = cur.GetParent();
		}

		return false;
	}

	private static VehiclePawn? ResolveVehiclePawn(Node node)
	{
		Node? cur = node;
		while (cur != null)
		{
			if (cur is VehiclePawn pawn)
				return pawn;
			cur = cur.GetParent();
		}

		return null;
	}

	private static void InferVehicleImpactPart(VehiclePawn vehiclePawn, Vector3 worldImpact, ref ArenaRayHit hit)
	{
		var local = vehiclePawn.ToLocal(worldImpact);

		if (TryInferDriverHit(local, ref hit))
			return;

		if (TryInferTireHit(local, ref hit))
			return;

		if (local.Y >= 0.72f)
		{
			hit.Section = ArmorSection.Top;
			hit.Part = "top";
			return;
		}

		if (local.Y <= -0.12f)
		{
			hit.Section = ArmorSection.Undercarriage;
			hit.Part = "undercarriage";
			return;
		}

		hit.Section = InferClosestSideSection(local);
		hit.Part = hit.Section?.ToString().ToLowerInvariant() ?? "body";
	}

	private static bool TryInferDriverHit(Vector3 local, ref ArenaRayHit hit)
	{
		const float halfWidth = 0.4f;
		const float halfHeight = 0.55f;
		const float halfLength = 0.6f;
		var centered = local - new Vector3(0f, 0.62f, -0.10f);
		if (MathF.Abs(centered.X) > halfWidth || MathF.Abs(centered.Y) > halfHeight || MathF.Abs(centered.Z) > halfLength)
			return false;

		hit.DriverHit = true;
		hit.Part = "driver";
		return true;
	}

	private static bool TryInferTireHit(Vector3 local, ref ArenaRayHit hit)
	{
		var tireCenters = new[]
		{
			new Vector3(-0.9f, -0.05f, -1.3f),
			new Vector3(0.9f, -0.05f, -1.3f),
			new Vector3(-0.9f, -0.05f, 1.3f),
			new Vector3(0.9f, -0.05f, 1.3f),
		};

		const float hitRadiusSq = 0.45f * 0.45f;
		for (var i = 0; i < tireCenters.Length; i++)
		{
			var dx = local.X - tireCenters[i].X;
			var dy = local.Y - tireCenters[i].Y;
			var dz = local.Z - tireCenters[i].Z;
			var distSq = dx * dx + dy * dy + dz * dz;
			if (distSq > hitRadiusSq)
				continue;

			hit.TireIndex = i;
			hit.Part = "tire";
			return true;
		}

		return false;
	}

	private static ArmorSection InferClosestSideSection(Vector3 local)
	{
		var bestSection = ArmorSection.Front;
		var bestScore = float.MaxValue;
		EvaluateSectionCandidate(ArmorSection.Front, local, new Vector3(0f, 0.45f, -1.10f), new Vector2(0.9f, 0.5f), ref bestSection, ref bestScore);
		EvaluateSectionCandidate(ArmorSection.Rear, local, new Vector3(0f, 0.45f, 1.10f), new Vector2(0.9f, 0.5f), ref bestSection, ref bestScore);
		EvaluateSectionCandidate(ArmorSection.Left, local, new Vector3(-0.60f, 0.45f, 0.0f), new Vector2(0.3f, 0.6f), ref bestSection, ref bestScore);
		EvaluateSectionCandidate(ArmorSection.Right, local, new Vector3(0.60f, 0.45f, 0.0f), new Vector2(0.3f, 0.6f), ref bestSection, ref bestScore);
		return bestSection;
	}

	private static void EvaluateSectionCandidate(
		ArmorSection section,
		Vector3 local,
		Vector3 center,
		Vector2 halfExtents,
		ref ArmorSection bestSection,
		ref float bestScore)
	{
		var dx = (local.X - center.X) / MathF.Max(0.01f, halfExtents.X);
		var dz = (local.Z - center.Z) / MathF.Max(0.01f, halfExtents.Y);
		var score = dx * dx + dz * dz;
		if (score >= bestScore)
			return;

		bestScore = score;
		bestSection = section;
	}

	private static bool IsNodeOrParentInGroup(Node node, string group)
	{
		Node? cur = node;
		while (cur != null)
		{
			if (cur.IsInGroup(group))
				return true;
			cur = cur.GetParent();
		}

		return false;
	}

	private static int TireIndexFromHitbox(Node node)
	{
		return node.Name.ToString() switch
		{
			"TireFL" => 0,
			"TireFR" => 1,
			"TireRL" => 2,
			"TireRR" => 3,
			_ => -1,
		};
	}

	private static ArmorSection? SectionFromHitbox(Node node)
	{
		return node.Name.ToString() switch
		{
			"SecFront" => ArmorSection.Front,
			"SecRear" => ArmorSection.Rear,
			"SecLeft" => ArmorSection.Left,
			"SecRight" => ArmorSection.Right,
			"SecTop" => ArmorSection.Top,
			"SecUnder" => ArmorSection.Undercarriage,
			_ => null,
		};
	}

	private static Godot.Collections.Array<Rid> BuildRaycastExcludes(CollisionObject3D shooter)
	{
		var excludes = new Godot.Collections.Array<Rid> { shooter.GetRid() };
		CollectHitboxRids(shooter, excludes);
		return excludes;
	}

	private static void CollectHitboxRids(Node node, Godot.Collections.Array<Rid> into)
	{
		foreach (var childObj in node.GetChildren())
		{
			if (childObj is not Node child)
				continue;
			if (child.IsInGroup("hitbox") && child is CollisionObject3D collisionObject)
				into.Add(collisionObject.GetRid());
			CollectHitboxRids(child, into);
		}
	}
}

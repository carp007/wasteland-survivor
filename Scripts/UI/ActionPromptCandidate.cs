// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/UI/ActionPromptCandidate.cs
// Purpose: Lightweight data model for in-world action prompt candidates.
// -------------------------------------------------------------------------------------------------
using Godot;

namespace WastelandSurvivor.Game.UI;

public enum ActionPromptAction
{
	None = 0,
	EnterVehicle = 1,
	ReplaceTire = 2,
	EnterRecoveredVehicle = 3,
	AttachTowCable = 4,
	DetachTowCable = 5,
	StripWreck = 6,
}

/// <summary>
/// Represents a single possible action prompt that could be shown to the player.
/// The overlay selects a focused anchor group, then shows all actions for that group.
/// </summary>
public readonly struct ActionPromptCandidate
{
	public readonly Node3D? Target;
	public readonly Vector3? WorldAnchor;
	public readonly string ActionText;
	public readonly string KeyText;
	public readonly ActionPromptAction Action;
	public readonly int ActionData;
	public readonly int Priority;
	public readonly float Distance;

	public ActionPromptCandidate(
		Node3D? target,
		Vector3? worldAnchor,
		string actionText,
		string keyText,
		ActionPromptAction action,
		int actionData,
		int priority,
		float distance)
	{
		Target = target;
		WorldAnchor = worldAnchor;
		ActionText = actionText;
		KeyText = keyText;
		Action = action;
		ActionData = actionData;
		Priority = priority;
		Distance = distance;
	}

	public static ActionPromptCandidate ForTarget(
		Node3D target,
		string actionText,
		string keyText,
		ActionPromptAction action,
		int priority,
		float distance,
		int actionData = 0)
		=> new(target, worldAnchor: null, actionText, keyText, action, actionData, priority, distance);

	public static ActionPromptCandidate ForWorld(
		Vector3 worldAnchor,
		string actionText,
		string keyText,
		ActionPromptAction action,
		int priority,
		float distance,
		int actionData = 0)
		=> new(target: null, worldAnchor, actionText, keyText, action, actionData, priority, distance);
}

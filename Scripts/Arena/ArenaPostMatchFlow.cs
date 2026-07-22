using System;
using Godot;
using GameUiKit.UI;

namespace WastelandSurvivor.Game.Arena;

/// <summary>
/// Owns the lightweight post-match arena-outcome instruction flow.
/// Keeps win/lose outcome state and the non-modal banner text out of ArenaRealtimeView.
/// </summary>
internal sealed class ArenaPostMatchFlow
{
	private readonly Func<IModalService?> _getModals;
	private IModalHandle? _matchEndedModal;
	private Action? _onContinue;
	private Label? _fallbackLabel;

	public ArenaPostMatchFlow(Func<IModalService?> getModals)
	{
		_getModals = getModals;
	}

	public bool AwaitingExit { get; private set; }
	public string Outcome { get; private set; } = string.Empty;
	public bool PlayerKilledThisMatch { get; private set; }
	public bool EnemyKilledThisMatch { get; private set; }
	public bool MatchEndedModalActive { get; private set; }
	public float ExitHoldSeconds { get; private set; }

	public void Begin(string outcome, Label fallbackLabel, float holdDurationSeconds, Action onContinue)
	{
		Outcome = outcome ?? string.Empty;
		AwaitingExit = true;
		ExitHoldSeconds = 0f;
		PlayerKilledThisMatch = string.Equals(Outcome, "lose", StringComparison.OrdinalIgnoreCase);
		EnemyKilledThisMatch = string.Equals(Outcome, "win", StringComparison.OrdinalIgnoreCase);
		_onContinue = onContinue;
		_fallbackLabel = fallbackLabel;

		fallbackLabel.Text = Outcome switch
		{
			"win" => "Enemy vehicle neutralized.\nSalvage if you want, then drive out through the highlighted south arena exit to finish the match.",
			"lose" => "DRIVER KILLED.\nA fresh clone is being decanted at your last upload facility. Memory intact — wallet a little lighter.",
			_ => $"Match ended ({Outcome}). Leaving the arena..."
		};
		fallbackLabel.Visible = true;

		CloseModal();
		MatchEndedModalActive = false;
	}

	public bool UpdateFallbackHold(float dt, bool holding, float holdDurationSeconds)
	{
		return false;
	}

	public void Reset()
	{
		ExitHoldSeconds = 0f;
		AwaitingExit = false;
		Outcome = string.Empty;
		PlayerKilledThisMatch = false;
		EnemyKilledThisMatch = false;
		_onContinue = null;
		if (_fallbackLabel != null && GodotObject.IsInstanceValid(_fallbackLabel))
			_fallbackLabel.Visible = false;
		CloseModal();
		MatchEndedModalActive = false;
	}

	public void CloseModal()
	{
		try
		{
			if (_matchEndedModal != null && _matchEndedModal.IsOpen)
				_matchEndedModal.Close();
		}
		catch
		{
			// Best-effort close.
		}

		_matchEndedModal = null;
		MatchEndedModalActive = false;
	}

	private void CompleteContinue()
	{
		var callback = _onContinue;
		Reset();
		callback?.Invoke();
	}


}

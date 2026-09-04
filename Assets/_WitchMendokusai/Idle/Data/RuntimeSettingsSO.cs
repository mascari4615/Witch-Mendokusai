using UnityEngine;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Idle
{
	[CreateAssetMenu(fileName = "IdleRuntimeSettings", menuName = "WM/Idle/Runtime Settings")]
	public sealed class RuntimeSettingsSO : ScriptableObject
	{
		[SerializeField, Min(0.1f)] private float saveIntervalSeconds = 10f;
		[SerializeField, Min(0.1f)] private float noteSeconds = 5f;
		[SerializeField, Min(0.05f)] private float uiRefreshSeconds = 0.1f;
		[SerializeField, Range(0f, 1f)] private float soundVolume = 0.32f;
		[SerializeField, Min(0f)] private float soundMinGapSeconds = 0.06f;
		[SerializeField, Min(0.1f)] private float tooltipTouchSeconds = 1.8f;
		[SerializeField, Min(1)] private int modalRepaintMilliseconds = 16;
		[SerializeField, Min(1)] private int previewStage = 4;
		[SerializeField, Min(0f)] private double previewResource = 500d;
		[SerializeField] private int[] previewPartyHeroIds = { 0, 3, 1 };

		public float SaveIntervalSeconds => saveIntervalSeconds;
		public float NoteSeconds => noteSeconds;
		public float UIRefreshSeconds => uiRefreshSeconds;
		public float SoundVolume => soundVolume;
		public float SoundMinGapSeconds => soundMinGapSeconds;
		public long TooltipTouchMilliseconds => (long)(tooltipTouchSeconds * 1000f);
		public long ModalRepaintMilliseconds => modalRepaintMilliseconds;

		public IdleState CreatePreviewState(IdleTuning tuning)
		{
			IdleState state = new IdleState();
			IdleHeroes.EnsureStarter(state);
			for (int seat = 0; seat < previewPartyHeroIds.Length && seat < state.Party.Length; seat++)
			{
				int heroId = previewPartyHeroIds[seat];
				if (state.IndexOfHero(heroId) < 0)
				{
					state.Heroes.Add(new IdleHeroOwned(heroId));
				}
				state.Party[seat] = heroId;
			}

			state.Stage = previewStage;
			state.BestStage = previewStage;
			state.ClearedStage = previewStage - 1;
			state.Resource = previewResource;
			state.EnsureSeatRoom(tuning);
			return state;
		}

		public bool TryValidate(out string error)
		{
			if (saveIntervalSeconds <= 0f || noteSeconds <= 0f || uiRefreshSeconds < 0.05f
				|| soundVolume < 0f || soundVolume > 1f || soundMinGapSeconds < 0f
				|| tooltipTouchSeconds <= 0f || modalRepaintMilliseconds <= 0 || previewStage <= 0
				|| previewResource < 0d || previewPartyHeroIds == null || previewPartyHeroIds.Length == 0)
			{
				error = "runtime timings and sound values must be in range";
				return false;
			}

			error = string.Empty;
			return true;
		}
	}
}

using System;
using System.Collections;
using FMODUnity;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	/// <summary>
	/// 기본 낚시 미니게임: 랜덤 대기 → 입질 신호(사운드 + 말풍선 "!") → 타이밍 입력 윈도우.
	/// IFishingMiniGame을 구현하므로 교체 시 이 컴포넌트만 바꾸면 된다.
	/// </summary>
	public class TimingFishingMiniGame : MonoBehaviour, IFishingMiniGame
	{
		[Header("_" + nameof(TimingFishingMiniGame))]
		[SerializeField] private Sprite biteSprite;
		[SerializeField] private string biteEventPath = "event:/SFX/Fishing/Bite";

		public IEnumerator Play(FishingContext context, Action<bool> onResult)
		{
			// 입질까지 랜덤 대기
			float biteDelay = Random.Range(context.Data.BiteDelayMin, context.Data.BiteDelayMax);
			yield return new WaitForSeconds(biteDelay);

			// 입질 신호
			if (string.IsNullOrEmpty(biteEventPath) == false)
				RuntimeManager.PlayOneShot(biteEventPath, context.Fisherman.position);

			UIManager.Instance.SpeechBubble.Show(context.Fisherman, biteSprite, context.Data.InputWindow);

			// 타이밍 입력 윈도우
			bool caught = false;
			Action onCatch = () => caught = true;
			InputManager.Instance.RegisterInputEvent(InputEventType.Submit, InputEventResponseType.Performed, onCatch);

			yield return new WaitForSeconds(context.Data.InputWindow);

			InputManager.Instance.UnregisterInputEvent(InputEventType.Submit, InputEventResponseType.Performed, onCatch);

			onResult(caught);
		}
	}
}

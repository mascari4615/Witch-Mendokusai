using System.Collections;
using UnityEngine;
using VContainer;
using WitchMendokusai.Presentation;

namespace WitchMendokusai
{
	/// <summary>
	/// 스테이지 진입 시 큰 배너 팝업 (월드/던전 진입). 큐 없음 — 새 호출이 오면 즉시 교체.
	/// PopupCard 의 stage variant USS 로 더 큰 시각적 임팩트.
	/// </summary>
	public class StagePopupView : MonoBehaviour
	{
		private const float HOLD_SECONDS = 2.0f;
		private const float TRANSITION_SECONDS = 0.35f;

		private PopupCard card;
		private Coroutine current;

		private UIRoot uiRoot;

		[Inject]
		public void Construct(UIRoot uiRoot)
		{
			this.uiRoot = uiRoot;
		}

		private void Start()
		{
			card = new PopupCard();
			card.AddToClassList(PopupCard.USS_VARIANT_STAGE);
			uiRoot.OverlayLayer.Add(card);
		}

		private void OnDestroy()
		{
			card?.RemoveFromHierarchy();
		}

		public void Popup(Stage stage)
		{
			if (stage == null)
				return;

			if (current != null)
				StopCoroutine(current);
			current = StartCoroutine(Play(stage));
		}

		private IEnumerator Play(Stage stage)
		{
			// 던전 안이면 설명 자리에 그 던전이 쓰는 스테이지 이름
			(string place, string placeSub) = StagePlaceText.Of(stage);
			card.SetData(stage.Sprite, place, string.IsNullOrEmpty(placeSub) ? stage.Description : placeSub);
			card.SetActive(true);
			yield return new WaitForSecondsRealtime(HOLD_SECONDS);
			card.SetActive(false);
			yield return new WaitForSecondsRealtime(TRANSITION_SECONDS);
			current = null;
		}
	}
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using WitchMendokusai.Presentation;

namespace WitchMendokusai
{
	/// <summary>
	/// 일반 DataSO 팝업 (퀘스트 완료, 업적 등). 큐 기반 — 동시에 여러 개 호출되면 순서대로 표시.
	/// 시각효과는 .wm-popup-card USS transition 으로 구현.
	/// </summary>
	public class PopupView : MonoBehaviour
	{
		private const float HOLD_SECONDS = 1.5f;
		private const float TRANSITION_SECONDS = 0.25f;

		private readonly Queue<DataSO> queue = new();
		private PopupCard card;
		private bool playing;

		private UIRoot uiRoot;

		[Inject]
		public void Construct(UIRoot uiRoot)
		{
			this.uiRoot = uiRoot;
		}

		private void Start()
		{
			card = new PopupCard();
			uiRoot.OverlayLayer.Add(card);
		}

		private void OnDestroy()
		{
			card?.RemoveFromHierarchy();
		}

		public void Popup(DataSO data)
		{
			if (data == null)
				return;

			queue.Enqueue(data);
			if (playing == false)
				StartCoroutine(PlayQueue());
		}

		private IEnumerator PlayQueue()
		{
			playing = true;
			WaitForSecondsRealtime hold = new(HOLD_SECONDS);
			WaitForSecondsRealtime transition = new(TRANSITION_SECONDS);

			while (queue.Count > 0)
			{
				DataSO data = queue.Dequeue();
				card.SetData(data.Sprite, data.Name, data.Description);
				card.SetActive(true);
				yield return hold;
				card.SetActive(false);
				yield return transition;
			}

			playing = false;
		}
	}
}

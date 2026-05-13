using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	/// <summary>
	/// 유닛 머리 위 말풍선 — 풀 기반 (재사용). World 좌표를 화면으로 변환해 매 프레임 추적.
	/// USS .wm-speech-bubble + --active 로 페이드 in/out.
	/// </summary>
	public class SpeechBubbleView : MonoBehaviour
	{
		private const float DEFAULT_DURATION = 3f;
		private const float FADE_OUT_SECONDS = 0.3f;
		private const int INITIAL_POOL_SIZE = 4;

		private readonly Stack<SpeechBubbleItem> pool = new();

		private UIRoot uiRoot;

		[Inject]
		public void Construct(UIRoot uiRoot)
		{
			this.uiRoot = uiRoot;
		}

		private void Start()
		{
			for (int i = 0; i < INITIAL_POOL_SIZE; i++)
				pool.Push(CreateItem());
		}

		private SpeechBubbleItem CreateItem()
		{
			SpeechBubbleItem item = new();
			item.style.display = UnityEngine.UIElements.DisplayStyle.None;
			uiRoot.OverlayLayer.Add(item);
			return item;
		}

		private SpeechBubbleItem Pop()
		{
			if (pool.Count == 0)
				return CreateItem();
			return pool.Pop();
		}

		public void Show(Transform target, string text, float duration = DEFAULT_DURATION)
		{
			SpeechBubbleItem item = Pop();
			item.style.display = UnityEngine.UIElements.DisplayStyle.Flex;
			item.ShowText(text);
			StartCoroutine(LifecycleCoroutine(item, target, duration));
		}

		public void Show(Transform target, Sprite emoji, float duration = DEFAULT_DURATION)
		{
			SpeechBubbleItem item = Pop();
			item.style.display = UnityEngine.UIElements.DisplayStyle.Flex;
			item.ShowEmoji(emoji);
			StartCoroutine(LifecycleCoroutine(item, target, duration));
		}

		private IEnumerator LifecycleCoroutine(SpeechBubbleItem item, Transform target, float duration)
		{
			float elapsed = 0f;
			bool fadingOut = false;

			while (elapsed < duration + FADE_OUT_SECONDS)
			{
				yield return null;

				if (target != null)
					item.FollowTarget(target);

				elapsed += Time.deltaTime;

				if (fadingOut == false && elapsed >= duration)
				{
					fadingOut = true;
					item.Deactivate();
				}
			}

			item.style.display = UnityEngine.UIElements.DisplayStyle.None;
			pool.Push(item);
		}
	}
}

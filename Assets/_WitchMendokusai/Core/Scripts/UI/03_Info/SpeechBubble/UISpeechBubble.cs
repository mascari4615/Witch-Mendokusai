using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class UISpeechBubble : MonoBehaviour
	{
		private const int CreateThreshold = 4;
		private const float DefaultDuration = 3f;

		[SerializeField] private Transform bubblesRoot;
		[SerializeField] private GameObject bubblePrefab;

		private readonly Stack<UISpeechBubbleItem> pool = new();

		private void Awake()
		{
			for (int i = 0; i < CreateThreshold; i++)
				CreateBubble();
		}

		private void CreateBubble()
		{
			UISpeechBubbleItem bubble = Instantiate(bubblePrefab, bubblesRoot).GetComponent<UISpeechBubbleItem>();
			bubble.gameObject.SetActive(false);
			pool.Push(bubble);
		}

		private UISpeechBubbleItem Pop()
		{
			if (pool.Count == 0)
				CreateBubble();
			return pool.Pop();
		}

		private void Return(UISpeechBubbleItem bubble)
		{
			pool.Push(bubble);
		}

		public void Show(Transform target, string text, float duration = DefaultDuration)
		{
			UISpeechBubbleItem bubble = Pop();
			bubble.Show(target, text, duration, () => Return(bubble));
		}

		public void Show(Transform target, UnityEngine.Sprite emoji, float duration = DefaultDuration)
		{
			UISpeechBubbleItem bubble = Pop();
			bubble.Show(target, emoji, duration, () => Return(bubble));
		}
	}
}

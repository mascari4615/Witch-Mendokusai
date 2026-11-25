using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 화면 아래 작은 띠 (업적 달성, 도감 새 기록) 의 한 줄 대기열. 종류가 달라도 하나씩 차례로 떠서 겹치지 않음.
	/// 시간이 멈춘 동안 (카드 선택, 전체 메뉴) 은 보조 HUD 처럼 숨고 시간도 안 감. 그 화면의 버튼과 겹치지 않게
	/// 머무는 시간은 UIFxSettings, 사라지는 움직임 길이는 띠의 USS transition 한 자리
	/// </summary>
	public sealed class UIToastQueue
	{
		private readonly struct Entry
		{
			public readonly VisualElement Toast;
			public readonly Action Show;
			public readonly Action Hide;

			public Entry(VisualElement toast, Action show, Action hide)
			{
				Toast = toast;
				Show = show;
				Hide = hide;
			}
		}

		private readonly MonoBehaviour host;
		private readonly Func<bool> isPaused;
		private readonly Queue<Entry> waiting = new();
		private bool isPlaying;

		/// <param name="host">코루틴을 돌릴 자리 (UIRoot)</param>
		public UIToastQueue(MonoBehaviour host, Func<bool> isPaused)
		{
			this.host = host;
			this.isPaused = isPaused;
		}

		public void Enqueue(VisualElement toast, Action show, Action hide)
		{
			waiting.Enqueue(new Entry(toast, show, hide));
			if (isPlaying == false)
			{
				host.StartCoroutine(Play());
			}
		}

		private IEnumerator Play()
		{
			isPlaying = true;
			while (waiting.Count > 0)
			{
				Entry entry = waiting.Dequeue();
				while (isPaused())
				{
					yield return null;
				}
				// 줄을 서는 동안 씬이 내려가 띠가 치워졌을 수 있음
				if (entry.Toast.panel == null)
				{
					continue;
				}
				entry.Show();
				for (float held = 0f; held < UIFxSettings.Instance.achievementToastHoldSeconds;)
				{
					yield return null;
					bool paused = isPaused();
					entry.Toast.style.visibility = paused ? Visibility.Hidden : Visibility.Visible;
					held += paused ? 0f : Time.unscaledDeltaTime;
				}
				bool faded = false;
				EventCallback<TransitionEndEvent> onEnd = _ => faded = true;
				EventCallback<TransitionCancelEvent> onCancel = _ => faded = true;
				entry.Toast.RegisterCallback(onEnd);
				entry.Toast.RegisterCallback(onCancel);
				entry.Hide();
				// 끝 신호가 안 오면 (USS 없음 등) 떠 있던 시간만큼에서 끊어 대기열이 멈추지 않게
				float fadeStarted = Time.realtimeSinceStartup;
				yield return new WaitUntil(() => faded || Time.realtimeSinceStartup - fadeStarted > UIFxSettings.Instance.achievementToastHoldSeconds);
				entry.Toast.UnregisterCallback(onEnd);
				entry.Toast.UnregisterCallback(onCancel);
			}
			isPlaying = false;
		}
	}
}

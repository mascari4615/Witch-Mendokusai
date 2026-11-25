using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// 판 안에서 새 기록이 적힌 순간의 띠. 하나씩 차례로. 첫 만남과 완성만 띄움 (중간 칸은 도감의 NEW 로)
	public partial class DiscoveryScreen
	{
		private const string USS_PALETTE = "wm-fullmenu-palette";
		private const int FIRST_STAGE = 1;

		private readonly Queue<MonsterLogStageEvent> toastQueue = new();
		private DiscoveryToast toast;
		private bool toastPlaying;
		private bool toastFaded;

		// 띠는 도감 화면 밖 (OverlayLayer) 이라 USS 를 따로 붙임
		private void BuildToast()
		{
			toast = new DiscoveryToast();
			toast.AddToClassList(USS_PALETTE);
			AddStyles(toast);
			toast.RegisterCallback<TransitionEndEvent>(_ => toastFaded = true);
			toast.RegisterCallback<TransitionCancelEvent>(_ => toastFaded = true);
			uiRoot.OverlayLayer.Add(toast);
		}

		private void DestroyToast() => toast.RemoveFromHierarchy();

		private void OnStageRaised(MonsterLogStageEvent evt)
		{
			if (evt.Stage != FIRST_STAGE && evt.Stage < evt.MaxStage)
			{
				return;
			}
			toastQueue.Enqueue(evt);
			if (toastPlaying == false)
			{
				StartCoroutine(PlayToasts());
			}
		}

		// 머무는 시간은 업적 띠와 같은 값
		private IEnumerator PlayToasts()
		{
			toastPlaying = true;
			while (toastQueue.Count > 0)
			{
				toast.Show(toastQueue.Dequeue());
				yield return new WaitForSecondsRealtime(UIFxSettings.Instance.achievementToastHoldSeconds);
				toastFaded = false;
				toast.Hide();
				// 끝 신호가 안 오면 (USS 없음 등) 떠 있던 시간만큼에서 끊어 대기열이 멈추지 않게
				float fadeStarted = Time.realtimeSinceStartup;
				yield return new WaitUntil(() => toastFaded || Time.realtimeSinceStartup - fadeStarted > UIFxSettings.Instance.achievementToastHoldSeconds);
			}
			toastPlaying = false;
		}
	}
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// 업적 달성 띠. 화면 아래 가운데, 하나씩 차례로. 조작을 막지 않음
	public partial class AchievementView
	{
		private readonly Queue<AchievementUnlockedEvent> toastQueue = new();
		private AchievementToast toast;
		private bool toastPlaying;
		private bool toastFaded;

		// 띠는 업적 화면 밖 (OverlayLayer) 이라 USS 를 따로 붙임
		private void BuildToast()
		{
			toast = new AchievementToast();
			toast.AddToClassList(USS_PALETTE);
			toast.styleSheets.Add(Settings.MenuStyle);
			toast.styleSheets.Add(Settings.Style);
			toast.RegisterCallback<TransitionEndEvent>(_ => toastFaded = true);
			toast.RegisterCallback<TransitionCancelEvent>(_ => toastFaded = true);
			uiRoot.OverlayLayer.Add(toast);
		}

		private void DestroyToast() => toast.RemoveFromHierarchy();

		private void OnUnlocked(AchievementUnlockedEvent evt)
		{
			toastQueue.Enqueue(evt);
			if (toastPlaying == false)
			{
				StartCoroutine(PlayToasts());
			}
		}

		private IEnumerator PlayToasts()
		{
			toastPlaying = true;
			while (toastQueue.Count > 0)
			{
				AchievementUnlockedEvent evt = toastQueue.Dequeue();
				toast.Show(SOHelper.Get<AchievementSO>(evt.AchievementId), evt.Tier);
				yield return new WaitForSecondsRealtime(UIFxSettings.Instance.achievementToastHoldSeconds);
				toastFaded = false;
				toast.Hide();
				// 사라지는 움직임 길이는 USS 한 자리 (wm-ach-toast transition). 끝 신호를 기다리되, 신호가 안 오면 (USS 없음 등) 떠 있던 시간만큼에서 끊어 대기열이 멈추지 않게
				float fadeStarted = Time.realtimeSinceStartup;
				yield return new WaitUntil(() => toastFaded || Time.realtimeSinceStartup - fadeStarted > UIFxSettings.Instance.achievementToastHoldSeconds);
			}
			toastPlaying = false;
		}
	}
}

using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	public class UITransition : MonoBehaviour
	{
		private CanvasGroup canvasGroup;
		private Animator[] transitionAnimators;

		private const float FadeWaitTime = 0.5f;
		private const float AnimWaitTime = 0.01f;
	
		// EarlyResumeRatio = 0.3f = FadeIn 애니메이션의 30% 지점에서 Resume
		// FadeIn Animation이 전부 끝나기 전에, 시간 정지를 풀고 입력을 받기 위해서 EarlyResumeRatio를 사용합니다.
		// FadeIn Animation이 다 끝날 때 기다리면 조금 답답한 느낌이 들어서. - 2025.03.19 20:28
		private const float EarlyResumeRatio = 0.3f;

		private void Awake()
		{
			canvasGroup = GetComponent<CanvasGroup>();
			transitionAnimators = GetComponentsInChildren<Animator>(true);
		}

		private void Start()
		{
			canvasGroup.alpha = 1;
		}

		public async UniTask Transition(Action aDuringTransition, Action aWhenStart = null, Action aWhenEnd = null)
		{
			UniTask ExecuteAction()
			{
				aDuringTransition?.Invoke();
				return UniTask.CompletedTask;
			}
			await TransitionCore(ExecuteAction, aWhenStart, aWhenEnd);
		}

		public async UniTask Transition(UniTask tDuringTransition, Action aWhenStart = null, Action aWhenEnd = null)
		{
			UniTask ExecuteTask() => tDuringTransition;
			await TransitionCore(ExecuteTask, aWhenStart, aWhenEnd);
		}

		private async UniTask TransitionCore(Func<UniTask> tDuringTransition, Action aWhenStart = null, Action aWhenEnd = null)
		{
			// HACK:
			Animator transitionAnimator = transitionAnimators[Random.Range(0, transitionAnimators.Length)];
			AnimatorStateInfo currentStateInfo;
			// Start
			aWhenStart?.Invoke();
			TimeManager.Instance.Pause();
			canvasGroup.blocksRaycasts = true;

			// During
			{
				// Fade Out
				transitionAnimator.SetTrigger("OUT");
				await UniTask.Delay(ToMilliseconds(AnimWaitTime), DelayType.Realtime);
				currentStateInfo = transitionAnimator.GetCurrentAnimatorStateInfo(0); // UpdateMode: UnscaledTime
				float fadeOutDuration = currentStateInfo.length / currentStateInfo.speedMultiplier;
				await UniTask.Delay(ToMilliseconds(fadeOutDuration), DelayType.Realtime);

				// Execute Action
				await tDuringTransition.Invoke();
				await UniTask.Delay(ToMilliseconds(FadeWaitTime), DelayType.Realtime);

				// Fade In
				transitionAnimator.SetTrigger("IN");
				await UniTask.Delay(ToMilliseconds(AnimWaitTime), DelayType.Realtime);
				currentStateInfo = transitionAnimator.GetCurrentAnimatorStateInfo(0); // UpdateMode: UnscaledTime
				float fadeInDuration = currentStateInfo.length / currentStateInfo.speedMultiplier;
				await UniTask.Delay(ToMilliseconds(fadeInDuration * EarlyResumeRatio), DelayType.Realtime);
			}

			// End
			canvasGroup.blocksRaycasts = false;
			TimeManager.Instance.Resume();
			aWhenEnd?.Invoke();
		}

		private int ToMilliseconds(float seconds)
		{
			return (int)(seconds * 1000);
		}
	}
}
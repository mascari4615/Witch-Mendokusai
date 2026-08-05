using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	public class UITransition : MonoBehaviour
	{
		private CanvasGroup canvasGroup;
		private Animator[] transitionAnimators;

		private TimeManager timeManager;

		[Inject]
		public void Construct(TimeManager timeManager)
		{
			this.timeManager = timeManager;
		}

		[Header("Transition Timing")]
		// 화면이 완전히 덮인 뒤 그대로 머무는 시간.
		[SerializeField] private float fadeWaitTime = 0.5f;
		// 애니메이터가 상태를 받아 넘어가는 걸 기다리는 한 프레임 남짓.
		[SerializeField] private float animWaitTime = 0.01f;

		// earlyResumeRatio = 0.3f = FadeIn 애니메이션의 30% 지점에서 Resume
		// FadeIn Animation이 전부 끝나기 전에, 시간 정지를 풀고 입력을 받기 위해서 earlyResumeRatio를 사용합니다.
		// FadeIn Animation이 다 끝날 때 기다리면 조금 답답한 느낌이 들어서. - 2025.03.19 20:28
		// ↑ 「답답한 느낌」은 숫자로 정해지지 않는다. 손으로 밀어보며 맞춰야 하는 값이라 인스펙터로 꺼냈다.
		[SerializeField, Range(0f, 1f)] private float earlyResumeRatio = 0.3f;

		public static bool IsInTransition { get; private set; } = false;

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
			IsInTransition = true;
			aWhenStart?.Invoke();
			timeManager.Pause(gameObject);
			canvasGroup.blocksRaycasts = true;

			// During
			{
				// Fade Out
				transitionAnimator.SetTrigger("OUT");
				await UniTask.Delay(ToMilliseconds(animWaitTime), DelayType.Realtime);
				currentStateInfo = transitionAnimator.GetCurrentAnimatorStateInfo(0); // UpdateMode: UnscaledTime
				float fadeOutDuration = currentStateInfo.length / currentStateInfo.speedMultiplier;
				await UniTask.Delay(ToMilliseconds(fadeOutDuration), DelayType.Realtime);

				// Execute Action
				await tDuringTransition.Invoke();
				await UniTask.Delay(ToMilliseconds(fadeWaitTime), DelayType.Realtime);

				// Fade In
				transitionAnimator.SetTrigger("IN");
				await UniTask.Delay(ToMilliseconds(animWaitTime), DelayType.Realtime);
				currentStateInfo = transitionAnimator.GetCurrentAnimatorStateInfo(0); // UpdateMode: UnscaledTime
				float fadeInDuration = currentStateInfo.length / currentStateInfo.speedMultiplier;
				await UniTask.Delay(ToMilliseconds(fadeInDuration * earlyResumeRatio), DelayType.Realtime);
			}

			// End
			IsInTransition = false;
			canvasGroup.blocksRaycasts = false;
			timeManager.Resume(gameObject);
			aWhenEnd?.Invoke();
		}

		private int ToMilliseconds(float seconds)
		{
			return (int)(seconds * 1000);
		}
	}
}
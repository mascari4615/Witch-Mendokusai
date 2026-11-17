using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	/// <summary>
	/// 씬 안 전환 (포털, 던전 입장과 귀환). ScreenWipe 로 덮고, 작업 뒤 걷음
	/// 전환은 UIWipe 에셋 (기본 UIFxSettings.portalWipe, 호출한 자리가 넘기면 그것). 머무는 시간과 입력 조기 재개는 여기
	/// </summary>
	public class TransitionView : MonoBehaviour
	{
		// 덮인 채 머무는 시간
		[SerializeField] private float holdSeconds = 0.25f;
		// 걷기의 이 비율 지점에서 시간 정지와 입력을 미리 풂 (체감 답답함 완화)
		[SerializeField, Range(0f, 1f)] private float earlyResumeRatio = 0.3f;

		private ScreenWipe wipe;

		private UIRoot uiRoot;
		private TimeManager timeManager;

		[Inject]
		public void Construct(UIRoot uiRoot, TimeManager timeManager)
		{
			this.uiRoot = uiRoot;
			this.timeManager = timeManager;
		}

		public static bool IsInTransition { get; private set; } = false;

		private void Start()
		{
			wipe = new ScreenWipe(() => UIFxSettings.Instance.portalWipe);
			wipe.SetInstant(false);
			uiRoot.OverlayLayer.Add(wipe);
		}

		private void OnDestroy()
		{
			wipe?.Dispose();
			// 전환 도중 씬이 바뀌면 입력 잠금이 남지 않게
			IsInTransition = false;
		}

		public async UniTask Transition(Action aDuringTransition, Action aWhenStart = null, Action aWhenEnd = null, UIWipe overrideWipe = null)
		{
			UniTask ExecuteAction()
			{
				aDuringTransition?.Invoke();
				return UniTask.CompletedTask;
			}
			await TransitionCore(ExecuteAction, aWhenStart, aWhenEnd, overrideWipe);
		}

		public async UniTask Transition(UniTask tDuringTransition, Action aWhenStart = null, Action aWhenEnd = null, UIWipe overrideWipe = null)
		{
			UniTask ExecuteTask() => tDuringTransition;
			await TransitionCore(ExecuteTask, aWhenStart, aWhenEnd, overrideWipe);
		}

		private async UniTask TransitionCore(Func<UniTask> tDuringTransition, Action aWhenStart, Action aWhenEnd, UIWipe overrideWipe)
		{
			IsInTransition = true;
			bool resumed = false;
			try
			{
				aWhenStart?.Invoke();
				timeManager.Pause(gameObject);

				UniTaskCompletionSource covered = new();
				wipe.Play(true, () => covered.TrySetResult(), overrideWipe);
				await covered.Task;

				await tDuringTransition.Invoke();
				await UniTask.Delay(ToMilliseconds(holdSeconds), DelayType.Realtime);

				UniTaskCompletionSource revealed = new();
				wipe.Play(false, () => revealed.TrySetResult());
				float revealSeconds = wipe.CurrentWipe.revealSeconds;
				await UniTask.Delay(ToMilliseconds(revealSeconds * earlyResumeRatio), DelayType.Realtime);

				Resume();
				aWhenEnd?.Invoke();

				await revealed.Task;
			}
			finally
			{
				// 작업이 예외로 끝나도 입력 잠금과 시간 정지가 남지 않게
				Resume();
			}

			void Resume()
			{
				if (resumed)
					return;
				resumed = true;
				IsInTransition = false;
				if (this != null)
					timeManager.Resume(gameObject);
			}
		}

		private static int ToMilliseconds(float seconds) => (int)(seconds * 1000);
	}
}

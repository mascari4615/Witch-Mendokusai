using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace WitchMendokusai
{
	/// <summary>
	/// 씬 전환 페이드 — UI Toolkit 검은 오버레이 + USS opacity transition.
	/// 원본 UITransition 의 UniTask API 시그니처 유지.
	/// </summary>
	public class TransitionView : MonoBehaviour
	{
		private const float FADE_OUT_SECONDS = 0.4f;
		private const float HOLD_SECONDS = 0.3f;
		private const float FADE_IN_SECONDS = 0.4f;
		private const float EARLY_RESUME_RATIO = 0.3f;

		public const string USS_CLASS = "wm-transition";
		public const string USS_ACTIVE = "wm-transition--active";

		private VisualElement overlay;

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
			overlay = new VisualElement();
			overlay.AddToClassList(USS_CLASS);
			overlay.pickingMode = PickingMode.Position;
			overlay.style.display = DisplayStyle.None;
			uiRoot.OverlayLayer.Add(overlay);
		}

		private void OnDestroy()
		{
			overlay?.RemoveFromHierarchy();
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

		private async UniTask TransitionCore(Func<UniTask> tDuringTransition, Action aWhenStart, Action aWhenEnd)
		{
			IsInTransition = true;
			aWhenStart?.Invoke();
			timeManager.Pause(gameObject);
			overlay.style.display = DisplayStyle.Flex;
			overlay.pickingMode = PickingMode.Position;

			// Fade Out (검은 오버레이가 화면 덮음)
			overlay.AddToClassList(USS_ACTIVE);
			await UniTask.Delay(ToMilliseconds(FADE_OUT_SECONDS), DelayType.Realtime);

			// During Action (검은 화면 상태에서 작업)
			await tDuringTransition.Invoke();
			await UniTask.Delay(ToMilliseconds(HOLD_SECONDS), DelayType.Realtime);

			// Fade In (검은 오버레이 사라짐)
			overlay.RemoveFromClassList(USS_ACTIVE);
			await UniTask.Delay(ToMilliseconds(FADE_IN_SECONDS * EARLY_RESUME_RATIO), DelayType.Realtime);

			// 시각 fade in 은 진행 중 — 입력은 미리 풀어줌 (체감 답답함 완화)
			IsInTransition = false;
			overlay.pickingMode = PickingMode.Ignore;
			timeManager.Resume(gameObject);
			aWhenEnd?.Invoke();

			// 남은 fade in 시간 — 시각 완료까지 대기 후 element 숨김
			await UniTask.Delay(ToMilliseconds(FADE_IN_SECONDS * (1f - EARLY_RESUME_RATIO)), DelayType.Realtime);
			overlay.style.display = DisplayStyle.None;
		}

		private static int ToMilliseconds(float seconds) => (int)(seconds * 1000);
	}
}

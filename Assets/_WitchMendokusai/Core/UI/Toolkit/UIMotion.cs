using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// UI Toolkit 열기와 닫기 애니메이션 순서. 움직임은 USS transition 담당
	/// - 열기: display 먼저, 활성 클래스는 다음 프레임. 같은 프레임에 무거운 열기 처리가 있으면 끝 값으로 튐
	///   (2026-11-17 MOIS2 상점 첫 열기 0.29초 멈칫, 65ms 만에 불투명. 두 번째 열기는 200ms 정상)
	/// - 닫기: 클래스 제거, 입력 차단, opacity 0 또는 USS 의 transition 길이 경과 시 display 끔
	///   display 를 바로 끄면 닫힘 애니메이션이 안 보임
	/// </summary>
	public static class UIMotion
	{
		/// <summary>창 열기. 살짝 커지며 나타남</summary>
		public const string POP = "wm-motion-pop";
		/// <summary>전체 화면 패널. 아래에서 살짝 올라오며 나타남</summary>
		public const string RISE = "wm-motion-rise";
		/// <summary>위 두 기본 클래스의 열린 상태</summary>
		public const string IN = "wm-motion--in";

		// 판정용 경계값, 조절 대상 아님
		private const float HIDDEN_OPACITY = 0.001f;
		private const float MILLISECONDS_PER_SECOND = 1000f;

		// 진행 중 예약 작업만 든다. 끝나면 비워 요소 참조를 남기지 않음
		private sealed class State
		{
			public int Generation;
			public IVisualElementScheduledItem Pending;
		}

		private static readonly ConditionalWeakTable<VisualElement, State> states = new();

		private static State GetState(VisualElement element) => states.GetValue(element, _ => new State());

		private static int Cancel(State state)
		{
			state.Pending?.Pause();
			state.Pending = null;
			return ++state.Generation;
		}

		/// <summary>display 를 켜고 다음 프레임에 inClass. 닫히는 중이면 그 자리에서 바로 되돌림</summary>
		public static void Show(VisualElement element, string inClass = IN)
		{
			State state = GetState(element);
			int generation = Cancel(state);
			element.SetEnabled(true);

			bool closing = element.resolvedStyle.display == DisplayStyle.Flex && element.resolvedStyle.opacity > HIDDEN_OPACITY;
			element.style.display = DisplayStyle.Flex;
			if (closing || element.panel == null)
			{
				element.AddToClassList(inClass);
				return;
			}

			int shownFrame = Time.frameCount;
			bool done = false;
			state.Pending = element.schedule.Execute(() =>
			{
				if (state.Generation != generation)
				{
					done = true;
					return;
				}
				if (Time.frameCount <= shownFrame)
					return;
				element.AddToClassList(inClass);
				state.Pending = null;
				done = true;
			}).Every(0).Until(() => done);
		}

		/// <summary>inClass 를 떼고 닫힘 transition 이 끝나면 display 를 끈다. 그 사이 Show 가 오면 취소</summary>
		public static void Hide(VisualElement element, string inClass = IN)
		{
			State state = GetState(element);
			int generation = Cancel(state);
			element.RemoveFromClassList(inClass);

			if (element.panel == null || element.resolvedStyle.display == DisplayStyle.None)
			{
				element.style.display = DisplayStyle.None;
				return;
			}

			// 닫히는 동안 클릭과 포커스 차단. 다시 열 때 Show 가 풀어 줌
			element.SetEnabled(false);

			// 마감은 다음 프레임 스타일의 transition 길이 (USS 정본). 조상이 숨어 transition 이 안 돌아도 그 시점에 끔
			int hideFrame = Time.frameCount;
			float deadline = float.MaxValue;
			bool done = false;
			state.Pending = element.schedule.Execute(() =>
			{
				if (state.Generation != generation)
				{
					done = true;
					return;
				}
				if (Time.frameCount <= hideFrame)
					return;
				if (deadline == float.MaxValue)
					deadline = Time.realtimeSinceStartup + LongestTransitionSeconds(element);
				bool faded = element.resolvedStyle.opacity <= HIDDEN_OPACITY;
				if (faded == false && Time.realtimeSinceStartup < deadline)
					return;
				element.style.display = DisplayStyle.None;
				element.SetEnabled(true);
				state.Pending = null;
				done = true;
			}).Every(0).Until(() => done);
		}

		/// <summary>애니메이션 없이 즉시 상태를 맞춘다. 초기화용</summary>
		public static void SetInstant(VisualElement element, bool visible, string inClass = IN)
		{
			Cancel(GetState(element));
			element.SetEnabled(true);
			element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
			element.EnableInClassList(inClass, visible);
		}

		private static float LongestTransitionSeconds(VisualElement element)
		{
			List<TimeValue> delays = new(element.resolvedStyle.transitionDelay);
			float longest = 0f;
			int index = 0;
			foreach (TimeValue duration in element.resolvedStyle.transitionDuration)
			{
				float delay = index < delays.Count ? ToSeconds(delays[index]) : 0f;
				longest = Mathf.Max(longest, ToSeconds(duration) + delay);
				index++;
			}
			return longest;
		}

		private static float ToSeconds(TimeValue time) => time.unit == TimeUnit.Millisecond ? time.value / MILLISECONDS_PER_SECOND : time.value;
	}
}

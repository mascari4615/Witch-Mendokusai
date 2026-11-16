using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// UI Toolkit 열기와 닫기 애니메이션 순서. 움직임은 USS transition 담당
	/// - 같은 프레임에 display 켜기와 활성 클래스: 시작 값 없음, transition 생략
	/// - 열기: display 먼저, 스타일 계산 한 번 뒤 다음 프레임에 클래스
	/// - 닫기: 클래스 제거, 닫힘 애니메이션 끝난 뒤 display 끔
	/// </summary>
	public static class UIMotion
	{
		/// <summary>창 열기. 살짝 커지며 나타남</summary>
		public const string POP = "wm-motion-pop";
		/// <summary>전체 화면 패널. 아래에서 살짝 올라오며 나타남</summary>
		public const string RISE = "wm-motion-rise";
		/// <summary>위 두 기본 클래스의 열린 상태</summary>
		public const string IN = "wm-motion--in";

		/// <summary>닫힘 애니메이션이 끝나지 않을 때 강제로 끄는 한도. 정상 경로는 opacity 0 도달</summary>
		private const float HIDE_FAILSAFE_SECONDS = 1f;
		private const float HIDDEN_OPACITY = 0.001f;

		private sealed class State
		{
			public int Generation;
			public IVisualElementScheduledItem Pending;
		}

		private static readonly ConditionalWeakTable<VisualElement, State> states = new();

		private static State GetState(VisualElement element) => states.GetValue(element, _ => new State());

		/// <summary>display 를 켜고 다음 프레임에 inClass 를 붙인다. 닫히는 중이면 그 자리에서 다시 연다</summary>
		public static void Show(VisualElement element, string inClass = IN)
		{
			if (element == null)
				return;

			State state = GetState(element);
			int generation = ++state.Generation;
			state.Pending?.Pause();

			element.style.display = DisplayStyle.Flex;

			// 닫히는 도중: 시작 값 있음, 바로 붙여 그 자리에서 되돌림
			if (element.resolvedStyle.display == DisplayStyle.Flex && element.panel != null && element.ClassListContains(inClass) == false && element.resolvedStyle.opacity > HIDDEN_OPACITY)
			{
				element.AddToClassList(inClass);
				state.Pending = null;
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
				done = true;
			}).Every(0).Until(() => done);
		}

		/// <summary>inClass 를 떼고, 닫힘 transition 으로 opacity 가 0 이 되면 display 를 끈다. 그 사이 Show 가 오면 취소</summary>
		public static void Hide(VisualElement element, string inClass = IN, Action onHidden = null)
		{
			if (element == null)
				return;

			State state = GetState(element);
			int generation = ++state.Generation;
			state.Pending?.Pause();
			element.RemoveFromClassList(inClass);

			if (element.panel == null || element.resolvedStyle.display == DisplayStyle.None)
			{
				element.style.display = DisplayStyle.None;
				state.Pending = null;
				onHidden?.Invoke();
				return;
			}

			// 길이 정본은 USS, 여기는 opacity 결과만 확인
			int hideFrame = Time.frameCount;
			float hideStart = Time.realtimeSinceStartup;
			bool done = false;
			state.Pending = element.schedule.Execute(() =>
			{
				if (state.Generation != generation)
				{
					done = true;
					return;
				}
				bool faded = Time.frameCount > hideFrame && element.resolvedStyle.opacity <= HIDDEN_OPACITY;
				bool timedOut = Time.realtimeSinceStartup - hideStart >= HIDE_FAILSAFE_SECONDS;
				if (faded == false && timedOut == false)
					return;
				element.style.display = DisplayStyle.None;
				done = true;
				onHidden?.Invoke();
			}).Every(0).Until(() => done);
		}

		/// <summary>애니메이션 없이 즉시 상태를 맞춘다. 초기화용</summary>
		public static void SetInstant(VisualElement element, bool visible, string inClass = IN)
		{
			if (element == null)
				return;

			State state = GetState(element);
			state.Generation++;
			state.Pending?.Pause();
			state.Pending = null;
			element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
			element.EnableInClassList(inClass, visible);
		}
	}
}

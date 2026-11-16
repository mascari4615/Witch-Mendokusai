using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// UI Toolkit 열기와 닫기 애니메이션 순서. 움직임은 USS transition 담당
	/// - 열기: display 먼저, 활성 클래스는 다음 프레임. 같은 프레임에 무거운 열기 처리가 있으면 끝 값으로 튐
	///   (2026-11-17 MOIS2 상점 첫 열기 0.29초 멈칫, 65ms 만에 불투명. 두 번째 열기는 200ms 정상)
	/// - 닫기: 클래스 제거 뒤 opacity 0 도달 시 display 끔. display 를 바로 끄면 닫힘 애니메이션이 안 보임
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

		/// <summary>display 를 켜고 다음 프레임에 inClass. 닫히는 중이면 그 자리에서 바로 되돌림</summary>
		public static void Show(VisualElement element, string inClass = IN)
		{
			if (element == null)
				return;

			State state = GetState(element);
			int generation = ++state.Generation;
			state.Pending?.Pause();
			state.Pending = null;

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

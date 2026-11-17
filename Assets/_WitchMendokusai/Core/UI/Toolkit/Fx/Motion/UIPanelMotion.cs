using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 패널과 창이 열리고 닫히는 움직임 에셋. UIFxProfile 의 panelMotion 칸에 끼움
	/// - 새 종류: 상속 클래스에 Open, Close 둘. 열기는 display 를 켜고 시작, 닫기는 끝에 display 를 끔 (Play 가 처리)
	/// - source 는 열기를 부른 요소 (칸, 버튼). 없을 수 있음
	/// - 이름 규격 Motion_{ID}_{이름}.asset
	/// </summary>
	public abstract class UIPanelMotion : ScriptableObject
	{
		private sealed class Running
		{
			public Action Cancel;
		}

		private static readonly ConditionalWeakTable<VisualElement, Running> running = new();
		private static readonly StyleList<TimeValue> NO_TRANSITION = new(new List<TimeValue> { new(0f) });

		/// <summary>열기 또는 닫기. 진행 중이던 것은 멈춤 (새 움직임은 처음 자세에서 시작)</summary>
		public void Play(VisualElement panel, bool open, VisualElement source, Action onDone = null)
		{
			Running state = running.GetValue(panel, _ => new Running());
			state.Cancel?.Invoke();
			state.Cancel = null;
			// USS 움직임이 예약해 둔 display 끄기가 새 열기를 덮지 않게
			UIMotion.CancelPending(panel);
			if (HandlesDisplay)
			{
				Action finished = onDone ?? delegate { };
				state.Cancel = open ? Open(panel, source, finished) : Close(panel, source, finished);
				return;
			}
			// USS transition 이 인라인 값 변화를 다시 보간하지 않게
			panel.style.transitionDuration = NO_TRANSITION;
			panel.AddToClassList(UIMotion.IN);
			if (open)
			{
				panel.style.display = DisplayStyle.Flex;
				panel.SetEnabled(true);
				state.Cancel = Open(panel, source, () =>
				{
					state.Cancel = null;
					panel.style.transitionDuration = StyleKeyword.Null;
					onDone?.Invoke();
				});
				return;
			}
			panel.SetEnabled(false);
			state.Cancel = Close(panel, source, () =>
			{
				state.Cancel = null;
				panel.style.display = DisplayStyle.None;
				panel.SetEnabled(true);
				Restore(panel);
				panel.style.transitionDuration = StyleKeyword.Null;
				onDone?.Invoke();
			});
		}

		/// <summary>열기 움직임. 끝에는 원래 모습 (Restore 와 같은 값). 중단 함수를 돌려줌</summary>
		protected abstract Action Open(VisualElement panel, VisualElement source, Action onDone);

		/// <summary>닫기 움직임. 끝나면 Play 가 display 를 끄고 Restore</summary>
		protected abstract Action Close(VisualElement panel, VisualElement source, Action onDone);

		/// <summary>display 와 입력 차단을 스스로 다루는 종류 (USS 움직임). Play 가 손대지 않음</summary>
		protected virtual bool HandlesDisplay => false;

		// 묶음 에셋이 다른 에셋의 한쪽만 빌릴 때
		internal Action OpenPart(VisualElement panel, VisualElement source, Action onDone) => Open(panel, source, onDone);
		internal Action ClosePart(VisualElement panel, VisualElement source, Action onDone) => Close(panel, source, onDone);

		/// <summary>움직임이 건드린 인라인 값을 원래대로</summary>
		protected static void Restore(VisualElement panel)
		{
			panel.style.scale = StyleKeyword.Null;
			panel.style.rotate = StyleKeyword.Null;
			panel.style.translate = StyleKeyword.Null;
			panel.style.opacity = StyleKeyword.Null;
			panel.style.borderTopLeftRadius = StyleKeyword.Null;
			panel.style.borderTopRightRadius = StyleKeyword.Null;
			panel.style.borderBottomLeftRadius = StyleKeyword.Null;
			panel.style.borderBottomRightRadius = StyleKeyword.Null;
			panel.style.overflow = StyleKeyword.Null;
			panel.style.transformOrigin = StyleKeyword.Null;
		}
	}
}

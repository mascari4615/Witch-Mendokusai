using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 마우스, 포커스가 가리키는 칸에 커서, 선택, 흐림 에셋을 적용. 모양과 수치는 UIFxProfile 의 cursor, select, dim 칸
	/// - 대상: Button 과 wm-idle-target 클래스. wm-idle-skip 아래와 안쪽 WMWindow (창 자신이 맡음) 는 제외
	/// - 커서는 root 위 덮개 층에 그림, 선택은 대상의 크기와 색을 바꾸고 해제 때 그 직전 인라인 값으로 되돌림
	/// - 패널이 닫힐 때 Reset (포인터가 떠나는 이벤트가 오지 않으므로)
	/// </summary>
	public static class UIFocusFx
	{
		private sealed class State
		{
			public VisualElement Root;
			public Func<UIFxProfile> Profile;
			public VisualElement Overlay;
			public VisualElement Cursor;
			public VisualElement Accent;
			public VisualElement Current;
			public Action CursorCancel;
			public Action PulseCancel;
			public Action SelectCancel;
			public Saved Selected;
			public readonly List<VisualElement> Dimmed = new();
		}

		// 바꾸기 직전의 인라인 값
		private sealed class Saved
		{
			public VisualElement Element;
			public StyleColor Background;
			public StyleColor Text;
			public StyleScale Scale;
		}

		private sealed class DimState
		{
			public StyleFloat Original;
			public bool Dimmed;
			public Action Cancel;
		}

		private static readonly ConditionalWeakTable<VisualElement, State> states = new();
		private static readonly ConditionalWeakTable<VisualElement, DimState> dims = new();

		public static void Attach(VisualElement root, Func<UIFxProfile> profile)
		{
			State state = states.GetValue(root, _ => new State());
			state.Root = root;
			state.Profile = profile;
			root.RegisterCallback<PointerOverEvent>(evt => Point(state, FindTarget(state, evt.target as VisualElement)));
			root.RegisterCallback<FocusInEvent>(evt => Point(state, FindTarget(state, evt.target as VisualElement)));
			root.RegisterCallback<PointerLeaveEvent>(_ => Point(state, null));
			root.RegisterCallback<PointerDownEvent>(evt => Flash(state, FindTarget(state, evt.target as VisualElement)), TrickleDown.TrickleDown);
		}

		/// <summary>코드에서 칸을 가리킴 (패드, 시연). null 이면 해제</summary>
		public static void Point(VisualElement root, VisualElement target) => Point(Get(root), target);

		/// <summary>코드에서 누름 번쩍</summary>
		public static void Press(VisualElement root, VisualElement target) => Flash(Get(root), target);

		/// <summary>가리킴, 선택, 흐림, 커서를 모두 즉시 해제. 패널, 창을 닫을 때</summary>
		public static void Reset(VisualElement root)
		{
			if (states.TryGetValue(root, out State state) == false)
				return;
			state.CursorCancel?.Invoke();
			state.PulseCancel?.Invoke();
			state.SelectCancel?.Invoke();
			RestoreSelected(state);
			foreach (VisualElement element in state.Dimmed)
				Undim(element);
			state.Dimmed.Clear();
			state.Current = null;
			if (state.Cursor != null)
				state.Cursor.style.opacity = 0f;
		}

		private static State Get(VisualElement root)
		{
			if (states.TryGetValue(root, out State state) == false)
				throw new InvalidOperationException("UIFocusFx.Attach 를 먼저 부를 것");
			return state;
		}

		private static VisualElement FindTarget(State state, VisualElement from)
		{
			for (VisualElement current = from; current != null && current != state.Root; current = current.parent)
			{
				if (current.ClassListContains(UIIdleFx.SKIP_CLASS) || current is WMWindow)
					return null;
				if (current is Button || current.ClassListContains(UIIdleFx.TARGET_CLASS))
					return current.enabledInHierarchy ? current : null;
			}
			return null;
		}

		private static void Point(State state, VisualElement target)
		{
			if (target == state.Current)
				return;
			UIFxProfile profile = state.Profile();
			state.Current = target;
			if (profile == null)
				return;
			if (profile.select != null)
				Select(state, profile.select, target);
			if (profile.dim != null)
				Dim(state, profile.dim, target);
			if (profile.cursor != null)
				MoveCursor(state, profile.cursor, target);
		}

		private static void RestoreSelected(State state)
		{
			Saved saved = state.Selected;
			if (saved == null)
				return;
			saved.Element.style.scale = saved.Scale;
			saved.Element.style.backgroundColor = saved.Background;
			saved.Element.style.color = saved.Text;
			state.Selected = null;
		}

		private static void Select(State state, UISelectStyle style, VisualElement target)
		{
			state.SelectCancel?.Invoke();
			RestoreSelected(state);
			if (target == null)
				return;
			state.Selected = new Saved { Element = target, Background = target.style.backgroundColor, Text = target.style.color, Scale = target.style.scale };
			if (style.invert)
			{
				target.style.backgroundColor = style.fillColor;
				target.style.color = style.textColor;
			}
			state.SelectCancel = UIFxTween.Run(target, style.seconds, 0f, progress =>
			{
				float scale = Mathf.LerpUnclamped(1f, style.scale, UIFxTween.OutBack(progress, style.overshoot));
				target.style.scale = new Scale(new Vector2(scale, scale));
			});
		}

		private static void Flash(State state, VisualElement target)
		{
			UIFxProfile profile = state.Profile();
			UISelectStyle style = profile != null ? profile.select : null;
			if (target == null || style == null)
				return;
			StyleColor before = target.style.backgroundColor;
			target.style.backgroundColor = style.flashColor;
			UIFxTween.After(target, style.flashSeconds, () =>
			{
				bool selected = state.Selected != null && state.Selected.Element == target;
				target.style.backgroundColor = selected && style.invert ? new StyleColor(style.fillColor) : before;
			});
		}

		private static void Dim(State state, UIDimStyle style, VisualElement target)
		{
			List<VisualElement> wanted = new();
			if (target != null && target.parent != null)
			{
				foreach (VisualElement sibling in target.parent.Children())
				{
					if (sibling != target && (sibling is Button || sibling.ClassListContains(UIIdleFx.TARGET_CLASS)))
						wanted.Add(sibling);
				}
			}
			foreach (VisualElement element in state.Dimmed)
			{
				if (wanted.Contains(element) == false)
					Fade(element, style, false);
			}
			foreach (VisualElement element in wanted)
				Fade(element, style, true);
			state.Dimmed.Clear();
			state.Dimmed.AddRange(wanted);
		}

		private static void Fade(VisualElement element, UIDimStyle style, bool dimmed)
		{
			DimState dim = dims.GetValue(element, _ => new DimState());
			if (dimmed == dim.Dimmed)
				return;
			dim.Cancel?.Invoke();
			if (dimmed)
				dim.Original = element.style.opacity;
			dim.Dimmed = dimmed;
			float from = element.resolvedStyle.opacity;
			float to = dimmed ? style.dimOpacity : 1f;
			dim.Cancel = UIFxTween.Run(element, style.seconds, 0f, progress => element.style.opacity = Mathf.Lerp(from, to, progress), () =>
			{
				dim.Cancel = null;
				if (dimmed == false)
					element.style.opacity = dim.Original;
			});
		}

		private static void Undim(VisualElement element)
		{
			if (dims.TryGetValue(element, out DimState dim) == false || dim.Dimmed == false)
				return;
			dim.Cancel?.Invoke();
			dim.Cancel = null;
			dim.Dimmed = false;
			element.style.opacity = dim.Original;
		}

		private static void MoveCursor(State state, UICursorStyle style, VisualElement target)
		{
			EnsureCursor(state, style);
			state.CursorCancel?.Invoke();
			state.PulseCancel?.Invoke();
			if (target == null)
			{
				state.CursorCancel = UIFxTween.Run(state.Cursor, style.slideSeconds, 0f, progress => state.Cursor.style.opacity = 1f - progress);
				return;
			}
			state.Overlay.BringToFront();
			bool hidden = state.Cursor.resolvedStyle.opacity <= UIFxTween.VISIBLE_OPACITY || state.Cursor.resolvedStyle.width <= 0f;
			Rect from = state.Cursor.worldBound;
			state.Cursor.style.opacity = 1f;
			// 선택 확대가 진행 중이어도 끝 자리를 쫓도록 매 프레임 대상 위치를 다시 읽음
			state.CursorCancel = UIFxTween.Run(state.Cursor, hidden ? 0f : style.slideSeconds, 0f, progress =>
			{
				Rect to = Padded(target, style);
				if (hidden)
					from = to;
				float eased = UIFxTween.OutBack(progress, style.overshoot);
				Rect now = new(Vector2.LerpUnclamped(from.position, to.position, eased), Vector2.LerpUnclamped(from.size, to.size, eased));
				UIFxTween.PlaceOver(state.Cursor, state.Overlay, now);
			}, () => state.PulseCancel = Follow(state, style, target));
		}

		private static Rect Padded(VisualElement target, UICursorStyle style)
		{
			Rect bound = target.worldBound;
			return new Rect(bound.x - style.padding, bound.y - style.padding, bound.width + style.padding * 2f, bound.height + style.padding * 2f);
		}

		// 멈춘 동안 맥동하며 대상 위치를 따라감 (스크롤, 레이아웃 변화). 대상이 숨으면 커서도 감추고 멈춤
		private static Action Follow(State state, UICursorStyle style, VisualElement target)
		{
			VisualElement cursor = state.Cursor;
			float elapsed = 0f;
			bool stopped = false;
			cursor.schedule.Execute(timer =>
			{
				if (UIIdleFx.IsShown(target) == false)
				{
					cursor.style.opacity = 0f;
					stopped = true;
					return;
				}
				elapsed += UIFxTween.Seconds(timer);
				UIFxTween.PlaceOver(cursor, state.Overlay, Padded(target, style));
				float wave = 0.5f - 0.5f * Mathf.Cos(elapsed / UIFxTween.Duration(style.pulseSeconds) * Mathf.PI * 2f);
				cursor.style.opacity = 1f - wave * style.pulseDepth;
			}).Every(0).Until(() => stopped || cursor.panel == null);
			return () =>
			{
				stopped = true;
				cursor.style.opacity = 1f;
			};
		}

		private static void EnsureCursor(State state, UICursorStyle style)
		{
			if (state.Overlay == null)
			{
				state.Overlay = UIFxTween.MakeLayer("focus-overlay");
				state.Cursor = new VisualElement { name = "focus-cursor", pickingMode = PickingMode.Ignore };
				state.Cursor.style.position = Position.Absolute;
				state.Cursor.style.opacity = 0f;
				state.Accent = new VisualElement { pickingMode = PickingMode.Ignore };
				state.Accent.style.position = Position.Absolute;
				state.Accent.style.left = 0;
				state.Accent.style.top = 0;
				state.Accent.style.bottom = 0;
				state.Cursor.Add(state.Accent);
				state.Overlay.Add(state.Cursor);
			}
			if (state.Overlay.parent != state.Root)
				state.Root.Add(state.Overlay);
			Color fill = style.color;
			fill.a *= style.fillAlpha;
			state.Cursor.style.backgroundColor = fill;
			UIFxTween.SetBorder(state.Cursor, style.borderWidth, style.color);
			UIFxTween.SetRadius(state.Cursor, style.cornerRadius);
			state.Cursor.style.rotate = UIFxTween.Degrees(style.tiltDegrees);
			state.Accent.style.width = style.accentWidth;
			state.Accent.style.backgroundColor = style.color;
		}
	}
}

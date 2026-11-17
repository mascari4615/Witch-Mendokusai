using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 평상시 요소 효과. 화면 안 버튼, 칸, 아이콘 중 하나에 가끔 짧은 효과
	/// - 대상: Button, 배경 그림이 있는 입력 칸, wm-idle-target 클래스. wm-idle-skip, 비활성, 포커스된 요소는 제외
	/// - 효과는 root 위 덮개 층에 그림. 대상 요소의 자식과 레이아웃은 건드리지 않음
	/// - 효과 하나가 끝나야 다음 간격을 셈. root 나 조상이 숨으면 쉬고, root 가 패널에서 떨어진 동안은 일정이 멈춤
	/// - 안쪽 WMWindow 는 창 자신의 루프가 맡으므로 건너뜀
	/// - 효과 모양과 수치는 UIFxProfile 의 idleEffect 에셋. 간격과 대상 거르기는 UIFxSettings 의 평상시 절
	/// - root 아래에 wm-idle-main 클래스가 하나라도 있으면 그것들에만 (반복 효과는 주 동작에만)
	/// </summary>
	public static class UIIdleFx
	{
		public const string TARGET_CLASS = "wm-idle-target";
		public const string SKIP_CLASS = "wm-idle-skip";
		/// <summary>주 동작. 있으면 평상시 효과가 이것들에만</summary>
		public const string MAIN_CLASS = "wm-idle-main";
		/// <summary>효과와 입자 층. 패널 몸통을 셀 때 빼는 표시</summary>
		public const string LAYER_CLASS = "wm-fx-layer";
		private const float MILLISECONDS_PER_SECOND = 1000f;
		private const long TICK_MILLISECONDS = 50;
		private const float MAX_STEP_SECONDS = 0.1f;

		private sealed class Loop
		{
			public VisualElement Root;
			public VisualElement Overlay;
			public float Wait;
			public float BusySeconds;
			public int Running;
			public int Generation;
			public Func<UIIdleEffect> Source;
		}

		/// <summary>root 아래 요소에 평상시 효과를 돌림. source 가 null 이나 빈 에셋을 주면 쉼</summary>
		public static void Attach(VisualElement root, Func<UIIdleEffect> source)
		{
			Loop loop = new() { Root = root, Wait = NextWait(), Source = source };
			root.schedule.Execute(timer => Tick(loop, timer)).Every(TICK_MILLISECONDS);
		}

		private static void Tick(Loop loop, TimerState timer)
		{
			UIFxSettings settings = UIFxSettings.Instance;
			float deltaSeconds = Mathf.Min(timer.deltaTime / MILLISECONDS_PER_SECOND, MAX_STEP_SECONDS);
			if (loop.Running > 0)
			{
				loop.BusySeconds += deltaSeconds;
				if (loop.BusySeconds < settings.idleBusyTimeoutSeconds)
					return;
				// 늦게 오는 완료는 회차가 달라 무시됨
				loop.Running = 0;
				loop.Generation++;
			}
			UIIdleEffect effect = loop.Source();
			if (effect == null || IsShown(loop.Root) == false)
				return;
			loop.Wait -= deltaSeconds;
			if (loop.Wait > 0f)
				return;
			loop.Wait = NextWait();
			List<VisualElement> targets = CollectTargets(loop.Root, settings);
			if (targets.Count == 0)
				return;
			EnsureOverlay(loop);
			loop.BusySeconds = 0f;
			// 끄기 에셋이나 묶음이 이번엔 쉬라고 하면 null
			UIIdleEffect picked = effect.Pick();
			if (picked == null)
				return;
			Play(loop, picked, targets);
		}

		private static float NextWait()
		{
			Vector2 range = UIFxSettings.Instance.idleIntervalRange;
			return UnityEngine.Random.Range(range.x, range.y);
		}

		private static void EnsureOverlay(Loop loop)
		{
			if (loop.Overlay == null)
			{
				loop.Overlay = MakeFill("idle-overlay");
				loop.Overlay.AddToClassList(LAYER_CLASS);
				loop.Overlay.AddToClassList(SKIP_CLASS);
			}
			if (loop.Overlay.parent != loop.Root)
				loop.Root.Add(loop.Overlay);
			// 다른 자식보다 위에
			loop.Overlay.BringToFront();
		}

		private static void Play(Loop loop, UIIdleEffect effect, List<VisualElement> targets)
		{
			int generation = ++loop.Generation;
			Action finished = () =>
			{
				if (generation != loop.Generation)
					return;
				loop.Running--;
			};
			VisualElement target = targets[UnityEngine.Random.Range(0, targets.Count)];
			// 그리는 중 바로 완료가 와도 셈이 맞도록 0 에서 시작해 더함
			// (x += f() 는 f 전에 x 를 읽어 그 사이 감소를 잃으므로 따로 받음)
			loop.Running = 0;
			int expected = effect.Play(loop.Overlay, target, targets, finished);
			loop.Running += expected;
		}

		/// <summary>자신과 조상 모두 보이는지. 다른 UI 연출도 숨은 동안 쉬는 데 씀</summary>
		public static bool IsShown(VisualElement root)
		{
			if (root.panel == null)
				return false;
			for (VisualElement current = root; current != null; current = current.parent)
			{
				IResolvedStyle resolved = current.resolvedStyle;
				if (resolved.display == DisplayStyle.None || resolved.visibility == Visibility.Hidden || resolved.opacity < UIFxSettings.Instance.idleVisibleOpacity)
					return false;
			}
			return true;
		}

		private static List<VisualElement> CollectTargets(VisualElement root, UIFxSettings settings)
		{
			List<VisualElement> found = new();
			Rect rootBound = root.worldBound;
			float maxArea = rootBound.width * rootBound.height * settings.idleMaxTargetAreaRatio;
			Focusable focused = root.focusController?.focusedElement;
			Stack<(VisualElement element, Rect clip)> stack = new();
			stack.Push((root, rootBound));
			while (stack.Count > 0)
			{
				(VisualElement current, Rect clip) = stack.Pop();
				if (current.resolvedStyle.display == DisplayStyle.None || current.visible == false || current.ClassListContains(SKIP_CLASS))
					continue;
				// 안쪽 창은 창 자신의 루프가 맡음
				if (current != root && current is WMWindow)
					continue;
				if (current != root && current != focused && IsTarget(current, settings, clip, maxArea))
					found.Add(current);
				// 스크롤 영역 밖으로 잘린 칸은 제외
				Rect childClip = current is ScrollView scrollView ? Intersect(clip, scrollView.contentViewport.worldBound) : clip;
				foreach (VisualElement child in current.Children())
					stack.Push((child, childClip));
			}
			List<VisualElement> main = found.FindAll(element => element.ClassListContains(MAIN_CLASS));
			return main.Count > 0 ? main : found;
		}

		private static Rect Intersect(Rect first, Rect second) =>
			Rect.MinMaxRect(Mathf.Max(first.xMin, second.xMin), Mathf.Max(first.yMin, second.yMin), Mathf.Min(first.xMax, second.xMax), Mathf.Min(first.yMax, second.yMax));

		private static bool IsTarget(VisualElement element, UIFxSettings settings, Rect clip, float maxArea)
		{
			bool candidate = element is Button
				|| element.ClassListContains(TARGET_CLASS)
				|| (element.pickingMode == PickingMode.Position && HasImage(element));
			if (candidate == false || element.enabledInHierarchy == false)
				return false;
			Rect bound = element.worldBound;
			if (bound.width < settings.idleMinTargetSize || bound.height < settings.idleMinTargetSize)
				return false;
			if (bound.width * bound.height > maxArea)
				return false;
			return clip.Contains(bound.center);
		}

		private static bool HasImage(VisualElement element)
		{
			Background image = element.resolvedStyle.backgroundImage;
			return image.texture != null || image.sprite != null || image.renderTexture != null || image.vectorImage != null;
		}

		/// <summary>같은 부모의 대상 형제들. 왼쪽 위부터</summary>
		public static List<VisualElement> SameRow(VisualElement target, List<VisualElement> targets)
		{
			List<VisualElement> row = new();
			foreach (VisualElement candidate in targets)
			{
				if (candidate.parent == target.parent)
					row.Add(candidate);
			}
			row.Sort((left, right) =>
			{
				Rect leftBound = left.worldBound;
				Rect rightBound = right.worldBound;
				return (leftBound.x + leftBound.y).CompareTo(rightBound.x + rightBound.y);
			});
			return row;
		}

		/// <summary>덮개 층 위에 대상 테두리 상자와 같은 자리, 같은 모서리 둥글기의 층</summary>
		public static VisualElement Cover(VisualElement overlay, VisualElement target, string layerName)
		{
			Rect bound = target.worldBound;
			Vector2 topLeft = overlay.WorldToLocal(bound.min);
			Vector2 bottomRight = overlay.WorldToLocal(bound.max);
			VisualElement layer = new() { name = layerName, pickingMode = PickingMode.Ignore };
			layer.style.position = Position.Absolute;
			layer.style.left = topLeft.x;
			layer.style.top = topLeft.y;
			layer.style.width = bottomRight.x - topLeft.x;
			layer.style.height = bottomRight.y - topLeft.y;
			layer.style.borderTopLeftRadius = target.resolvedStyle.borderTopLeftRadius;
			layer.style.borderTopRightRadius = target.resolvedStyle.borderTopRightRadius;
			layer.style.borderBottomLeftRadius = target.resolvedStyle.borderBottomLeftRadius;
			layer.style.borderBottomRightRadius = target.resolvedStyle.borderBottomRightRadius;
			overlay.Add(layer);
			return layer;
		}

		/// <summary>layer 에 seconds 동안 진행 0~1 을 넘기고, 끝나면 layer 를 떼고 onDone</summary>
		public static void Animate(VisualElement layer, float seconds, float delay, Action<float> step, Action onDone)
		{
			float elapsed = -delay;
			bool done = false;
			layer.schedule.Execute(timer =>
			{
				elapsed += Mathf.Min(timer.deltaTime / MILLISECONDS_PER_SECOND, MAX_STEP_SECONDS);
				if (elapsed < 0f)
					return;
				float progress = seconds <= 0f ? 1f : Mathf.Clamp01(elapsed / seconds);
				step(progress);
				if (progress < 1f)
					return;
				layer.RemoveFromHierarchy();
				done = true;
				onDone?.Invoke();
			}).Every(0).Until(() => done);
		}

		private static VisualElement MakeFill(string layerName)
		{
			VisualElement layer = new() { name = layerName, pickingMode = PickingMode.Ignore };
			layer.style.position = Position.Absolute;
			layer.style.left = 0;
			layer.style.top = 0;
			layer.style.right = 0;
			layer.style.bottom = 0;
			return layer;
		}
	}
}

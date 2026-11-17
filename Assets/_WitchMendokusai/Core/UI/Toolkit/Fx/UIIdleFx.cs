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
	/// - 모양과 수치는 UIFxSettings 의 평상시 절
	/// </summary>
	public static class UIIdleFx
	{
		public const string TARGET_CLASS = "wm-idle-target";
		public const string SKIP_CLASS = "wm-idle-skip";
		/// <summary>효과와 입자 층. 패널 몸통을 셀 때 빼는 표시</summary>
		public const string LAYER_CLASS = "wm-fx-layer";
		private const float MILLISECONDS_PER_SECOND = 1000f;
		private const long TICK_MILLISECONDS = 50;
		private const float MAX_STEP_SECONDS = 0.1f;
		// Twinkle 이 피는 구간 비율. 나머지는 지는 구간
		private const float TWINKLE_BLOOM_PART = 0.35f;

		private sealed class Loop
		{
			public VisualElement Root;
			public VisualElement Overlay;
			public float Wait;
			public float BusySeconds;
			public int Running;
			public int Generation;
		}

		/// <summary>root 아래 요소에 평상시 효과를 돌림</summary>
		public static void Attach(VisualElement root)
		{
			Loop loop = new() { Root = root, Wait = NextWait() };
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
			if (settings.idleFx == IdleFxStyle.None || IsShown(loop.Root) == false)
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
			Play(loop, Pick(settings.idleFx, settings), targets);
		}

		private static float NextWait()
		{
			Vector2 range = UIFxSettings.Instance.idleIntervalRange;
			return Random.Range(range.x, range.y);
		}

		private static IdleFxStyle Pick(IdleFxStyle style, UIFxSettings settings)
		{
			if (style != IdleFxStyle.Mixed)
				return style;
			IdleFxStyle[] pool = settings.idleMixedPool;
			Debug.Assert(pool != null && pool.Length > 0, "UIFxSettings.idleMixedPool 비어 있음");
			IdleFxStyle picked = pool[Random.Range(0, pool.Length)];
			return picked == IdleFxStyle.Mixed || picked == IdleFxStyle.None ? IdleFxStyle.Glint : picked;
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

		private static void Play(Loop loop, IdleFxStyle style, List<VisualElement> targets)
		{
			int generation = ++loop.Generation;
			System.Action finished = () =>
			{
				if (generation != loop.Generation)
					return;
				loop.Running--;
			};
			VisualElement target = targets[Random.Range(0, targets.Count)];
			if (style == IdleFxStyle.Wave)
			{
				List<VisualElement> row = SameRow(target, targets);
				int count = Mathf.Min(row.Count, UIFxSettings.Instance.idleWaveMaxItems);
				loop.Running = count;
				for (int i = 0; i < count; i++)
					Glint(loop.Overlay, row[i], UIFxSettings.Instance.idleWaveStepSeconds * i, finished);
				return;
			}
			loop.Running = 1;
			switch (style)
			{
				case IdleFxStyle.Twinkle:
					Twinkle(loop.Overlay, target, finished);
					return;
				case IdleFxStyle.Pulse:
					Outline(loop.Overlay, target, false, finished);
					return;
				case IdleFxStyle.Ring:
					Outline(loop.Overlay, target, true, finished);
					return;
				default:
					Glint(loop.Overlay, target, 0f, finished);
					return;
			}
		}

		// 자신과 조상 모두 보여야 함 (창 안 Content 는 창이 닫혀도 자기 값은 그대로)
		private static bool IsShown(VisualElement root)
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
			return found;
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

		// 같은 부모의 대상 형제들. 왼쪽 위부터
		private static List<VisualElement> SameRow(VisualElement target, List<VisualElement> targets)
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

		// 덮개 층 위에 대상 테두리 상자와 같은 자리, 같은 모서리 둥글기의 층
		private static VisualElement Cover(VisualElement overlay, VisualElement target, string layerName)
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

		// 요소 위를 비스듬한 빛 띠가 한 번 훑음. 크기는 한 번 정하고 이동은 translate
		private static void Glint(VisualElement overlay, VisualElement target, float delay, System.Action onDone)
		{
			UIFxSettings settings = UIFxSettings.Instance;
			Rect bound = target.worldBound;
			VisualElement clip = Cover(overlay, target, "idle-glint");
			clip.style.overflow = Overflow.Hidden;

			float width = Mathf.Max(bound.width, bound.height) * settings.idleGlintWidthRatio;
			float height = (bound.height + width) * settings.idleGlintLength;
			VisualElement band = new() { pickingMode = PickingMode.Ignore };
			band.style.position = Position.Absolute;
			band.style.width = width;
			band.style.height = height;
			band.style.left = 0f;
			band.style.top = (bound.height - height) * 0.5f;
			band.style.backgroundImage = new StyleBackground(UIFxTextures.Band);
			Color tint = settings.idleColor;
			tint.a *= settings.idleGlintAlpha;
			band.style.unityBackgroundImageTintColor = tint;
			band.style.rotate = new UnityEngine.UIElements.Rotate(new UnityEngine.UIElements.Angle(settings.idleGlintAngle, AngleUnit.Degree));
			band.style.opacity = 0f;
			clip.Add(band);

			float from = -width * settings.idleGlintTravel.x;
			float to = bound.width + width * settings.idleGlintTravel.y;
			Animate(clip, settings.idleGlintSeconds, delay, progress =>
			{
				float eased = progress * progress * (3f - 2f * progress);
				band.style.translate = new Translate(Mathf.Lerp(from, to, eased), 0f);
				band.style.opacity = Mathf.Sin(progress * Mathf.PI);
			}, onDone);
		}

		// 요소 모서리 근처에 십자 반짝임이 돌며 피었다 짐
		private static void Twinkle(VisualElement overlay, VisualElement target, System.Action onDone)
		{
			UIFxSettings settings = UIFxSettings.Instance;
			Rect bound = target.worldBound;
			Vector2 jitter = settings.idleTwinkleSizeJitter;
			float size = settings.idleTwinkleSize * Random.Range(jitter.x, jitter.y);
			Vector2 inset = settings.idleTwinkleCornerRange;
			float CornerAxis(float length) => Random.value < 0.5f ? length * Random.Range(inset.x, inset.y) : length * (1f - Random.Range(inset.x, inset.y));

			VisualElement host = Cover(overlay, target, "idle-twinkle");
			VisualElement star = new() { pickingMode = PickingMode.Ignore };
			star.style.position = Position.Absolute;
			star.style.width = size;
			star.style.height = size;
			star.style.left = CornerAxis(bound.width) - size * 0.5f;
			star.style.top = CornerAxis(bound.height) - size * 0.5f;
			star.style.backgroundImage = new StyleBackground(UIFxTextures.Shape(ParticleShape.Sparkle));
			star.style.unityBackgroundImageTintColor = settings.idleColor;
			star.style.scale = new Scale(Vector2.zero);
			host.Add(star);

			float spin = settings.idleTwinkleTurnDegrees * (Random.value < 0.5f ? -1f : 1f);
			Animate(host, settings.idleTwinkleSeconds, 0f, progress =>
			{
				// 빠르게 피고 천천히 짐
				float bloom = progress < TWINKLE_BLOOM_PART
					? progress / TWINKLE_BLOOM_PART
					: 1f - (progress - TWINKLE_BLOOM_PART) / (1f - TWINKLE_BLOOM_PART);
				float grown = Mathf.Sin(bloom * Mathf.PI * 0.5f);
				star.style.scale = new Scale(new Vector2(grown, grown));
				star.style.rotate = new UnityEngine.UIElements.Rotate(new UnityEngine.UIElements.Angle(spin * progress, AngleUnit.Degree));
				star.style.opacity = grown;
			}, onDone);
		}

		// 테두리 빛. ring 이면 바깥으로 퍼지며 사라짐, 아니면 제자리에서 한 번 숨 쉼
		private static void Outline(VisualElement overlay, VisualElement target, bool ring, System.Action onDone)
		{
			UIFxSettings settings = UIFxSettings.Instance;
			VisualElement outline = Cover(overlay, target, ring ? "idle-ring" : "idle-pulse");
			float border = settings.idleOutlineWidth;
			outline.style.borderTopWidth = border;
			outline.style.borderBottomWidth = border;
			outline.style.borderLeftWidth = border;
			outline.style.borderRightWidth = border;
			Color edge = settings.idleColor;
			outline.style.borderTopColor = edge;
			outline.style.borderBottomColor = edge;
			outline.style.borderLeftColor = edge;
			outline.style.borderRightColor = edge;
			if (ring == false)
			{
				Color inner = settings.idleColor;
				inner.a *= settings.idlePulseFill;
				outline.style.backgroundColor = inner;
			}
			outline.style.opacity = 0f;

			float seconds = ring ? settings.idleRingSeconds : settings.idlePulseSeconds;
			Animate(outline, seconds, 0f, progress =>
			{
				if (ring)
				{
					float eased = 1f - (1f - progress) * (1f - progress);
					float grown = Mathf.Lerp(1f, settings.idleRingGrowth, eased);
					outline.style.scale = new Scale(new Vector2(grown, grown));
					outline.style.opacity = 1f - eased;
					return;
				}
				outline.style.opacity = Mathf.Sin(progress * Mathf.PI);
			}, onDone);
		}

		private static void Animate(VisualElement layer, float seconds, float delay, System.Action<float> step, System.Action onDone)
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

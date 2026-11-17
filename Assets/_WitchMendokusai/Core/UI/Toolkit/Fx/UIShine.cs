using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 창이 열릴 때 겉면 빛 (빛 띠, 테두리 번쩍임), 자식 순차 등장. 수치는 UIFxSettings
	/// </summary>
	public static class UIShine
	{
		private const float MILLISECONDS_PER_SECOND = 1000f;

		/// <summary>창이 열릴 때 겉면 빛. 모양은 shineStyle</summary>
		public static void Sweep(VisualElement target)
		{
			UIFxSettings settings = UIFxSettings.Instance;
			switch (settings.shineStyle)
			{
				case ShineStyle.None:
					return;
				case ShineStyle.EdgeFlash:
					EdgeFlash(target, settings);
					return;
				case ShineStyle.DoubleSweep:
					Band(target, settings, 0f, 1f);
					// 뒤따르는 가는 띠
					Band(target, settings, settings.shineSeconds * 0.3f, 0.45f);
					return;
				default:
					Band(target, settings, 0f, 1f);
					return;
			}
		}

		// 테두리가 한 번 밝게 빛나고 안쪽이 살짝 환해졌다 사라짐
		private static void EdgeFlash(VisualElement target, UIFxSettings settings)
		{
			VisualElement flash = new() { name = "shine-flash", pickingMode = PickingMode.Ignore };
			flash.style.position = Position.Absolute;
			flash.style.left = 0;
			flash.style.top = 0;
			flash.style.right = 0;
			flash.style.bottom = 0;
			float border = settings.shineEdgeWidth;
			flash.style.borderTopWidth = border;
			flash.style.borderBottomWidth = border;
			flash.style.borderLeftWidth = border;
			flash.style.borderRightWidth = border;
			Color edge = settings.shineColor;
			flash.style.borderTopColor = edge;
			flash.style.borderBottomColor = edge;
			flash.style.borderLeftColor = edge;
			flash.style.borderRightColor = edge;
			Color inner = settings.shineColor;
			inner.a *= settings.shineAlpha;
			flash.style.backgroundColor = inner;
			// 둥근 창 모서리를 따라감
			flash.style.borderTopLeftRadius = target.resolvedStyle.borderTopLeftRadius;
			flash.style.borderTopRightRadius = target.resolvedStyle.borderTopRightRadius;
			flash.style.borderBottomLeftRadius = target.resolvedStyle.borderBottomLeftRadius;
			flash.style.borderBottomRightRadius = target.resolvedStyle.borderBottomRightRadius;
			flash.style.opacity = 0f;
			target.Add(flash);

			float elapsed = -settings.shineDelaySeconds;
			bool done = false;
			flash.schedule.Execute(timer =>
			{
				elapsed += timer.deltaTime / MILLISECONDS_PER_SECOND;
				if (elapsed < 0f)
					return;
				float progress = Mathf.Clamp01(elapsed / settings.shineSeconds);
				// 빠르게 켜지고 천천히 꺼짐
				flash.style.opacity = progress < 0.2f ? progress / 0.2f : 1f - (progress - 0.2f) / 0.8f;
				if (progress < 1f)
					return;
				flash.RemoveFromHierarchy();
				done = true;
			}).Every(0).Until(() => done);
		}

		// 빛 띠 하나. extraDelay 만큼 늦게, 너비는 widthScale 배
		private static void Band(VisualElement target, UIFxSettings settings, float extraDelay, float widthScale)
		{
			VisualElement clip = new() { name = "shine-clip", pickingMode = PickingMode.Ignore };
			clip.style.position = Position.Absolute;
			clip.style.left = 0;
			clip.style.top = 0;
			clip.style.right = 0;
			clip.style.bottom = 0;
			clip.style.overflow = Overflow.Hidden;
			target.Add(clip);

			VisualElement band = new() { pickingMode = PickingMode.Ignore };
			band.style.position = Position.Absolute;
			band.style.backgroundImage = new StyleBackground(UIFxTextures.Band);
			Color tint = settings.shineColor;
			tint.a *= settings.shineAlpha;
			band.style.unityBackgroundImageTintColor = tint;
			band.style.rotate = new UnityEngine.UIElements.Rotate(new UnityEngine.UIElements.Angle(settings.shineAngle, AngleUnit.Degree));
			band.style.opacity = 0f;
			clip.Add(band);

			float elapsed = -settings.shineDelaySeconds - extraDelay;
			bool done = false;
			clip.schedule.Execute(timer =>
			{
				Rect area = clip.contentRect;
				elapsed += timer.deltaTime / MILLISECONDS_PER_SECOND;
				if (elapsed < 0f || area.width <= 0f)
					return;
				float progress = Mathf.Clamp01(elapsed / settings.shineSeconds);
				float width = area.width * settings.shineWidthRatio * widthScale;
				float height = area.height * settings.shineBandLength;
				float eased = 1f - (1f - progress) * (1f - progress);
				band.style.width = width;
				band.style.height = height;
				band.style.left = Mathf.Lerp(-width * 1.5f, area.width + width * 0.5f, eased);
				band.style.top = (area.height - height) * 0.5f;
				band.style.opacity = Mathf.Sin(progress * Mathf.PI);
				if (progress < 1f)
					return;
				clip.RemoveFromHierarchy();
				done = true;
			}).Every(0).Until(() => done);
		}

		/// <summary>창 안에서 칸이 가장 많은 자손 (인벤토리 격자 등) 을 찾아 물결처럼 차례로</summary>
		public static void StaggerGrid(VisualElement root)
		{
			UIFxSettings settings = UIFxSettings.Instance;
			if (settings.staggerChildren == false)
				return;
			VisualElement grid = FindGrid(root, settings.gridStaggerMinItems);
			if (grid != null)
				StaggerChildren(grid, settings.gridStaggerStepSeconds);
		}

		private static VisualElement FindGrid(VisualElement root, int minItems)
		{
			Queue<VisualElement> queue = new();
			queue.Enqueue(root);
			VisualElement best = null;
			while (queue.Count > 0)
			{
				VisualElement current = queue.Dequeue();
				if (current != root && current.childCount >= minItems && (best == null || current.childCount > best.childCount))
					best = current;
				foreach (VisualElement child in current.Children())
				{
					if (child.pickingMode == PickingMode.Position || child.childCount > 0)
						queue.Enqueue(child);
				}
			}
			return best;
		}

		/// <summary>컨테이너 자식을 위에서부터 차례로 떠오르게. stepSeconds 0 이하면 설정값</summary>
		public static void StaggerChildren(VisualElement container, float stepSeconds = 0f)
		{
			UIFxSettings settings = UIFxSettings.Instance;
			if (settings.staggerChildren == false)
				return;
			float step = stepSeconds > 0f ? stepSeconds : settings.staggerStepSeconds;

			int index = 0;
			foreach (VisualElement child in container.Children())
			{
				// 입력 무시 층 (빛 입자, 장식) 은 제외
				if (child.pickingMode == PickingMode.Ignore)
					continue;
				VisualElement item = child;
				float elapsed = -step * index;
				bool done = false;
				item.style.opacity = 0f;
				item.style.translate = new Translate(0, settings.staggerRisePixels);
				item.schedule.Execute(timer =>
				{
					elapsed += timer.deltaTime / MILLISECONDS_PER_SECOND;
					float progress = Mathf.Clamp01(elapsed / settings.staggerItemSeconds);
					float eased = 1f - Mathf.Pow(1f - progress, 3f);
					item.style.opacity = eased;
					item.style.translate = new Translate(0, settings.staggerRisePixels * (1f - eased));
					if (progress < 1f)
						return;
					item.style.opacity = StyleKeyword.Null;
					item.style.translate = StyleKeyword.Null;
					done = true;
				}).Every(0).Until(() => done);
				index++;
			}
		}
	}
}

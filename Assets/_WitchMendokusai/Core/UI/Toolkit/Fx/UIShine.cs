using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 창 위를 한 번 비스듬히 지나가는 빛 띠, 자식 순차 등장. 수치는 UIFxSettings
	/// </summary>
	public static class UIShine
	{
		private const float MILLISECONDS_PER_SECOND = 1000f;
		// 회전한 띠가 모서리까지 덮도록 높이를 늘리는 배수
		private const float BAND_HEIGHT_MULTIPLIER = 2.2f;

		/// <summary>대상 위로 빛 띠를 왼쪽에서 오른쪽으로 한 번</summary>
		public static void Sweep(VisualElement target)
		{
			UIFxSettings settings = UIFxSettings.Instance;
			if (settings.windowShine == false)
				return;

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
			band.style.unityBackgroundImageTintColor = new Color(1f, 1f, 1f, settings.shineAlpha);
			band.style.rotate = new Rotate(settings.shineAngle);
			band.style.opacity = 0f;
			clip.Add(band);

			float elapsed = -settings.shineDelaySeconds;
			bool done = false;
			clip.schedule.Execute(timer =>
			{
				Rect area = clip.contentRect;
				elapsed += timer.deltaTime / MILLISECONDS_PER_SECOND;
				if (elapsed < 0f || area.width <= 0f)
					return;
				float progress = Mathf.Clamp01(elapsed / settings.shineSeconds);
				float width = area.width * settings.shineWidthRatio;
				float height = area.height * BAND_HEIGHT_MULTIPLIER;
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

		/// <summary>컨테이너 자식을 위에서부터 차례로 떠오르게</summary>
		public static void StaggerChildren(VisualElement container)
		{
			UIFxSettings settings = UIFxSettings.Instance;
			if (settings.staggerChildren == false)
				return;

			int index = 0;
			foreach (VisualElement child in container.Children())
			{
				// 입력 무시 층 (빛 입자, 장식) 은 제외
				if (child.pickingMode == PickingMode.Ignore)
					continue;
				VisualElement item = child;
				float elapsed = -settings.staggerStepSeconds * index;
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

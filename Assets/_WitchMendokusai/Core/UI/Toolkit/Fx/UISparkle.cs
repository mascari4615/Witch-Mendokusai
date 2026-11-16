using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// UI 위 반짝이 입자. 창이나 패널 테두리에서 튀어 떠오르며 깜박이고 사라짐
	/// 입자는 대상의 자식으로 붙고 끝나면 스스로 떨어짐. 수치는 UIFxSettings
	/// </summary>
	public static class UISparkle
	{
		private const float MILLISECONDS_PER_SECOND = 1000f;
		// 깜박임 횟수 (수명 동안)
		private const float TWINKLE_CYCLES = 2.5f;

		private sealed class Particle
		{
			public VisualElement Element;
			public Vector2 Position;
			public Vector2 Velocity;
			public float Size;
			public float Age;
			public float Life;
			public float Phase;
		}

		/// <summary>대상 테두리를 따라 반짝이 count 개. count 0 이하면 설정값</summary>
		public static void Burst(VisualElement target, int count = 0)
		{
			UIFxSettings settings = UIFxSettings.Instance;
			Rect area = target.contentRect;
			if (area.width <= 0f || area.height <= 0f || float.IsNaN(area.width))
			{
				// 막 켜진 창은 레이아웃 전. 크기가 잡히는 프레임까지 미룸
				WhenLaidOut(target, () => Burst(target, count));
				return;
			}
			int total = count > 0 ? count : settings.sparkleCount;

			VisualElement layer = new() { name = "sparkle-burst", pickingMode = PickingMode.Ignore };
			layer.style.position = Position.Absolute;
			layer.style.left = 0;
			layer.style.top = 0;
			layer.style.right = 0;
			layer.style.bottom = 0;
			target.Add(layer);

			List<Particle> particles = new();
			for (int i = 0; i < total; i++)
			{
				Vector2 edge = PointOnEdge(area, (i + Random.value * 0.8f) / total);
				Vector2 outward = (edge - area.center).normalized;
				float size = Random.Range(settings.sparkleSizeRange.x, settings.sparkleSizeRange.y);
				Particle particle = new()
				{
					Position = edge,
					Velocity = outward * size * settings.sparklePopOut + Vector2.down * settings.sparkleRiseSpeed,
					Size = size,
					Life = settings.sparkleLifeSeconds * Random.Range(0.7f, 1.15f),
					Phase = Random.value,
					Element = MakeDot(UIFxTextures.Sparkle, settings.sparkleColor, size),
				};
				layer.Add(particle.Element);
				particles.Add(particle);
			}

			bool done = false;
			layer.schedule.Execute(timer =>
			{
				float deltaSeconds = timer.deltaTime / MILLISECONDS_PER_SECOND;
				int alive = 0;
				foreach (Particle particle in particles)
				{
					particle.Age += deltaSeconds;
					float lifeRatio = particle.Age / particle.Life;
					if (lifeRatio >= 1f)
					{
						particle.Element.style.display = DisplayStyle.None;
						continue;
					}
					alive++;
					particle.Position += particle.Velocity * deltaSeconds;
					particle.Velocity *= 1f - Mathf.Clamp01(deltaSeconds * 3f);
					float twinkle = 0.6f + 0.4f * Mathf.Sin((lifeRatio * TWINKLE_CYCLES + particle.Phase) * Mathf.PI * 2f);
					float grow = lifeRatio < 0.2f ? lifeRatio / 0.2f : 1f - (lifeRatio - 0.2f) / 0.8f;
					Place(particle.Element, particle.Position, particle.Size * Mathf.Max(0f, grow) * (0.7f + 0.3f * twinkle));
					particle.Element.style.opacity = Mathf.Clamp01(grow * twinkle * 1.4f);
				}
				if (alive > 0)
					return;
				layer.RemoveFromHierarchy();
				done = true;
			}).Every(0).Until(() => done);
		}

		/// <summary>떠다니는 빛 입자 층. 대상의 맨 앞 자식 (입력 무시), 대상이 숨으면 멈춤</summary>
		public static VisualElement AttachMotes(VisualElement target)
		{
			UIFxSettings settings = UIFxSettings.Instance;
			VisualElement layer = new() { name = "ambient-motes", pickingMode = PickingMode.Ignore };
			layer.style.position = Position.Absolute;
			layer.style.left = 0;
			layer.style.top = 0;
			layer.style.right = 0;
			layer.style.bottom = 0;
			target.Add(layer);

			List<Particle> motes = new();
			for (int i = 0; i < settings.moteCount; i++)
			{
				float size = Random.Range(settings.moteSizeRange.x, settings.moteSizeRange.y);
				Particle mote = new()
				{
					Size = size,
					Phase = Random.value,
					Life = Random.Range(4f, 8f),
					Age = Random.Range(0f, 8f),
					Element = MakeDot(UIFxTextures.Glow, settings.moteColor, size),
				};
				layer.Add(mote.Element);
				motes.Add(mote);
			}

			layer.schedule.Execute(timer =>
			{
				Rect area = layer.contentRect;
				if (area.width <= 0f || target.resolvedStyle.display == DisplayStyle.None)
					return;
				float deltaSeconds = Mathf.Min(timer.deltaTime / MILLISECONDS_PER_SECOND, 0.1f);
				foreach (Particle mote in motes)
				{
					mote.Age += deltaSeconds;
					if (mote.Age >= mote.Life || mote.Position == Vector2.zero)
					{
						mote.Age = mote.Position == Vector2.zero ? Random.Range(0f, mote.Life) : 0f;
						mote.Position = new Vector2(Random.Range(area.xMin, area.xMax), Random.Range(area.yMin, area.yMax));
					}
					float lifeRatio = mote.Age / mote.Life;
					float sway = Mathf.Sin((lifeRatio * 2f + mote.Phase) * Mathf.PI * 2f) * 6f;
					Vector2 drift = new(sway, -settings.moteRiseSpeed * mote.Age);
					Place(mote.Element, mote.Position + drift, mote.Size);
					float fade = Mathf.Sin(lifeRatio * Mathf.PI);
					mote.Element.style.opacity = fade * settings.moteAlpha;
				}
			}).Every(0);
			return layer;
		}

		private const int LAYOUT_WAIT_FRAMES = 10;

		private static void WhenLaidOut(VisualElement target, System.Action action)
		{
			int waited = 0;
			bool done = false;
			target.schedule.Execute(() =>
			{
				waited++;
				Rect area = target.contentRect;
				bool ready = area.width > 0f && area.height > 0f && float.IsNaN(area.width) == false;
				if (ready)
					action();
				done = ready || waited >= LAYOUT_WAIT_FRAMES;
			}).Every(0).Until(() => done);
		}

		private static VisualElement MakeDot(Texture2D texture, Color color, float size)
		{
			VisualElement dot = new() { pickingMode = PickingMode.Ignore };
			dot.style.position = Position.Absolute;
			dot.style.backgroundImage = new StyleBackground(texture);
			dot.style.unityBackgroundImageTintColor = color;
			dot.style.width = size;
			dot.style.height = size;
			dot.style.opacity = 0f;
			return dot;
		}

		private static void Place(VisualElement dot, Vector2 center, float size)
		{
			dot.style.width = size;
			dot.style.height = size;
			dot.style.left = center.x - size * 0.5f;
			dot.style.top = center.y - size * 0.5f;
		}

		// 둘레 비율 0~1 을 사각형 테두리 위 점으로
		private static Vector2 PointOnEdge(Rect area, float ratio)
		{
			float perimeter = (area.width + area.height) * 2f;
			float distance = Mathf.Repeat(ratio, 1f) * perimeter;
			if (distance < area.width)
				return new Vector2(area.xMin + distance, area.yMin);
			distance -= area.width;
			if (distance < area.height)
				return new Vector2(area.xMax, area.yMin + distance);
			distance -= area.height;
			if (distance < area.width)
				return new Vector2(area.xMax - distance, area.yMax);
			distance -= area.width;
			return new Vector2(area.xMin, area.yMax - distance);
		}
	}
}

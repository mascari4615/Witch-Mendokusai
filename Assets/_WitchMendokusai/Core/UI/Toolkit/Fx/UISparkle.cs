using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// UI 위 입자. 창 열기 반짝이 (Burst) 와 패널 뒤 떠다니는 입자 (AttachMotes)
	/// 입자는 대상의 자식으로 붙고 끝나면 스스로 떨어짐. 모양, 움직임, 수치는 UIFxSettings
	/// </summary>
	public static class UISparkle
	{
		private const float MILLISECONDS_PER_SECOND = 1000f;
		// 깜박임 횟수 (수명 동안)
		private const float TWINKLE_CYCLES = 2.5f;
		// 멈칫 뒤 한 프레임 이동 상한
		private const float MAX_STEP_SECONDS = 0.1f;
		private const int LAYOUT_WAIT_FRAMES = 10;

		private sealed class Particle
		{
			public VisualElement Element;
			public Vector2 Position;
			public Vector2 Velocity;
			public float Size;
			public float Age;
			public float Life;
			public float Phase;
			public float Rotation;
			public float Spin;
			// Orbit 의 둘레 위치 0~1
			public float EdgeRatio;
			// Fountain 의 중력 가속도
			public float Gravity;
		}

		/// <summary>대상 테두리에서 반짝이 count 개. count 0 이하면 설정값. 모양과 움직임은 sparkleShape, sparkleMotion</summary>
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
			BurstMotion motion = settings.sparkleMotion;
			ParticleShape shape = settings.sparkleShape;
			bool spins = UIFxTextures.IsRound(shape) == false;

			VisualElement layer = MakeLayer("sparkle-burst");
			target.Add(layer);

			List<Particle> particles = new();
			for (int i = 0; i < total; i++)
			{
				float size = Random.Range(settings.sparkleSizeRange.x, settings.sparkleSizeRange.y);
				Particle particle = new()
				{
					Size = size,
					Life = settings.sparkleLifeSeconds * Random.Range(0.7f, 1.15f),
					Phase = Random.value,
					EdgeRatio = (i + Random.value * 0.8f) / total,
					Rotation = spins ? Random.Range(0f, 360f) : 0f,
					Spin = spins ? Random.Range(-1f, 1f) * settings.particleSpinSpeed : 0f,
					Element = MakeDot(UIFxTextures.Shape(shape), settings.sparkleColor, size),
				};
				SpawnBurst(particle, motion, area, settings);
				layer.Add(particle.Element);
				particles.Add(particle);
			}

			bool done = false;
			layer.schedule.Execute(timer =>
			{
				float deltaSeconds = Mathf.Min(timer.deltaTime / MILLISECONDS_PER_SECOND, MAX_STEP_SECONDS);
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
					MoveBurst(particle, motion, area, settings, deltaSeconds);
					float twinkle = 0.6f + 0.4f * Mathf.Sin((lifeRatio * TWINKLE_CYCLES + particle.Phase) * Mathf.PI * 2f);
					float grow = lifeRatio < 0.2f ? lifeRatio / 0.2f : 1f - (lifeRatio - 0.2f) / 0.8f;
					Place(particle.Element, particle.Position, particle.Size * Mathf.Max(0f, grow) * (0.7f + 0.3f * twinkle));
					Turn(particle, deltaSeconds);
					particle.Element.style.opacity = Mathf.Clamp01(grow * twinkle * 1.4f);
				}
				if (alive > 0)
					return;
				layer.RemoveFromHierarchy();
				done = true;
			}).Every(0).Until(() => done);
		}

		private static void SpawnBurst(Particle particle, BurstMotion motion, Rect area, UIFxSettings settings)
		{
			switch (motion)
			{
				case BurstMotion.PopRise:
				{
					Vector2 edge = PointOnEdge(area, particle.EdgeRatio);
					Vector2 outward = (edge - area.center).normalized;
					particle.Position = edge;
					particle.Velocity = outward * particle.Size * settings.sparklePopOut + Vector2.down * settings.sparkleRiseSpeed;
					break;
				}
				case BurstMotion.Fountain:
				{
					// 아래 변에서 솟아 포물선. 정점이 수명 60% 지점, 높이는 창 높이 비율
					float peakTime = particle.Life * 0.6f;
					float height = area.height * settings.sparkleFountainHeight * Random.Range(0.7f, 1.2f);
					particle.Position = new Vector2(Random.Range(area.xMin, area.xMax), area.yMax);
					particle.Velocity = new Vector2(Random.Range(-0.25f, 0.25f) * area.width, -2f * height / peakTime);
					particle.Gravity = 2f * height / (peakTime * peakTime);
					break;
				}
				case BurstMotion.Orbit:
					particle.Position = PointOnEdge(area, particle.EdgeRatio);
					particle.Life *= 1.4f;
					break;
				case BurstMotion.Fall:
					particle.Position = new Vector2(Random.Range(area.xMin, area.xMax), area.yMin + Random.Range(-0.1f, 0.3f) * area.height);
					particle.Velocity = new Vector2(0f, settings.sparkleFallSpeed * Random.Range(0.7f, 1.3f));
					particle.Life *= 1.6f;
					break;
			}
		}

		private static void MoveBurst(Particle particle, BurstMotion motion, Rect area, UIFxSettings settings, float deltaSeconds)
		{
			switch (motion)
			{
				case BurstMotion.PopRise:
					particle.Position += particle.Velocity * deltaSeconds;
					particle.Velocity *= 1f - Mathf.Clamp01(deltaSeconds * 3f);
					break;
				case BurstMotion.Fountain:
					particle.Velocity += Vector2.up * particle.Gravity * deltaSeconds;
					particle.Position += particle.Velocity * deltaSeconds;
					break;
				case BurstMotion.Orbit:
					particle.EdgeRatio += settings.sparkleOrbitSpeed * deltaSeconds;
					particle.Position = PointOnEdge(area, particle.EdgeRatio);
					break;
				case BurstMotion.Fall:
				{
					float sway = Mathf.Sin((particle.Age * 1.6f + particle.Phase) * Mathf.PI * 2f) * particle.Size * 1.2f;
					particle.Position += new Vector2(sway, particle.Velocity.y) * deltaSeconds;
					break;
				}
			}
		}

		/// <summary>떠다니는 입자 층. 대상의 맨 앞 자식 (입력 무시), 대상이 숨으면 멈춤. 모양과 움직임은 moteShape, moteMotion</summary>
		public static VisualElement AttachMotes(VisualElement target)
		{
			UIFxSettings settings = UIFxSettings.Instance;
			VisualElement layer = MakeLayer("ambient-motes");
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

			ParticleShape appliedShape = ParticleShape.Glow;
			layer.schedule.Execute(timer =>
			{
				Rect area = layer.contentRect;
				if (area.width <= 0f || target.resolvedStyle.display == DisplayStyle.None)
					return;
				// 설정을 바꾸면 다음 프레임부터 새 모양
				if (settings.moteShape != appliedShape)
				{
					appliedShape = settings.moteShape;
					bool spins = UIFxTextures.IsRound(appliedShape) == false;
					foreach (Particle mote in motes)
					{
						mote.Element.style.backgroundImage = new StyleBackground(UIFxTextures.Shape(appliedShape));
						mote.Spin = spins ? Random.Range(-0.5f, 0.5f) * settings.particleSpinSpeed : 0f;
						mote.Rotation = 0f;
						mote.Element.style.rotate = StyleKeyword.Null;
					}
				}
				float deltaSeconds = Mathf.Min(timer.deltaTime / MILLISECONDS_PER_SECOND, MAX_STEP_SECONDS);
				MoteMotion motion = settings.moteMotion;
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
					float travel = settings.moteRiseSpeed * mote.Age;
					Vector2 drift = motion switch
					{
						MoteMotion.Fall => new Vector2(sway * 2f, travel),
						MoteMotion.Drift => new Vector2(travel, sway),
						MoteMotion.Twinkle => Vector2.zero,
						_ => new Vector2(sway, -travel),
					};
					Place(mote.Element, mote.Position + drift, mote.Size);
					Turn(mote, deltaSeconds);
					float fade = Mathf.Sin(lifeRatio * Mathf.PI);
					if (motion == MoteMotion.Twinkle)
						fade *= Mathf.Max(0f, Mathf.Sin((mote.Age * 1.5f + mote.Phase) * Mathf.PI * 2f));
					mote.Element.style.opacity = fade * settings.moteAlpha;
				}
			}).Every(0);
			return layer;
		}

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

		private static VisualElement MakeLayer(string layerName)
		{
			VisualElement layer = new() { name = layerName, pickingMode = PickingMode.Ignore };
			layer.style.position = Position.Absolute;
			layer.style.left = 0;
			layer.style.top = 0;
			layer.style.right = 0;
			layer.style.bottom = 0;
			return layer;
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

		private static void Turn(Particle particle, float deltaSeconds)
		{
			if (particle.Spin == 0f && particle.Rotation == 0f)
				return;
			particle.Rotation += particle.Spin * deltaSeconds;
			particle.Element.style.rotate = new UnityEngine.UIElements.Rotate(new UnityEngine.UIElements.Angle(particle.Rotation, AngleUnit.Degree));
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

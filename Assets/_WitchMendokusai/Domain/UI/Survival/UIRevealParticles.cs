using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	public enum RevealParticleKind
	{
		Dot = 0,
		Spark = 1,
		Coin = 2,
		Twinkle = 3
	}

	// 공개 연출 입자. 불꽃 줄, 부드러운 점, 동전, 반짝임. 시간 정지 중에도 실제 시간
	[RequireComponent(typeof(CanvasRenderer))]
	public sealed class UIRevealParticles : MaskableGraphic
	{
		public struct Particle
		{
			public Vector2 Position;
			public Vector2 Velocity;
			public float Gravity;
			public float Drag;
			public float Age;
			public float Life;
			public float Size;
			public Color Tint;
			public RevealParticleKind Kind;
			public float Spin;
		}

		// 불꽃 줄 길이. 속도 비례, 상한
		[SerializeField, Min(0f)] private float sparkLengthPerSpeed = 0.035f;
		private const float SPARK_MAX_LENGTH = 54f;
		private const float SPARK_WIDTH_RATIO = 0.5f;
		[SerializeField, Min(0f)] private float coinSpinSpeed = 9f;
		private const float FADE_IN_RATIO = 0.1f;

		private static readonly Color COIN_FRONT = new(1f, 0.82f, 0.29f, 1f);
		private static readonly Color COIN_BACK = new(0.79f, 0.54f, 0.07f, 1f);

		private readonly List<Particle> particles = new();

		public override Texture mainTexture => RevealTextures.SoftDot.texture;

		public static UIRevealParticles Create(Transform parent, string objectName)
		{
			GameObject particleObject = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(UIRevealParticles));
			RectTransform rect = (RectTransform)particleObject.transform;
			rect.SetParent(parent, false);
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = rect.offsetMax = Vector2.zero;
			UIRevealParticles result = particleObject.GetComponent<UIRevealParticles>();
			result.raycastTarget = false;
			return result;
		}

		public void Add(Particle particle)
		{
			if (UIEffects.Enabled == false)
				return;
			particles.Add(particle);
			SetVerticesDirty();
		}

		// 한 점에서 퍼지는 터짐. spread 는 라디안, angle 은 가운데 방향
		public void Burst(Vector2 position, int count, Color tint, float speed, float gravity, float life, float size, float angle = 0f, float spread = Mathf.PI * 2f, float drag = 2f, float sparkRatio = 0.55f)
		{
			for (int i = 0; i < count; i++)
			{
				float radians = angle + (Random.value - 0.5f) * spread;
				float particleSpeed = speed * Random.Range(0.35f, 1.1f);
				Add(new Particle
				{
					Position = position,
					Velocity = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * particleSpeed,
					Gravity = gravity,
					Drag = drag,
					Life = life * Random.Range(0.6f, 1.2f),
					Size = size * Random.Range(0.5f, 1.5f),
					Tint = tint,
					Kind = Random.value < sparkRatio ? RevealParticleKind.Spark : RevealParticleKind.Dot,
				});
			}
		}

		public void Clear()
		{
			particles.Clear();
			SetVerticesDirty();
		}

		protected override void OnDisable()
		{
			particles.Clear();
			base.OnDisable();
		}

		private void Update()
		{
			if (particles.Count == 0)
				return;
			float deltaTime = Time.unscaledDeltaTime;
			for (int i = particles.Count - 1; i >= 0; i--)
			{
				Particle particle = particles[i];
				particle.Age += deltaTime;
				if (particle.Age >= particle.Life)
				{
					particles.RemoveAt(i);
					continue;
				}
				particle.Velocity *= Mathf.Max(0f, 1f - particle.Drag * deltaTime);
				// UI 좌표는 위가 +, 중력은 아래로
				particle.Velocity.y -= particle.Gravity * deltaTime;
				particle.Position += particle.Velocity * deltaTime;
				particle.Spin += deltaTime * coinSpinSpeed;
				particles[i] = particle;
			}
			SetVerticesDirty();
		}

		protected override void OnPopulateMesh(VertexHelper vertexHelper)
		{
			vertexHelper.Clear();
			Vector2 center = GetPixelAdjustedRect().center;
			foreach (Particle particle in particles)
			{
				float progress = particle.Age / particle.Life;
				float fade = progress < FADE_IN_RATIO ? progress / FADE_IN_RATIO : 1f - Mathf.Pow((progress - FADE_IN_RATIO) / (1f - FADE_IN_RATIO), 1.6f);
				Vector2 middle = center + particle.Position;
				switch (particle.Kind)
				{
					case RevealParticleKind.Spark:
					{
						float speed = particle.Velocity.magnitude;
						Vector2 direction = speed > 0.01f ? particle.Velocity / speed : Vector2.up;
						float length = Mathf.Min(SPARK_MAX_LENGTH, particle.Size + speed * sparkLengthPerSpeed);
						AddQuad(vertexHelper, middle - direction * length * 0.5f, direction, length, particle.Size * SPARK_WIDTH_RATIO, WithAlpha(particle.Tint, fade));
						break;
					}
					case RevealParticleKind.Coin:
					{
						float face = Mathf.Cos(particle.Spin);
						Color coin = face > 0f ? COIN_FRONT : COIN_BACK;
						AddQuad(vertexHelper, middle, Vector2.right, Mathf.Max(1f, Mathf.Abs(face) * particle.Size * 2f), particle.Size * 2f, WithAlpha(coin, fade));
						break;
					}
					case RevealParticleKind.Twinkle:
					{
						float pulse = particle.Size * (0.6f + 0.4f * Mathf.Sin(particle.Spin * 2f));
						Color tint = WithAlpha(particle.Tint, fade);
						AddQuad(vertexHelper, middle, Vector2.right, pulse * 6f, pulse * 0.6f, tint);
						AddQuad(vertexHelper, middle, Vector2.up, pulse * 6f, pulse * 0.6f, tint);
						AddQuad(vertexHelper, middle, Vector2.right, pulse * 2.4f, pulse * 2.4f, tint);
						break;
					}
					default:
						AddQuad(vertexHelper, middle, Vector2.right, particle.Size * 2f, particle.Size * 2f, WithAlpha(particle.Tint, fade));
						break;
				}
			}
		}

		private static Color WithAlpha(Color tint, float alpha) => new(tint.r, tint.g, tint.b, tint.a * Mathf.Clamp01(alpha));

		// 가운데 middle, 긴 축 axis 방향의 사각형
		private static void AddQuad(VertexHelper vertexHelper, Vector2 middle, Vector2 axis, float length, float width, Color tint)
		{
			Vector2 along = axis * (length * 0.5f);
			Vector2 across = new Vector2(-axis.y, axis.x) * (width * 0.5f);
			int baseIndex = vertexHelper.currentVertCount;
			Color32 vertexColor = tint;
			vertexHelper.AddVert(middle - along - across, vertexColor, new Vector2(0f, 0f));
			vertexHelper.AddVert(middle - along + across, vertexColor, new Vector2(0f, 1f));
			vertexHelper.AddVert(middle + along + across, vertexColor, new Vector2(1f, 1f));
			vertexHelper.AddVert(middle + along - across, vertexColor, new Vector2(1f, 0f));
			vertexHelper.AddTriangle(baseIndex, baseIndex + 1, baseIndex + 2);
			vertexHelper.AddTriangle(baseIndex, baseIndex + 2, baseIndex + 3);
		}
	}
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	// 선택 화면 입자. 시간 정지 중에도 실제 시간으로 진행
	[RequireComponent(typeof(CanvasRenderer))]
	public class UIBurst : MaskableGraphic
	{
		private struct Particle
		{
			public Vector2 Position;
			public Vector2 Velocity;
			public float Age;
			public float Life;
			public float Size;
		}

		[SerializeField] private Sprite particleSprite;
		[SerializeField] private int defaultCount = 24;
		[SerializeField] private float minSpeed = 180f;
		[SerializeField] private float maxSpeed = 620f;
		[SerializeField] private float minLife = 0.35f;
		[SerializeField] private float maxLife = 0.8f;
		[SerializeField] private float minSize = 6f;
		[SerializeField] private float maxSize = 22f;
		[SerializeField] private float drag = 2.4f;

		private readonly List<Particle> particles = new();

		public override Texture mainTexture => particleSprite != null ? particleSprite.texture : base.mainTexture;

		public void Play(Color burstColor, int count = -1)
		{
			if (UIEffects.Enabled == false)
				return;
			color = burstColor;
			int spawnCount = count < 0 ? defaultCount : count;
			for (int i = 0; i < spawnCount; i++)
			{
				float radians = Random.value * Mathf.PI * 2f;
				particles.Add(new Particle
				{
					Position = Vector2.zero,
					Velocity = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * Random.Range(minSpeed, maxSpeed),
					Age = 0f,
					Life = Random.Range(minLife, maxLife),
					Size = Random.Range(minSize, maxSize)
				});
			}
			SetVerticesDirty();
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
				particle.Position += particle.Velocity * deltaTime;
				particle.Velocity *= Mathf.Max(0f, 1f - drag * deltaTime);
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
				float remain = 1f - particle.Age / particle.Life;
				float half = particle.Size * remain * 0.5f;
				Color32 vertexColor = new Color(color.r, color.g, color.b, color.a * remain);
				Vector2 middle = center + particle.Position;
				int baseIndex = vertexHelper.currentVertCount;
				vertexHelper.AddVert(middle + new Vector2(-half, -half), vertexColor, new Vector2(0f, 0f));
				vertexHelper.AddVert(middle + new Vector2(-half, half), vertexColor, new Vector2(0f, 1f));
				vertexHelper.AddVert(middle + new Vector2(half, half), vertexColor, new Vector2(1f, 1f));
				vertexHelper.AddVert(middle + new Vector2(half, -half), vertexColor, new Vector2(1f, 0f));
				vertexHelper.AddTriangle(baseIndex, baseIndex + 1, baseIndex + 2);
				vertexHelper.AddTriangle(baseIndex, baseIndex + 2, baseIndex + 3);
			}
		}
	}
}

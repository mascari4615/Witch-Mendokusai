using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 그린 텍스처 폭발을 한 벌 (PaintedFxSet) 마다 모아 그림. 풀 오브젝트마다 재질을 만들지 않게. 씬마다 처음 쓸 때 만들어짐
	public sealed class PaintedBurstLayer : MonoBehaviour
	{
		public struct Burst
		{
			public Vector3 Position;
			public float Radius;
			public float Seconds;
			public float RingSeconds;
			public Color Tint;
			public Color RingTint;
			public Color Shade;
			public float Cover;
			public float StartTime;
			public float Seed;
		}

		private const int FLIPBOOK_SIDE = 4;
		private static readonly Dictionary<PaintedFxSet, PaintedBurstLayer> layers = new();

		private readonly List<Burst> bursts = new();
		private readonly List<FxQuadBatch> batches = new();
		private FxQuadBatch shadeBatch;
		private FxQuadBatch ringBatch;
		private FxQuadBatch burstBatch;

		public static void Play(PaintedFxSet art, Burst burst)
		{
			if (layers.TryGetValue(art, out PaintedBurstLayer layer) == false || layer == null)
			{
				layer = new GameObject($"PaintedBurst_{art.name}").AddComponent<PaintedBurstLayer>();
				layer.Prepare(art);
				layers[art] = layer;
			}
			burst.StartTime = Time.time;
			burst.Seed = Random.value;
			layer.bursts.Add(burst);
		}

		private void Prepare(PaintedFxSet art)
		{
			shadeBatch = Batch("Shade", art, art.Soft, 2991);
			ringBatch = Batch("Ring", art, art.Shock, 2995);
			burstBatch = Batch("Burst", art, art.Impact, 3125, true);
		}

		private FxQuadBatch Batch(string label, PaintedFxSet art, Texture texture, int queue, bool onTop = false)
		{
			FxQuadBatch batch = new($"PaintedBurst{label}", transform, art.Shader, texture, art.Noise, queue, onTop);
			batches.Add(batch);
			return batch;
		}

		private void OnDestroy()
		{
			foreach (FxQuadBatch batch in batches)
				batch.Destroy();
		}

		private void LateUpdate()
		{
			Camera viewCamera = Camera.main;
			if (viewCamera == null)
				return;
			foreach (FxQuadBatch batch in batches)
				batch.Begin();
			for (int i = bursts.Count - 1; i >= 0; i--)
			{
				if (Draw(bursts[i], viewCamera.transform) == false)
					bursts.RemoveAt(i);
			}
			foreach (FxQuadBatch batch in batches)
				batch.Commit();
		}

		private bool Draw(Burst burst, Transform view)
		{
			float age = (Time.time - burst.StartTime) / burst.Seconds;
			if (age >= 1f)
				return false;
			Vector3 ground = burst.Position + Vector3.up * 0.05f;
			float shade = burst.Radius * 1.15f;
			shadeBatch.Add(ground, Vector3.right * shade, Vector3.forward * shade, FxQuadLook.Of(Faded(burst.Shade, 1f - age), 1f));
			float ring = (Time.time - burst.StartTime) / burst.RingSeconds;
			if (ring < 1f)
			{
				float size = burst.Radius * (1f - (1f - ring) * (1f - ring));
				ringBatch.Add(ground + Vector3.up * 0.04f, Vector3.right * size, Vector3.forward * size, FxQuadLook.Of(burst.RingTint, burst.Cover * 0.6f, Mathf.Clamp01(ring * 1.5f - 0.5f), burst.Seed));
			}
			Rect cell = FxQuadBatch.Cell(Mathf.Min(FLIPBOOK_SIDE * FLIPBOOK_SIDE - 1, (int)(age * FLIPBOOK_SIDE * FLIPBOOK_SIDE)), FLIPBOOK_SIDE, FLIPBOOK_SIDE);
			FxQuadLook look = FxQuadLook.Of(burst.Tint, burst.Cover);
			look.Turn = burst.Seed * Mathf.PI * 2f;
			burstBatch.Add(burst.Position + Vector3.up * burst.Radius * 0.3f, view.right * burst.Radius, view.up * burst.Radius, cell, look);
			return true;
		}

		private static Color Faded(Color color, float alpha) => new(color.r * alpha, color.g * alpha, color.b * alpha, color.a * alpha);
	}
}

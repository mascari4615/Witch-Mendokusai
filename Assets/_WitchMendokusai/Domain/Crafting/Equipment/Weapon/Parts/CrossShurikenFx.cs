using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	// 알리사 십자 수리검의 그림. 커지는 동안 모이는 빛, 떨어지는 줄기, 박히는 순간의 폭발과 바닥 고리와 사방 번개, 남는 전기장
	// - 판정 없음. CrossShurikenThrower 가 같은 자리와 반지름으로 부름
	// - 그린 텍스처 (PaintedFxSet) 를 겹겹이. 만든 물체는 이 오브젝트의 자식 (풀과 같이 씬을 넘어 살아남음). LateUpdate 에서만 그림
	public sealed class CrossShurikenFx : MonoBehaviour
	{
		private const int FLIPBOOK_SIDE = 4;

		[SerializeField] private PaintedFxSet art;

		[Header("색")]
		[SerializeField, ColorUsage(false, true)] private Color lightTint = new(0.8f, 0.86f, 1f);
		[SerializeField, ColorUsage(false, true)] private Color hotTint = new(1.1f, 1.2f, 1.35f);
		[SerializeField] private Color boltTint = new(0.55f, 0.85f, 1f, 1f);
		[SerializeField] private Color shadeTint = new(0.01f, 0.02f, 0.06f, 1f);
		[SerializeField] private Color lampColor = new(0.45f, 0.75f, 1f);

		[Header("모음과 떨어짐")]
		[SerializeField, Min(0f)] private float gatherSize = 2.2f;
		[SerializeField, Min(0)] private int gatherStreaks = 7;
		[SerializeField, Min(0f)] private float trailWidth = 1.6f;

		[Header("폭발 (반지름 배율)")]
		[SerializeField, Min(0.02f)] private float burstSeconds = 0.42f;
		[SerializeField, Min(0f)] private float burstScale = 1.35f;
		[SerializeField, Min(0.02f)] private float ringSeconds = 0.33f;
		[SerializeField, Min(0.02f)] private float scorchSeconds = 0.9f;
		[SerializeField, Min(0)] private int boltCount = 9;
		[SerializeField, Min(0.02f)] private float boltSeconds = 0.28f;
		[SerializeField, Min(0f)] private float boltReach = 1.25f;
		[SerializeField, Min(0f)] private float boltWidth = 0.5f;
		[SerializeField, Min(0f)] private float lampIntensity = 14f;

		[Header("전기장")]
		[SerializeField, Min(0.05f)] private float fieldPulseSeconds = 0.5f;
		[SerializeField, Min(0)] private int fieldBolts = 5;

		private struct Blast
		{
			public Vector3 Position;
			public float Radius;
			public float StartTime;
			public float Seed;
		}

		private struct Field
		{
			public Vector3 Position;
			public float Radius;
			public float StartTime;
			public float Seconds;
			public float Seed;
		}

		private readonly List<Blast> blasts = new();
		private readonly List<Field> fields = new();
		private readonly List<FxQuadBatch> batches = new();
		private Transform root;
		private Transform body;
		private float gather;
		private Vector3 fallFrom;
		private float fallUntil;
		private FxQuadBatch shadeBatch;
		private FxQuadBatch scorchBatch;
		private FxQuadBatch ringBatch;
		private FxQuadBatch boltBatch;
		private FxQuadBatch glowBatch;
		private FxQuadBatch burstBatch;
		private Light lamp;

		// 머리 위에서 커지는 동안. amount 0 에서 1
		public void SetGather(Transform shuriken, float amount)
		{
			Prepare();
			body = shuriken;
			gather = amount;
		}

		public void PlayFall(Vector3 from, float seconds)
		{
			fallFrom = from;
			fallUntil = Time.time + seconds;
			gather = 0f;
		}

		public void PlayBlast(Vector3 position, float radius)
		{
			Prepare();
			blasts.Add(new Blast { Position = position, Radius = radius, StartTime = Time.time, Seed = Random.value });
		}

		public void PlayField(Vector3 position, float radius, float seconds)
		{
			Prepare();
			fields.Add(new Field { Position = position, Radius = radius, StartTime = Time.time, Seconds = seconds, Seed = Random.value });
		}

		private void OnDisable()
		{
			blasts.Clear();
			fields.Clear();
			gather = 0f;
			fallUntil = 0f;
			if (lamp != null)
				lamp.enabled = false;
			foreach (FxQuadBatch batch in batches)
				batch.Clear();
		}

		private void OnDestroy()
		{
			foreach (FxQuadBatch batch in batches)
				batch.Destroy();
		}

		private void LateUpdate()
		{
			if (root == null)
				return;
			Camera viewCamera = Camera.main;
			root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
			root.gameObject.SetActive(viewCamera != null);
			if (viewCamera == null)
				return;
			foreach (FxQuadBatch batch in batches)
				batch.Begin();
			Transform view = viewCamera.transform;
			lamp.enabled = false;
			DrawGather(view);
			for (int i = fields.Count - 1; i >= 0; i--)
			{
				if (DrawField(fields[i], view) == false)
					fields.RemoveAt(i);
			}
			for (int i = blasts.Count - 1; i >= 0; i--)
			{
				if (DrawBlast(blasts[i], view) == false)
					blasts.RemoveAt(i);
			}
			foreach (FxQuadBatch batch in batches)
				batch.Commit();
		}

		private void DrawGather(Transform view)
		{
			if (body == null || body.gameObject.activeInHierarchy == false)
				return;
			Vector3 center = body.position;
			if (Time.time < fallUntil)
			{
				boltBatch.AddStrip(fallFrom, center, trailWidth, view.forward, PaintedFxSet.StreakRow(2), FxQuadLook.Of(boltTint, 0.3f));
				boltBatch.AddStrip(Vector3.Lerp(fallFrom, center, 0.35f), center, trailWidth * 0.5f, view.forward, PaintedFxSet.StreakRow(0), FxQuadLook.Of(hotTint));
			}
			if (gather <= 0f)
				return;
			float size = gatherSize * Mathf.Lerp(0.5f, 1f, gather);
			glowBatch.Add(center, view.right * size, view.up * size, FxQuadLook.Of(Alpha(lightTint, gather * 0.7f)));
			for (int i = 0; i < gatherStreaks; i++)
			{
				float cycle = Mathf.Repeat(gather * 1.6f + i * 0.618f, 1f);
				float angle = i * 2.399f;
				Vector3 outward = view.right * Mathf.Cos(angle) + view.up * Mathf.Sin(angle);
				float far = size * Mathf.Lerp(1.5f, 0.35f, cycle);
				boltBatch.AddStrip(center + outward * (far + size * 0.5f), center + outward * far, 0.35f, view.forward, PaintedFxSet.StreakRow(0), FxQuadLook.Of(Alpha(boltTint, gather * Mathf.Sin(cycle * Mathf.PI)), 0.2f));
			}
		}

		private bool DrawBlast(Blast blast, Transform view)
		{
			float age = Time.time - blast.StartTime;
			if (age >= Mathf.Max(burstSeconds, scorchSeconds))
				return false;
			Vector3 ground = blast.Position + Vector3.up * 0.04f;
			float scorch = age / scorchSeconds;
			if (scorch < 1f)
			{
				shadeBatch.Add(ground, Vector3.right * blast.Radius * 1.3f, Vector3.forward * blast.Radius * 1.3f, FxQuadLook.Of(Alpha(shadeTint, 0.55f * (1f - scorch)), 1f));
				scorchBatch.Add(ground + Vector3.up * 0.02f, Vector3.right * blast.Radius, Vector3.forward * blast.Radius, FxQuadLook.Of(lightTint, 0.2f, scorch, blast.Seed));
			}
			float ring = age / ringSeconds;
			if (ring < 1f)
			{
				float size = blast.Radius * (1f - (1f - ring) * (1f - ring)) * 1.08f;
				ringBatch.Add(ground + Vector3.up * 0.05f, Vector3.right * size, Vector3.forward * size, FxQuadLook.Of(hotTint, 0.25f, Mathf.Clamp01(ring * 1.4f - 0.4f), blast.Seed));
			}
			float burst = age / burstSeconds;
			if (burst < 1f)
			{
				float size = blast.Radius * burstScale;
				Rect cell = FxQuadBatch.Cell(Mathf.Min(FLIPBOOK_SIDE * FLIPBOOK_SIDE - 1, (int)(burst * FLIPBOOK_SIDE * FLIPBOOK_SIDE)), FLIPBOOK_SIDE, FLIPBOOK_SIDE);
				burstBatch.Add(blast.Position + Vector3.up * blast.Radius * 0.35f, view.right * size, view.up * size, cell, FxQuadLook.Of(lightTint, 0.35f));
				float flash = Mathf.Pow(0.5f, age / 0.06f);
				glowBatch.Add(blast.Position + Vector3.up * 0.6f, view.right * size * 1.3f, view.up * size * 1.3f, FxQuadLook.Of(Alpha(hotTint, flash)));
				Illuminate(blast.Position, blast.Radius, 1f - burst);
			}
			if (age < boltSeconds)
				DrawBolts(blast.Position + Vector3.up * 0.5f, blast.Radius * 0.15f, blast.Radius * boltReach, boltCount, blast.Seed, 1f - age / boltSeconds, view);
			return true;
		}

		private bool DrawField(Field field, Transform view)
		{
			float age = Time.time - field.StartTime;
			if (age >= field.Seconds)
				return false;
			float fade = Mathf.Clamp01((field.Seconds - age) / 0.25f) * Mathf.Clamp01(age / 0.1f);
			Vector3 ground = field.Position + Vector3.up * 0.05f;
			shadeBatch.Add(ground, Vector3.right * field.Radius * 1.1f, Vector3.forward * field.Radius * 1.1f, FxQuadLook.Of(Alpha(shadeTint, 0.45f * fade), 1f));
			FxQuadLook edge = FxQuadLook.Of(Alpha(lightTint, fade), 0.2f, 0.12f, field.Seed);
			edge.Turn = age * 0.8f;
			ringBatch.Add(ground + Vector3.up * 0.03f, Vector3.right * field.Radius, Vector3.forward * field.Radius, edge);
			float pulse = Mathf.Repeat(age, fieldPulseSeconds) / fieldPulseSeconds;
			if (pulse < 0.4f)
			{
				DrawBolts(ground + Vector3.up * 0.35f, field.Radius * 0.1f, field.Radius * 0.95f, fieldBolts, field.Seed + Mathf.Floor(age / fieldPulseSeconds) * 0.37f, fade * (1f - pulse / 0.4f), view);
				Illuminate(field.Position, field.Radius, 0.35f * fade);
			}
			return true;
		}

		// 가운데에서 사방으로 꺾이는 번개. 0.05초마다 다시 꺾임
		private void DrawBolts(Vector3 center, float from, float to, int count, float seed, float strength, Transform view)
		{
			const int SEGMENTS = 4;
			Random.State state = Random.state;
			Random.InitState((int)(seed * 9973f) + (int)(Time.time * 20f));
			for (int i = 0; i < count; i++)
			{
				float angle = (i + Random.Range(-0.3f, 0.3f)) / count * Mathf.PI * 2f;
				Vector3 outward = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
				Vector3 side = Vector3.Cross(outward, Vector3.up);
				Vector3 last = center + outward * from;
				float reach = to * Random.Range(0.7f, 1f);
				for (int j = 1; j <= SEGMENTS; j++)
				{
					float part = j / (float)SEGMENTS;
					Vector3 next = center + outward * Mathf.Lerp(from, reach, part) + side * Random.Range(-0.22f, 0.22f) * reach * 0.6f + Vector3.up * (Random.Range(0f, 0.5f) * (1f - part));
					float width = boltWidth * (1f - part * 0.6f);
					boltBatch.AddStrip(last, next, width * 1.7f, view.forward, PaintedFxSet.StreakRow(2), FxQuadLook.Of(Alpha(boltTint, strength), 0.25f));
					boltBatch.AddStrip(last, next, width * 0.7f, view.forward, PaintedFxSet.StreakRow(0), FxQuadLook.Of(Alpha(hotTint, strength)));
					last = next;
				}
			}
			Random.state = state;
		}

		private void Illuminate(Vector3 position, float radius, float strength)
		{
			lamp.enabled = true;
			lamp.transform.position = position + Vector3.up * 1.2f;
			lamp.range = radius * 2.4f;
			lamp.intensity = lampIntensity * strength;
		}

		private static Color Alpha(Color color, float alpha) => new(color.r * alpha, color.g * alpha, color.b * alpha, color.a * alpha);

		private void Prepare()
		{
			if (root != null)
				return;
			root = new GameObject("CrossShurikenFx").transform;
			root.SetParent(transform, false);
			shadeBatch = Batch("Shade", art.Soft, 2990);
			scorchBatch = Batch("Scorch", art.Scorch, 2992);
			ringBatch = Batch("Ring", art.Shock, 2994);
			glowBatch = Batch("Glow", art.Soft, 3110);
			boltBatch = Batch("Bolt", art.Streaks, 3120, true);
			burstBatch = Batch("Burst", art.Grand, 3130, true);
			lamp = new GameObject("Lamp").AddComponent<Light>();
			lamp.transform.SetParent(root, false);
			lamp.type = LightType.Point;
			lamp.color = lampColor;
			lamp.shadows = LightShadows.None;
			lamp.enabled = false;
		}

		private FxQuadBatch Batch(string name, Texture texture, int queue, bool onTop = false)
		{
			FxQuadBatch batch = new($"CrossShuriken{name}", root, art.Shader, texture, art.Noise, queue, onTop);
			batches.Add(batch);
			return batch;
		}
	}
}

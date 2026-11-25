using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	// 알리사 빛의 검의 그림. 검기는 앞으로 나는 깃 초승달과 뒤따르는 얇은 잔상과 빛줄, 최상 단계는 끝에서 별로 터지고, 날개는 등 뒤에서 한 번 펼쳐짐
	// - 판정 없음. SwordWaveEmitter 가 검기를 낼 때 같은 자리, 방향, 길이로 부름
	// - 그린 텍스처 (PaintedFxSet) 를 겹겹이. 텍스처마다 FxQuadBatch 하나. 게임 시간
	// - 검기 판정 오브젝트 (0.2초) 보다 그림이 길어서 따로 둠. 만든 물체는 이 오브젝트의 자식 (풀과 같이 씬을 넘어 살아남음)
	// - 그림은 LateUpdate 에서만 그림. PlayWave 와 PlayWings 는 예약만 해서 부른 쪽 (판정) 을 끊지 않음
	public sealed class SwordWaveFx : MonoBehaviour
	{
		private static readonly Rect FULL = new(0f, 0f, 1f, 1f);
		private static readonly int INTENSITY_ID = Shader.PropertyToID("_Intensity");
		private const int RING_FIRST_FRAME = 2;

		[SerializeField] private PaintedFxSet art;

		[Header("색")]
		[SerializeField, ColorUsage(false, true)] private Color lightTint = new(0.74f, 0.72f, 0.66f);
		[SerializeField, ColorUsage(false, true)] private Color hotTint = new(1.15f, 1.12f, 1.1f);
		[SerializeField] private Color goldTint = new(1f, 0.82f, 0.28f, 1f);
		[SerializeField] private Color paleTint = new(1f, 0.95f, 0.78f, 1f);
		[SerializeField] private Color shadeTint = new(0.03f, 0.021f, 0.008f, 1f);

		[Header("검기")]
		[SerializeField, Min(0.1f), Tooltip("검기 그림의 반 크기 (폭 배율 1 일 때)")] private float crescentSize = 1.05f;
		[SerializeField, Range(0f, 1f), Tooltip("그림 안에서 앞날이 있는 자리")] private float edgeInPicture = 0.976f;
		[SerializeField, Min(0f)] private float height = 0.7f;
		[SerializeField, Min(0f), Tooltip("나오는 자리 (인물 앞)")] private float startDistance = 0.4f;
		[SerializeField, Min(0.02f), Tooltip("끝까지 나는 길이 (초)")] private float flySeconds = 0.22f;
		[SerializeField, Min(0.02f)] private float fadeSeconds = 0.22f;
		[SerializeField, Range(0f, 1f), Tooltip("판을 카메라 쪽으로 세우는 정도. 0 이면 바닥에 누움")] private float stand = 0.6f;
		[SerializeField, Min(0f), Tooltip("나오는 순간 줄어들어 있는 정도")] private float birthShrink = 0.45f;
		[SerializeField, Min(0)] private int echoes = 2;
		[SerializeField, Min(0f), Tooltip("잔상 사이 거리")] private float echoGap = 0.7f;
		[SerializeField, Range(0f, 1f)] private float echoAlpha = 0.55f;
		[SerializeField, Min(0), Tooltip("뒤따르는 빛줄 수")] private int trails = 4;
		[SerializeField, Min(0f)] private float trailLength = 3.2f;
		[SerializeField, Min(0f)] private float trailWidth = 0.7f;
		[SerializeField, Min(0f)] private float shade = 0.6f;
		[SerializeField, Min(0.01f)] private float flashHalfLife = 0.06f;
		[SerializeField, Min(0)] private int sparksPerWave = 6;

		[Header("끝 폭발")]
		[SerializeField, Min(0f)] private float burstSize = 2.8f;
		[SerializeField, Min(0.02f)] private float burstSeconds = 0.4f;
		[SerializeField, Min(0f)] private float ringSize = 1.5f;
		[SerializeField, Min(0.02f)] private float ringSeconds = 0.3f;
		[SerializeField, Min(0)] private int sparksPerBurst = 10;

		[Header("날개")]
		[SerializeField, Min(0.1f), Tooltip("날개 그림의 반 폭")] private float wingSpan = 2.5f;
		[SerializeField] private float wingAspect = 1.5f;
		[SerializeField] private float wingHeight = 1.1f;
		[SerializeField, Min(0.02f)] private float wingOpen = 0.18f;
		[SerializeField, Min(0.02f)] private float wingHold = 0.5f;
		[SerializeField, Min(0.02f)] private float wingBreak = 0.4f;
		[SerializeField, Min(0f)] private float wingSigil = 2.4f;
		[SerializeField, Min(0f)] private float sparkIntensity = 2.4f;

		private struct Wave
		{
			public Vector3 Origin;
			public Vector3 Direction;
			public float Length;
			public float Width;
			public float StartTime;
			public float Seed;
			public bool BurstAtEnd;
		}

		private struct Burst
		{
			public Vector3 Position;
			public float Ground;
			public float Size;
			public float StartTime;
			public float Seed;
		}

		private readonly List<Wave> waves = new();
		private readonly List<Burst> bursts = new();
		private readonly List<FxQuadBatch> batches = new();
		private Transform root;
		private Transform wingOwner;
		private Camera viewCamera;
		private FxQuadBatch shadeBatch;
		private FxQuadBatch shockBatch;
		private FxQuadBatch sigilBatch;
		private FxQuadBatch wingGlow;
		private FxQuadBatch wingBatch;
		private FxQuadBatch trailBatch;
		private FxQuadBatch echoBatch;
		private FxQuadBatch waveBatch;
		private FxQuadBatch glowBatch;
		private FxQuadBatch impactBatch;
		private ParticleSystem sparks;
		private Material sparkMaterial;
		private float wingStart = float.NegativeInfinity;

		// 검기 하나. length 는 월드 길이, width 는 배율
		public void PlayWave(Vector3 origin, Vector3 direction, float length, float width, bool burstAtEnd)
		{
			Prepare();
			Vector3 forward = direction.normalized;
			waves.Add(new Wave { Origin = origin + Vector3.up * height, Direction = forward, Length = length, Width = width, StartTime = Time.time, Seed = Random.value, BurstAtEnd = burstAtEnd });
			for (int i = 0; i < sparksPerWave; i++)
				UltimateFxStage.Emit(sparks, origin + Vector3.up * height + forward, (forward * 2f + Random.onUnitSphere).normalized * Random.Range(3f, 8f), Random.Range(0.06f, 0.13f), Random.Range(0.15f, 0.35f), Color.white);
		}

		public void PlayWings(Transform owner)
		{
			Prepare();
			wingOwner = owner;
			wingStart = Time.time;
		}

		private void OnDisable()
		{
			waves.Clear();
			bursts.Clear();
			wingStart = float.NegativeInfinity;
			foreach (FxQuadBatch batch in batches)
				batch.Clear();
		}

		private void OnDestroy()
		{
			foreach (FxQuadBatch batch in batches)
				batch.Destroy();
			Destroy(sparkMaterial);
		}

		private void LateUpdate()
		{
			if (root == null)
				return;
			viewCamera = Camera.main;
			// 무기 오브젝트는 베기마다 자리를 옮김. 그림은 월드 자리라 뿌리를 원점에 되돌림
			root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
			// 씬이 바뀌는 사이에는 카메라가 없음. 그동안 감춤
			root.gameObject.SetActive(viewCamera != null);
			if (viewCamera == null)
				return;
			foreach (FxQuadBatch batch in batches)
				batch.Begin();
			for (int i = waves.Count - 1; i >= 0; i--)
			{
				if (DrawWave(i) == false)
					waves.RemoveAt(i);
			}
			for (int i = bursts.Count - 1; i >= 0; i--)
			{
				if (DrawBurst(bursts[i]) == false)
					bursts.RemoveAt(i);
			}
			DrawWings();
			foreach (FxQuadBatch batch in batches)
				batch.Commit();
		}

		// 끝났으면 false
		private bool DrawWave(int index)
		{
			Wave wave = waves[index];
			float age = Time.time - wave.StartTime;
			float fly = Mathf.Clamp01(age / flySeconds);
			float fade = Mathf.Clamp01((age - flySeconds) / fadeSeconds);
			if (fade >= 1f)
				return false;
			if (fly >= 1f && wave.BurstAtEnd)
			{
				wave.BurstAtEnd = false;
				waves[index] = wave;
				PlayBurst(wave.Origin + wave.Direction * wave.Length, wave.Width);
			}
			Vector3 viewForward = viewCamera.transform.forward;
			float heat = Mathf.Pow(0.5f, age / flashHalfLife);
			float distance = Mathf.Lerp(startDistance, wave.Length, 1f - (1f - fly) * (1f - fly));
			float size = crescentSize * wave.Width * (1f - birthShrink * (1f - fly) * (1f - fly));
			Vector3 head = wave.Origin + wave.Direction * distance;
			// 옆에서 봐도 초승달로 읽히게 판을 카메라 쪽으로 세움
			Vector3 facing = FxQuadBatch.Side(wave.Direction, viewForward);
			Vector3 flat = Vector3.Cross(wave.Direction, Vector3.up).normalized;
			Vector3 up = Vector3.Slerp(Vector3.Dot(flat, facing) < 0f ? -flat : flat, facing, stand).normalized;
			Vector3 along = wave.Direction * size;
			Vector3 center = head - wave.Direction * (size * (edgeInPicture * 2f - 1f));

			shadeBatch.Add(center, along * 1.4f, up * (size * 1.4f), FxQuadLook.Of(Alpha(shadeTint, shade * (1f - fade)), 1f));
			float tail = Mathf.Min(trailLength * wave.Width, distance);
			for (int i = 0; i < trails; i++)
			{
				float across = trails <= 1 ? 0f : i / (trails - 1f) * 2f - 1f;
				Vector3 start = head + up * (across * size * 0.78f) - wave.Direction * (size * (0.25f + Mathf.Abs(across)));
				float length = tail * Mathf.Lerp(0.6f, 1f, Mathf.Repeat(wave.Seed * 7.1f + i * 0.618f, 1f));
				trailBatch.AddStrip(start - wave.Direction * length, start, trailWidth * wave.Width, viewForward, PaintedFxSet.StreakRow(i % 2 == 0 ? 0 : 3), FxQuadLook.Of(i % 2 == 0 ? paleTint : goldTint, 0.2f, fade, wave.Seed + i));
			}
			for (int i = echoes; i >= 1; i--)
			{
				float back = Mathf.Min(distance, echoGap * wave.Width * i);
				echoBatch.Add(center - wave.Direction * back, along * (1f - 0.08f * i), up * (size * (1f - 0.08f * i)), FxQuadLook.Of(Alpha(paleTint, echoAlpha / i), 0.2f, Mathf.Max(fade, 0.18f * i), wave.Seed + i * 0.3f));
			}
			waveBatch.Add(center, along, up * size, FxQuadLook.Of(Color.Lerp(lightTint, hotTint, heat), 0.95f, fade, wave.Seed));
			if (heat > 0.05f)
				Billboard(glowBatch, head, size * 0.8f, FxQuadLook.Of(goldTint * (heat * 0.3f)));
			return true;
		}

		private void PlayBurst(Vector3 position, float width)
		{
			bursts.Add(new Burst { Position = position, Ground = position.y - height + 0.06f, Size = width, StartTime = Time.time, Seed = Random.value });
			for (int i = 0; i < sparksPerBurst; i++)
				UltimateFxStage.Emit(sparks, position, (Vector3.up * 0.6f + Random.onUnitSphere).normalized * Random.Range(2f, 7f), Random.Range(0.07f, 0.16f), Random.Range(0.25f, 0.5f), Color.white);
		}

		// 끝났으면 false
		private bool DrawBurst(in Burst burst)
		{
			float age = Time.time - burst.StartTime;
			if (age >= burstSeconds)
				return false;
			float progress = age / burstSeconds;
			int frame = Mathf.Min(15, RING_FIRST_FRAME + Mathf.FloorToInt(progress * (16 - RING_FIRST_FRAME)));
			Billboard(shadeBatch, burst.Position, burstSize * 0.7f, FxQuadLook.Of(Alpha(shadeTint, 0.4f * (1f - progress)), 1f));
			Billboard(impactBatch, burst.Position, burstSize * burst.Size * 0.5f * (0.9f + 0.25f * progress), FxQuadLook.Of(lightTint, 0.8f), FxQuadBatch.Cell(frame, 4, 4));
			float ring = age / ringSeconds;
			if (ring < 1f)
			{
				FxQuadLook look = FxQuadLook.Of(lightTint, 0.3f, ring, burst.Seed);
				look.Turn = burst.Seed * 6.28f;
				float radius = ringSize * burst.Size * (0.3f + 0.7f * Mathf.Sqrt(ring));
				shockBatch.Add(new Vector3(burst.Position.x, burst.Ground, burst.Position.z), Vector3.right * radius, Vector3.forward * radius, look);
			}
			return true;
		}

		private void DrawWings()
		{
			float age = Time.time - wingStart;
			if (age >= wingHold + wingBreak || wingOwner == null)
				return;
			float open = Mathf.Clamp01(age / wingOpen);
			open = 1f - (1f - open) * (1f - open);
			float gone = Mathf.Clamp01((age - wingHold) / wingBreak);
			float heat = Mathf.Pow(0.5f, age / (flashHalfLife * 2f));
			Transform cameraTransform = viewCamera.transform;
			Vector3 center = wingOwner.position + Vector3.up * wingHeight + cameraTransform.forward * 0.3f;
			float beat = 1f + 0.04f * Mathf.Sin(age * 14f);
			wingGlow.Add(center, cameraTransform.right * (wingSpan * 1.3f), cameraTransform.up * (wingSpan * 1.1f), FxQuadLook.Of(Alpha(shadeTint, 0.55f * open * (1f - gone)), 1f));
			Vector3 right = cameraTransform.right * (wingSpan * Mathf.Lerp(0.25f, 1f, open) * beat * (1f + 0.1f * gone));
			Vector3 up = cameraTransform.up * (wingSpan / wingAspect * Mathf.Lerp(0.6f, 1f, open) * (1f + 0.1f * gone));
			FxQuadLook look = FxQuadLook.Of(Color.Lerp(lightTint, hotTint, heat), 0.85f, gone, 0.37f);
			wingBatch.Add(center, right, up, look);
			float radius = wingSigil * Mathf.Lerp(0.4f, 1f, open);
			FxQuadLook ground = FxQuadLook.Of(Alpha(lightTint, 0.8f), 0.3f, Mathf.Max(1f - open, gone), 0.2f);
			ground.Turn = age * 1.2f;
			sigilBatch.Add(wingOwner.position + Vector3.up * 0.08f, Vector3.right * radius, Vector3.forward * radius, ground);
			if (heat > 0.05f)
				Billboard(glowBatch, center, wingSpan * 0.8f, FxQuadLook.Of(goldTint * (heat * 0.5f)));
		}

		private void Billboard(FxQuadBatch batch, Vector3 position, float half, in FxQuadLook look) => Billboard(batch, position, half, look, FULL);

		private void Billboard(FxQuadBatch batch, Vector3 position, float half, in FxQuadLook look, Rect uv)
		{
			Transform cameraTransform = viewCamera.transform;
			batch.Add(position, cameraTransform.right * half, cameraTransform.up * half, uv, look);
		}

		private static Color Alpha(Color color, float alpha) => new(color.r, color.g, color.b, alpha);

		private void Prepare()
		{
			if (root != null)
				return;
			if (art == null)
				throw new InvalidOperationException($"{name}: 검기 그림 묶음 미연결");
			root = new GameObject(nameof(SwordWaveFx)).transform;
			root.SetParent(transform, false);
			sigilBatch = Layer("Sigil", art.Sigil, 2948, false);
			shockBatch = Layer("Shock", art.Shock, 2951, false);
			wingGlow = Layer("WingGlow", art.Soft, 2956, false);
			wingBatch = Layer("Wings", art.Wings, 2962, true);
			shadeBatch = Layer("Shade", art.Soft, 3005, true);
			trailBatch = Layer("Trail", art.Streaks, 3006, true);
			echoBatch = Layer("Echo", art.CrescentThin, 3008, true);
			waveBatch = Layer("Wave", art.Crescent, 3010, true);
			glowBatch = Layer("Glow", art.Soft, 3012, true);
			impactBatch = Layer("Impact", art.Impact, 3014, true);
			sparkMaterial = new Material(art.Shader) { name = "SwordSpark", renderQueue = 3016, mainTexture = art.Spark };
			sparkMaterial.SetTexture("_NoiseTex", art.Noise);
			sparkMaterial.SetFloat(INTENSITY_ID, sparkIntensity);
			sparks = UltimateFxStage.CreateParticles("SwordSparks", root, sparkMaterial, 400, 0.4f, true);
			UltimateFxStage.SetLifetimeLook(sparks, UltimateFxStage.FadeGradient(Color.white, 0.02f, 0.45f), AnimationCurve.Linear(0f, 1f, 1f, 0.15f));
		}

		// 인물 그림은 큐 3000. 그보다 작으면 뒤
		private FxQuadBatch Layer(string layerName, Texture texture, int queue, bool onTop)
		{
			FxQuadBatch batch = new("Sword" + layerName, root, art.Shader, texture, art.Noise, queue, onTop);
			batches.Add(batch);
			return batch;
		}
	}
}

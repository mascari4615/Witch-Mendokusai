using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	// 일제 사격의 줄기, 총구 섬광, 맞은 자리
	// - 줄기는 어둠, 몸, 심, 곁가지, 머리 다섯 겹. 총구에서 탄 머리까지 이어지고 꼬리가 뒤따라 줄어듦
	// - 탄이 꺼지면 (맞음이나 수명 끝) 마지막 자리에서 플립북이 터지고 바닥에 자국이 남음
	public sealed partial class VolleyArsenal
	{
		private static readonly int INTENSITY_ID = Shader.PropertyToID("_Intensity");

		[Header("줄기")]
		[SerializeField, Min(0.02f), Tooltip("가장 굵은 몸의 폭")] private float beamWidth = 1.1f;
		[SerializeField, Min(0.02f), Tooltip("머리 그림의 길이")] private float boltLength = 2.8f;
		[SerializeField, Min(0.02f)] private float boltWidth = 0.95f;
		[SerializeField, Min(0), Tooltip("곁을 따라 나는 바늘 줄기 수")] private int sideStreaks = 4;
		[SerializeField, Min(0f)] private float sideSpread = 0.6f;
		[SerializeField, Min(0f), Tooltip("꼬리가 총구에 머무는 길이")] private float tailHold = 0.3f;
		[SerializeField, Min(1f), Tooltip("꼬리가 머리를 쫓는 빠르기")] private float tailSpeed = 25f;
		[SerializeField, Min(0.02f), Tooltip("총구에서 나간 줄기가 실제 탄의 길로 모이는 길이")] private float convergeSeconds = 0.35f;
		[SerializeField, Min(0.02f), Tooltip("탄이 꺼진 뒤 줄기가 깎이는 길이")] private float beamFade = 0.35f;
		[SerializeField, Min(0f), Tooltip("탄 중심에서 그림 높이 (탄 충돌 높이와 같게)")] private float shotHeight = 0.8f;
		[SerializeField, Min(0f), Tooltip("한 프레임에 이보다 멀리 뛰면 다른 탄으로 다시 쓰인 것으로 봄")] private float jumpDistance = 3f;
		[SerializeField, Min(0f), Tooltip("끊긴 프레임에서 허용하는 빠르기 (월드/초)")] private float jumpSpeed = 45f;

		[Header("곁줄기 (그림만)")]
		[SerializeField, Tooltip("한 발마다 다른 문에서 같이 나는 가는 줄기. 첫 발과 마지막 발")] private Vector2 phantomsPerShot = new(3f, 11f);
		[SerializeField, Min(0.02f)] private float phantomSeconds = 0.3f;
		[SerializeField] private Vector2 phantomReach = new(6f, 10f);
		[SerializeField, Range(0f, 90f)] private float phantomFan = 30f;
		[SerializeField, Min(0f)] private float phantomWidth = 1.15f;

		[Header("총구와 맞은 자리")]
		[SerializeField, Min(0f)] private float muzzleSize = 2.1f;
		[SerializeField, Min(0.02f)] private float muzzleSeconds = 0.22f;
		[SerializeField, Min(0f)] private float impactSize = 3.4f;
		[SerializeField, Min(0.02f)] private float impactSeconds = 0.5f;
		[SerializeField, Min(0f)] private float pillarHeight = 2.6f;
		[SerializeField, Min(0f)] private float pillarWidth = 0.32f;
		[SerializeField, Min(0f)] private float ringSize = 1.5f;
		[SerializeField, Min(0.02f)] private float ringSeconds = 0.32f;
		[SerializeField, Min(0f)] private float scorchSize = 0.75f;
		[SerializeField, Min(0.02f)] private float scorchSeconds = 0.55f;
		[SerializeField, Min(0), Tooltip("플립북의 앞쪽 빈 칸을 건너뜀")] private int impactFirstFrame = 2;
		[SerializeField, Min(0)] private int muzzleFirstFrame = 2;
		[SerializeField, Min(0)] private int sparksPerMuzzle = 5;
		[SerializeField, Min(0)] private int sparksPerImpact = 16;
		[SerializeField, Min(0f)] private float sparkIntensity = 2.4f;

		[Header("빛")]
		[SerializeField, Min(0f)] private float lightIntensity = 3f;
		[SerializeField, Min(0.1f)] private float lightRange = 9f;

		private sealed class Tracer
		{
			public Transform Shot;
			public Vector3 Head;
			public Vector3 Tail;
			// 총구와 탄 시작 자리의 차이. 시간이 지나며 0 이 되어 그림이 탄의 길로 모임
			public Vector3 Offset;
			public Vector3 Shown;
			public float StartTime;
			public float EndTime;
			public float Seed;
			public int Row;
			public float Scale;
			// 완드째 날아가면 그 길이
			public float WandLength;
			public Color Tint;
			public bool Alive;
		}

		private struct Phantom
		{
			public Vector3 From;
			public Vector3 To;
			public float StartTime;
			public float Seed;
			public int Row;
			public bool Pale;
		}

		private struct Burst
		{
			public Vector3 Position;
			public Vector3 Forward;
			public float Ground;
			public float StartTime;
			public float Seed;
			public float Scale;
			public bool Muzzle;
		}

		private readonly List<Tracer> tracers = new();
		private readonly List<Burst> bursts = new();
		private readonly List<Phantom> phantoms = new();
		private FxQuadBatch phantomBatch;
		private FxQuadBatch flyingWand;
		private FxQuadBatch shaftBatch;
		private FxQuadBatch streakShade;
		private FxQuadBatch streakFront;
		private FxQuadBatch boltBatch;
		private FxQuadBatch impactBatch;
		private FxQuadBatch muzzleBatch;
		private FxQuadBatch flashSoft;
		private ParticleSystem sparks;
		private Light flashLight;
		private float lightLevel;

		private void PrepareShots()
		{
			phantomBatch = Layer("Phantom", art.Streaks, 3006, true);
			shaftBatch = Layer("Pillar", art.Soft, 3007, true);
			streakShade = Layer("StreakShade", art.Streaks, 3008, true);
			streakFront = Layer("Streak", art.Streaks, 3010, true);
			boltBatch = Layer("Bolt", art.Bolt, 3012, true);
			flashSoft = Layer("FlashSoft", art.Soft, 3013, true);
			flyingWand = Layer("FlyingWand", art.Wands[^1], 3013, true);
			impactBatch = Layer("Impact", art.Impact, 3014, true);
			muzzleBatch = Layer("Muzzle", art.Muzzle, 3016, true);
			Material sparkMaterial = Own(new Material(art.Shader) { name = "VolleySpark", renderQueue = 3018, mainTexture = art.Spark });
			sparkMaterial.SetTexture("_NoiseTex", art.Noise);
			sparkMaterial.SetFloat(INTENSITY_ID, sparkIntensity);
			sparks = UltimateFxStage.CreateParticles("VolleySparks", root, sparkMaterial, 600, 0.5f, true);
			UltimateFxStage.SetLifetimeLook(sparks, UltimateFxStage.FadeGradient(Color.white, 0.02f, 0.45f), AnimationCurve.Linear(0f, 1f, 1f, 0.15f));
			ParticleSystem.NoiseModule turbulence = sparks.noise;
			turbulence.enabled = true;
			turbulence.strength = 0.8f;
			turbulence.frequency = 1.4f;
			PrepareFinale();
			flashLight = new GameObject("VolleyLight").AddComponent<Light>();
			flashLight.transform.SetParent(root, false);
			flashLight.type = LightType.Point;
			flashLight.color = lightColor;
			flashLight.range = lightRange;
			flashLight.enabled = false;
		}

		private void StartTracer(Vector3 muzzle, Transform shot, float scale, float wandLength)
		{
			// 풀이 같은 탄을 바로 다시 냈으면 옛 줄기는 거기서 끝
			foreach (Tracer old in tracers)
			{
				if (old.Alive && old.Shot == shot)
					EndTracer(old);
			}
			Tracer tracer = tracers.Find(candidate => candidate.Alive == false);
			if (tracer == null)
			{
				tracer = new Tracer();
				tracers.Add(tracer);
			}
			tracer.Shot = shot;
			tracer.Head = shot.position + Vector3.up * shotHeight;
			tracer.Tail = muzzle;
			tracer.Offset = muzzle - tracer.Head;
			tracer.Shown = muzzle;
			tracer.StartTime = Time.time;
			tracer.EndTime = float.PositiveInfinity;
			tracer.Seed = Random.value;
			tracer.Row = Random.value < 0.5f ? 1 : 2;
			tracer.Scale = scale;
			tracer.WandLength = wandLength;
			tracer.Tint = shotsLeft % 2 == 0 ? goldTint : paleTint;
			tracer.Alive = true;
		}

		private void DrawShots()
		{
			Vector3 viewForward = viewCamera.transform.forward;
			foreach (Tracer tracer in tracers)
			{
				if (tracer.Alive)
					DrawTracer(tracer, viewForward);
			}
			for (int i = bursts.Count - 1; i >= 0; i--)
			{
				if (DrawBurst(bursts[i], viewForward) == false)
					bursts.RemoveAt(i);
			}
			for (int i = phantoms.Count - 1; i >= 0; i--)
			{
				if (DrawPhantom(phantoms[i], viewForward) == false)
					phantoms.RemoveAt(i);
			}
			DrawFinale();
			lightLevel *= Mathf.Pow(0.5f, Time.deltaTime / 0.08f);
			flashLight.enabled = lightLevel > 0.02f;
			flashLight.intensity = lightIntensity * lightLevel;
			flashLight.transform.position = owner.position + Vector3.up * 1.4f + Aim() * 0.8f;
		}

		// 줄기 끝을 실제 맞은 자리에 맞추고 터뜨림
		private void EndTracer(Tracer tracer)
		{
			tracer.Shot = null;
			tracer.EndTime = Time.time;
			tracer.Shown = tracer.Head;
			bursts.Add(new Burst { Position = tracer.Head, Forward = Vector3.up, Ground = tracer.Head.y - shotHeight + groundLift, StartTime = Time.time, Seed = Random.value, Scale = tracer.Scale });
			for (int i = 0; i < sparksPerImpact; i++)
			{
				Vector3 velocity = (Vector3.up * 0.8f + Random.onUnitSphere).normalized * Random.Range(2f, 8f);
				UltimateFxStage.Emit(sparks, tracer.Head, velocity, Random.Range(0.08f, 0.2f), Random.Range(0.25f, 0.6f), Color.white);
			}
		}

		private void MuzzleFlash(Vector3 position, Vector3 forward)
		{
			bursts.Add(new Burst { Position = position, Forward = forward, StartTime = Time.time, Seed = Random.value, Muzzle = true });
			for (int i = 0; i < sparksPerMuzzle; i++)
			{
				Vector3 velocity = (forward * 1.6f + Random.onUnitSphere).normalized * Random.Range(3f, 9f);
				UltimateFxStage.Emit(sparks, position, velocity, Random.Range(0.06f, 0.14f), Random.Range(0.15f, 0.35f), Color.white);
			}
		}

		private void DrawTracer(Tracer tracer, Vector3 viewForward)
		{
			bool ended = tracer.EndTime != float.PositiveInfinity;
			if (ended == false)
			{
				Vector3 now = tracer.Shot == null ? tracer.Head : tracer.Shot.position + Vector3.up * shotHeight;
				bool gone = tracer.Shot == null || tracer.Shot.gameObject.activeInHierarchy == false || (now - tracer.Head).magnitude > Mathf.Max(jumpDistance, jumpSpeed * Time.deltaTime);
				if (gone)
				{
					EndTracer(tracer);
					ended = true;
				}
				else
				{
					tracer.Head = now;
					tracer.Shown = now + tracer.Offset * (1f - Mathf.Clamp01((Time.time - tracer.StartTime) / convergeSeconds));
				}
			}
			if (Time.time - tracer.StartTime > tailHold)
				tracer.Tail = Vector3.MoveTowards(tracer.Tail, tracer.Shown, tailSpeed * Time.deltaTime);
			float faded = ended ? Mathf.Clamp01((Time.time - tracer.EndTime) / beamFade) : 0f;
			Vector3 span = tracer.Shown - tracer.Tail;
			float length = span.magnitude;
			if (faded >= 1f || (ended && length < 0.05f))
			{
				tracer.Alive = false;
				return;
			}
			if (length < 0.05f)
				return;
			Vector3 direction = span / length;
			// 나온 순간 굵고 금방 제 굵기로
			float birth = Mathf.Pow(0.5f, (Time.time - tracer.StartTime) / 0.06f);
			float width = beamWidth * tracer.Scale * (1f + 0.7f * birth) * (1f - 0.5f * faded);
			Color body = Color.Lerp(tracer.Tint, lightTint, 0.25f + 0.4f * birth);

			streakShade.AddStrip(tracer.Tail, tracer.Shown, width * 1.3f, viewForward, PaintedFxSet.StreakRow(2), FxQuadLook.Of(WithAlpha(shadeTint, 0.7f), 1f, faded, tracer.Seed));
			FxQuadLook look = FxQuadLook.Of(body, 0.85f, faded, tracer.Seed);
			look.Distort = 0.02f;
			streakFront.AddStrip(tracer.Tail, tracer.Shown, width, viewForward, PaintedFxSet.StreakRow(tracer.Row), look);
			look = FxQuadLook.Of(WithAlpha(hotTint, 0.8f), 0f, faded, tracer.Seed);
			streakFront.AddStrip(tracer.Tail, tracer.Shown, width * 0.6f, viewForward, PaintedFxSet.StreakRow(0), look);
			Vector3 side = FxQuadBatch.Side(direction, viewForward);
			for (int i = 0; i < sideStreaks; i++)
			{
				float offset = sideSpread * (Mathf.Repeat(tracer.Seed * 13.7f + i * 0.618f, 1f) * 2f - 1f);
				float start = Mathf.Repeat(tracer.Seed * 5.3f + i * 0.43f, 0.5f);
				look = FxQuadLook.Of(WithAlpha(i % 2 == 0 ? goldTint : paleTint, 0.8f), 0f, faded, tracer.Seed + i);
				streakFront.AddStrip(tracer.Tail + span * start + side * offset, tracer.Shown - span * (0.1f * i) + side * (offset * 0.4f), width * 0.7f, viewForward, PaintedFxSet.StreakRow(i % 2 == 0 ? 0 : 3), look);
			}
			if (ended)
				return;
			float headLength = Mathf.Min(boltLength, length + 0.8f);
			look = FxQuadLook.Of(body, 0.9f, 0f, tracer.Seed);
			look.Distort = 0.03f;
			boltBatch.AddStrip(tracer.Shown - direction * (headLength * 0.85f), tracer.Shown + direction * (headLength * 0.15f), boltWidth * tracer.Scale * (1f + 0.5f * birth), viewForward, new Rect(0f, 0f, 1f, 1f), look);
			if (tracer.WandLength > 0f)
				flyingWand.AddStrip(tracer.Shown - direction * (tracer.WandLength * 0.75f), tracer.Shown + direction * (tracer.WandLength * 0.25f), tracer.WandLength / weaponAspects[^1], viewForward, new Rect(0f, 0f, 1f, 1f), FxQuadLook.Of(Color.white * 1.08f, 1f));
		}

		// 판정 없는 가는 줄기. 아직 서 있는 문에서 부채 안 아무 데로
		private void SpawnPhantoms(int count)
		{
			for (int i = 0; i < count; i++)
			{
				Weapon from = weapons[Random.Range(0, weapons.Count)];
				if (from.Alive == false)
					continue;
				// 둘러선 문도 쏘는 것처럼 번쩍임
				if (from.Decor)
					from.FireTime = Time.time;
				Pose(from, out Vector3 position, out _);
				Vector3 direction = Quaternion.Euler(0f, Random.Range(-0.5f, 0.5f) * phantomFan, 0f) * Aim();
				phantoms.Add(new Phantom
				{
					From = position,
					To = owner.position + Vector3.up * shotHeight + direction * Random.Range(phantomReach.x, phantomReach.y),
					StartTime = Time.time + Random.value * 0.05f,
					Seed = Random.value,
					Row = i % 3 == 2 ? 3 : i % 3 == 1 ? 1 : 0,
					Pale = Random.value < 0.5f,
				});
			}
		}

		// 끝났으면 false
		private bool DrawPhantom(in Phantom phantom, Vector3 viewForward)
		{
			float progress = (Time.time - phantom.StartTime) / phantomSeconds;
			if (progress < 0f)
				return true;
			if (progress >= 1f)
				return false;
			Vector3 span = phantom.To - phantom.From;
			float head = Mathf.Clamp01(progress * 1.6f);
			float tail = Mathf.Clamp01((progress * 1.6f - 0.75f) / 0.85f);
			FxQuadLook look = FxQuadLook.Of(WithAlpha(phantom.Pale ? paleTint : goldTint, 0.9f), 0.15f, Mathf.Clamp01(progress * 1.4f - 0.4f), phantom.Seed);
			phantomBatch.AddStrip(phantom.From + span * tail, phantom.From + span * head, phantomWidth * (1f - 0.5f * progress), viewForward, PaintedFxSet.StreakRow(phantom.Row), look);
			return true;
		}

		// 끝났으면 false
		private bool DrawBurst(in Burst burst, Vector3 viewForward)
		{
			float age = Time.time - burst.StartTime;
			if (burst.Muzzle)
			{
				if (age >= muzzleSeconds)
					return false;
				Rect cell = FxQuadBatch.Cell(Frame(age / muzzleSeconds, muzzleFirstFrame), 4, 4);
				float half = muzzleSize * 0.5f;
				Vector3 center = burst.Position + burst.Forward * ((0.5f - art.MuzzleOrigin) * muzzleSize);
				float heat = Mathf.Pow(0.5f, age / 0.05f);
				muzzleBatch.Add(center, burst.Forward * half, FxQuadBatch.Side(burst.Forward, viewForward) * half, cell, FxQuadLook.Of(Color.Lerp(lightTint, hotTint, heat * 0.4f), 0.8f));
				if (heat > 0.05f)
					Billboard(flashSoft, burst.Position, 0.6f, 0f, FxQuadLook.Of(Scale(goldTint, heat * 0.25f)));
				return true;
			}
			if (age < impactSeconds)
			{
				Rect cell = FxQuadBatch.Cell(Frame(age / impactSeconds, impactFirstFrame), 4, 4);
				float heat = Mathf.Pow(0.5f, age / 0.06f);
				float progress = age / impactSeconds;
				// 짧은 빛기둥
				Vector3 pillar = Vector3.up * (pillarHeight * burst.Scale * (0.4f + 0.6f * Mathf.Sqrt(progress)));
				Vector3 pillarSide = Vector3.Cross(viewForward, Vector3.up).normalized * (pillarWidth * burst.Scale * (1f - progress));
				shaftBatch.Add(burst.Position + pillar - Vector3.up * shotHeight, pillarSide, pillar, FxQuadLook.Of(Scale(goldTint, 0.8f * (1f - progress))));
				Billboard(flashSoft, burst.Position, impactSize * 0.75f, 0f, FxQuadLook.Of(WithAlpha(shadeTint, 0.5f * (1f - progress)), 1f));
				Billboard(impactBatch, burst.Position, impactSize * burst.Scale * 0.5f * (0.9f + 0.25f * progress), (burst.Seed - 0.5f) * 50f, cell, FxQuadLook.Of(Color.Lerp(Scale(lightTint, 0.9f), hotTint, heat * 0.35f), 0.8f));
				if (heat > 0.05f)
					Billboard(flashSoft, burst.Position, impactSize * 0.5f, 0f, FxQuadLook.Of(Scale(lightTint, heat * 0.3f)));
			}
			DrawImpactGround(burst, age);
			return age < scorchSeconds;
		}

		private static int Frame(float progress, int first) => Mathf.Min(15, first + Mathf.FloorToInt(progress * (16 - first)));

		private void StopShots()
		{
			foreach (Tracer tracer in tracers)
				tracer.Alive = false;
			bursts.Clear();
			phantoms.Clear();
			StopFinale();
			if (flashLight != null)
				flashLight.enabled = false;
			lightLevel = 0f;
		}
	}
}

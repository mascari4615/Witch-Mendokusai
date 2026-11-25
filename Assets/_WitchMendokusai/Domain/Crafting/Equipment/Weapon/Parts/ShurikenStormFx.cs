using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 알리사 표창 폭풍의 그림. 도는 동안 붓결 소용돌이 두 겹과 표창마다 붓결 띠, 흩어질 때 고리, 멈춘 순간 달아오름, 돌아온 순간 터짐
	// - 판정 없음. ShurikenStorm 이 매 프레임 자리와 반지름을 넘김. LateUpdate 에서만 그림
	// - 캐릭터와 같은 밀도의 판 (NinjaFormFx 와 같은 그림). 만든 물체는 이 오브젝트의 자식
	public sealed class ShurikenStormFx : MonoBehaviour
	{
		public struct Ring
		{
			public Vector3 Center;
			public float Radius;
			public float RestRadius;
			public float Height;
			public int Count;
			public float Spin;
			// 본체 1, 분신은 피해 비율
			public float Strength;
			public bool Hot;
			// 나가는 중이거나 멈춘 동안. 띠가 안쪽으로 남음
			public bool Outgoing;
		}

		private const int SIDE = 4;
		private const int TRAIL_ROWS = 4;
		private const int BRUSH_BURST_FRAME = 3;
		private const int SWIRL_FRAME = 4;
		private const int SWIRL_FRAMES = 3;
		private const int GROUND_QUEUE = 2955;
		private const int SWIRL_QUEUE = 2960;
		private const int TRAIL_QUEUE = 2965;
		private const int FRONT_QUEUE = 3120;
		private static readonly int COLOR_ID = Shader.PropertyToID("_Color");

		[Header("그림")]
		[SerializeField, Tooltip("셰이더와 잡음, 흩어질 때 고리")] private PaintedFxSet art;
		[SerializeField, Tooltip("4x4 붓결")] private Texture2D brushSheet;
		[SerializeField, Tooltip("가로 붓결 네 줄. 머리가 오른쪽")] private Texture2D trailTexture;
		[SerializeField, Tooltip("달아오를 때 네 갈래 빛")] private Texture2D sparkTexture;
		[SerializeField, Tooltip("달아오른 표창의 한 색")] private Shader hotShader;
		[SerializeField, Min(1f), Tooltip("그림이 바뀌는 횟수 (초당)")] private float poseSteps = 20f;

		[Header("소용돌이 (도는 반지름 배율)")]
		[SerializeField] private Vector2 swirlSize = new(1.28f, 0.82f);
		[SerializeField] private Vector2 swirlHeight = new(0.06f, 0.45f);
		[SerializeField, Tooltip("초당 도는 각. 표창보다 빠르게")] private Vector2 swirlDegrees = new(420f, 600f);
		[SerializeField] private Vector2 swirlAlpha = new(0.8f, 0.6f);
		[SerializeField, Min(0.1f), Tooltip("흩어지며 소용돌이가 사라지는 거리")] private float calmDistance = 1.5f;

		[Header("띠")]
		[SerializeField, Range(0f, 120f)] private float trailDegrees = 58f;
		[SerializeField, Min(0f)] private float trailWidth = 0.95f;
		[SerializeField, Min(0f), Tooltip("흩어질 때 띠 길이")] private float dashLength = 3f;
		[SerializeField, Min(2)] private int trailSegments = 10;

		[Header("순간")]
		[SerializeField, Min(0.02f)] private float landSeconds = 0.45f;
		[SerializeField, Min(0.1f)] private float landRadius = 2.2f;
		[SerializeField, Min(0.1f)] private float launchRadius = 2.8f;
		[SerializeField, Min(0.02f)] private float launchSeconds = 0.3f;
		[SerializeField] private Color launchTint = new(0.85f, 0.82f, 1f, 1f);
		[SerializeField] private Color launchShade = new(0.02f, 0.015f, 0.05f, 0.4f);
		[SerializeField] private Color hotColor = new(0.85f, 0.88f, 1f, 1f);
		[SerializeField, ColorUsage(false, true)] private Color sparkTint = new(1.1f, 1.15f, 1.4f);
		[SerializeField, Min(0f)] private float sparkSize = 0.9f;

		private readonly List<Ring> rings = new();
		private readonly List<FxQuadBatch> batches = new();
		private readonly Dictionary<SpriteRenderer, Material> heated = new();
		private Transform root;
		private FxQuadBatch groundBatch;
		private FxQuadBatch swirlBatch;
		private FxQuadBatch trailBatch;
		private FxQuadBatch sparkBatch;
		private Material hotMaterial;
		private float landTime = float.NegativeInfinity;
		private float lastRadius;

		// 이번 프레임의 폭풍. 첫 고리가 본체
		public void Begin() => rings.Clear();

		public void Add(Ring ring)
		{
			if (ring.Count > 0)
				rings.Add(ring);
		}

		public void PlayLaunch(Vector3 center)
		{
			PaintedBurstLayer.Play(art, new PaintedBurstLayer.Burst
			{
				Position = center,
				Radius = launchRadius,
				Seconds = launchSeconds,
				RingSeconds = launchSeconds,
				Tint = Color.white,
				RingTint = launchTint,
				Shade = launchShade,
				Cover = 1f,
			});
		}

		// 표창 그림을 한 색으로. 풀로 돌아가기 전에 되돌림
		public void Heat(GameObject blade, bool hot)
		{
			SpriteRenderer sprite = blade.GetComponentInChildren<SpriteRenderer>();
			if (hot == heated.ContainsKey(sprite))
				return;
			if (hot)
			{
				Prepare();
				heated[sprite] = sprite.sharedMaterial;
				sprite.sharedMaterial = hotMaterial;
				return;
			}
			sprite.sharedMaterial = heated[sprite];
			heated.Remove(sprite);
		}

		private void OnDisable()
		{
			foreach (KeyValuePair<SpriteRenderer, Material> pair in heated)
			{
				if (pair.Key != null)
					pair.Key.sharedMaterial = pair.Value;
			}
			heated.Clear();
			rings.Clear();
			landTime = float.NegativeInfinity;
			lastRadius = 0f;
			foreach (FxQuadBatch batch in batches)
				batch.Clear();
		}

		private void OnDestroy()
		{
			foreach (FxQuadBatch batch in batches)
				batch.Destroy();
			Destroy(hotMaterial);
		}

		private void LateUpdate()
		{
			Prepare();
			Camera viewCamera = Camera.main;
			root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
			foreach (FxQuadBatch batch in batches)
				batch.Begin();
			if (viewCamera != null)
			{
				for (int i = 0; i < rings.Count; i++)
					DrawRing(rings[i], i == 0, viewCamera.transform);
			}
			foreach (FxQuadBatch batch in batches)
				batch.Commit();
		}

		private void DrawRing(in Ring ring, bool main, Transform view)
		{
			float stepped = Mathf.Floor(Time.time * poseSteps) / poseSteps;
			if (main)
				WatchLanding(ring);
			float land = stepped - landTime;
			float burst = main ? Mathf.Clamp01(1f - land / landSeconds) : 0f;
			float calm = Mathf.Clamp01(1f - (ring.Radius - ring.RestRadius) / calmDistance);
			if (calm > 0.05f)
			{
				int frame = Mathf.FloorToInt(stepped * poseSteps * 0.5f);
				Swirl(groundBatch, ring, 0, frame, stepped, calm, burst);
				if (main)
					Swirl(swirlBatch, ring, 1, frame + 1, stepped, calm, burst);
			}
			if (main && land >= 0f && land < landSeconds)
				DrawLanding(ring, land, view);
			for (int i = 0; i < ring.Count; i++)
			{
				float angle = ShurikenStormRules.Angle(i, ring.Count, ring.Spin);
				DrawTrail(ring, angle, i);
				if (ring.Hot)
				{
					Vector3 position = ring.Center + Outward(angle) * ring.Radius + Vector3.up * ring.Height;
					sparkBatch.Add(position, view.right * sparkSize, view.up * sparkSize, FxQuadLook.Of(sparkTint));
				}
			}
		}

		// 흩어졌다 도는 반지름으로 돌아온 순간
		private void WatchLanding(in Ring ring)
		{
			bool away = ring.Radius > ring.RestRadius + 0.01f;
			if (away == false && lastRadius > ring.RestRadius + 0.01f)
				landTime = Time.time;
			lastRadius = ring.Radius;
		}

		private void Swirl(FxQuadBatch batch, in Ring ring, int layer, int frame, float now, float calm, float burst)
		{
			float half = ring.RestRadius * swirlSize[layer];
			Quaternion turn = Quaternion.Euler(0f, now * swirlDegrees[layer], 0f);
			float alpha = Mathf.Clamp01(swirlAlpha[layer] + burst * 0.4f) * calm * Mathf.Lerp(0.5f, 1f, ring.Strength);
			// 붓결 그림은 반시계로 감김. 표창과 같은 시계 방향으로 뒤집음
			batch.Add(ring.Center + Vector3.up * swirlHeight[layer], turn * Vector3.left * half, turn * Vector3.forward * half, FxQuadBatch.Cell(SWIRL_FRAME + frame % SWIRL_FRAMES, SIDE, SIDE), FxQuadLook.Of(new Color(1f, 1f, 1f, alpha), 1f));
		}

		private void DrawLanding(in Ring ring, float land, Transform view)
		{
			int frame = BRUSH_BURST_FRAME + Mathf.FloorToInt(land * poseSteps + 0.001f);
			Vector3 right = new(view.right.x, 0f, view.right.z);
			Vector3 forward = Vector3.Cross(right.normalized, Vector3.up);
			if (frame < SIDE * SIDE)
				swirlBatch.Add(ring.Center + Vector3.up * 0.3f, right.normalized * -landRadius, forward * landRadius, FxQuadBatch.Cell(frame, SIDE, SIDE), FxQuadLook.Of(Color.white, 1f));
		}

		// 도는 동안은 원을 따라, 흩어지거나 돌아올 때는 곧게
		private void DrawTrail(in Ring ring, float angle, int index)
		{
			float dash = Mathf.Clamp01((ring.Radius - ring.RestRadius) / 2f);
			float arc = trailDegrees * (1f - dash);
			float length = dashLength * dash * (ring.Outgoing ? -1f : 1f);
			float half = trailWidth * 0.5f * Mathf.Lerp(0.6f, 1f, ring.Strength);
			Rect row = new(0f, 1f - (index % TRAIL_ROWS + 1) / (float)TRAIL_ROWS, 1f, 1f / TRAIL_ROWS);
			FxQuadLook look = FxQuadLook.Of(new Color(1f, 1f, 1f, Mathf.Lerp(0.6f, 1f, ring.Strength)), 1f);
			Vector3 head = TrailPoint(ring, angle, ring.Radius);
			for (int i = 1; i <= trailSegments; i++)
			{
				float part = i / (float)trailSegments;
				Vector3 tail = TrailPoint(ring, angle - arc * part, ring.Radius + length * part);
				Vector3 along = (head - tail) * 0.5f;
				Vector3 side = Vector3.Cross(Vector3.up, along).normalized * half;
				float from = 1f - part;
				trailBatch.Add(tail + along, along, side, new Rect(from, row.y, 1f / trailSegments, row.height), look);
				head = tail;
			}
		}

		private static Vector3 TrailPoint(in Ring ring, float angle, float radius) => ring.Center + Outward(angle) * Mathf.Max(0f, radius) + Vector3.up * ring.Height;

		private static Vector3 Outward(float angle) => Quaternion.Euler(0f, angle, 0f) * Vector3.forward;

		private void Prepare()
		{
			if (root != null)
				return;
			root = new GameObject("ShurikenStormFx").transform;
			root.SetParent(transform, false);
			groundBatch = Batch("Ground", brushSheet, GROUND_QUEUE, false);
			swirlBatch = Batch("Swirl", brushSheet, SWIRL_QUEUE, false);
			trailBatch = Batch("Trail", trailTexture, TRAIL_QUEUE, false);
			sparkBatch = Batch("Spark", sparkTexture, FRONT_QUEUE, true);
			hotMaterial = new Material(hotShader) { name = "ShurikenStormHot" };
			hotMaterial.SetColor(COLOR_ID, hotColor);
		}

		private FxQuadBatch Batch(string label, Texture2D texture, int queue, bool onTop)
		{
			FxQuadBatch batch = new($"ShurikenStorm{label}", root, art.Shader, texture, art.Noise, queue, onTop);
			batches.Add(batch);
			return batch;
		}
	}
}

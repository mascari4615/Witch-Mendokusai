using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	// 판정 범위를 덮는 검 효과의 표현 방식
	public enum SwordSwingStyle
	{
		// 칼끝이 정면을 지날 때 초승달 검기가 떨어져 판정 끝까지 날아감
		FlyingWave,
		// 칼이 지나간 자리의 초승달 궤적을 판정 거리까지 키움
		ScaledTrail,
		// 판정 상자 안에 들어가는 베기 획 하나를 그림
		FittedStroke,
	}

	// 로우폴리 검을 월드 수평면에서 휘두르고, 판정 범위(SwordSwingArea)에 맞춘 각진 단색 효과를 짧게 남기는 연출
	// 판정과 독립된 수명, 소유자 위치를 따라가고 방향은 생성 시점 고정
	public partial class SwordSwing : MonoBehaviour
	{
		[SerializeField] private MeshFilter bladeFilter;
		[SerializeField] private MeshRenderer bladeRenderer;
		[SerializeField] private MeshFilter trailFilter;
		[SerializeField] private MeshRenderer trailRenderer;
		[SerializeField] private SwordSwingStyle style = SwordSwingStyle.ScaledTrail;

		[Header("Blade")]
		// 판정 거리에 맞춘 칼 길이의 하한과 상한
		[SerializeField, Min(0.1f)] private float minBladeLength = 0.5f;
		[SerializeField, Min(0.1f)] private float maxBladeLength = 0.85f;
		[SerializeField, Min(0.01f)] private float bladeWidth = 0.12f;
		[SerializeField, Min(0.005f)] private float bladeThickness = 0.03f;
		[SerializeField, Min(0.01f)] private float tipLength = 0.14f;
		[SerializeField, Min(0.01f)] private float guardWidth = 0.24f;
		[SerializeField, Min(0.01f)] private float guardDepth = 0.05f;
		[SerializeField, Min(0.01f)] private float gripLength = 0.2f;
		[SerializeField, Min(0.01f)] private float gripWidth = 0.05f;
		[SerializeField] private Color bladeTopColor = new(0.6f, 0.66f, 0.76f, 1f);
		[SerializeField] private Color bladeBottomColor = new(0.32f, 0.36f, 0.46f, 1f);
		[SerializeField] private Color guardColor = new(0.8f, 0.48f, 0.12f, 1f);
		[SerializeField] private Color gripColor = new(0.16f, 0.2f, 0.45f, 1f);

		[Header("Swing")]
		[SerializeField] private float handRadius = 0.3f;
		// 지면 위 높이, 휘두르기 면은 월드 수평
		[SerializeField, Min(0.05f)] private float height = 0.5f;
		// 판정 좌우 각도보다 좁게 휘두르지 않는 하한, 0도는 공격 방향
		[SerializeField, Range(0f, 180f)] private float minHalfAngle = 60f;
		// 판정 각도 밖에서 시작하는 예비 동작과 끝난 뒤 더 넘어가는 각도
		[SerializeField, Min(0f)] private float startOvershoot = 55f;
		[SerializeField, Min(0f)] private float endOvershoot = 0f;
		[SerializeField, Min(1f)] private float easeExponent = 3f;
		[SerializeField, Min(0.01f)] private float swingTime = 0.14f;
		[SerializeField, Min(0f)] private float holdTime = 0.08f;

		[Header("Trail")]
		[SerializeField, Min(0.01f)] private float trailTime = 0.07f;
		[SerializeField, Range(2, 48)] private int trailSegments = 24;
		// 칼날 길이 대비 손 쪽 궤적 안쪽 끝
		[SerializeField, Range(0f, 1f)] private float trailInner = 0.35f;
		// 궤적 확대 방식에서 판정 거리 대비 바깥 띠, 중간 띠, 밝은 가장자리 안쪽 끝
		[SerializeField] private Vector3 scaledTrailBands = new(0.35f, 0.62f, 0.9f);
		// 바깥 띠, 몸통, 밝은 심 순서의 단색
		[SerializeField] private Color glowColor = new(0.28f, 0.42f, 0.85f, 0.25f);
		[SerializeField] private Color bodyColor = new(0.55f, 0.72f, 1f, 0.55f);
		[SerializeField] private Color coreColor = new(0.94f, 0.97f, 1f, 0.95f);

		[Header("Flying wave")]
		[SerializeField, Min(0.01f)] private float waveTime = 0.18f;
		[SerializeField, Range(4, 32)] private int waveSegments = 16;
		// 판정 폭 대비 출발 때 양 끝 거리, 앞으로 휘는 깊이, 두께
		[SerializeField, Range(0.1f, 1f)] private float waveStartSpan = 0.75f;
		[SerializeField, Range(0f, 1f)] private float waveBend = 0.35f;
		[SerializeField, Range(0.05f, 1f)] private float waveThickness = 0.32f;
		[SerializeField, Range(0, 3)] private int waveEchoes = 2;
		[SerializeField, Range(0f, 0.5f)] private float waveEchoLag = 0.12f;

		[Header("Fitted stroke")]
		[SerializeField, Min(0.01f)] private float strokeReveal = 0.12f;
		[SerializeField, Min(0.01f)] private float strokeFade = 0.1f;
		[SerializeField, Range(4, 48)] private int strokeSegments = 24;
		// 크기와 무관하게 고정된 획 끝 길이와 굵기
		[SerializeField, Min(0.01f)] private float strokeTip = 0.45f;
		[SerializeField, Min(0.01f)] private float strokeWidth = 0.26f;
		// 판정 길이가 폭의 이 배수 이상이면 앞으로 뻗는 긴 획, 아니면 폭을 가로지르는 호
		[SerializeField, Min(0.1f)] private float strokeLongRatio = 1.2f;
		[SerializeField, Min(0f)] private float strokeMaxSagitta = 0.7f;
		[SerializeField, Min(0f)] private float strokeMargin = 0.08f;

		private const int MAX_QUADS = 192;

		private ObjectPoolManager pool;
		private GameObject owner;
		private Transform follow;
		private float elapsed;
		private float side = 1f;
		private float bladeLength;
		private float startAngle;
		private float endAngle;
		private SwordSwingArea area;
		private Mesh bladeMesh;

		public float Duration => Mathf.Max(swingTime + Mathf.Max(holdTime, trailTime), EffectEnd);

		private float HiltRadius => handRadius + guardDepth;
		private float BladeTip => HiltRadius + bladeLength;

		private void Awake()
		{
			BuildEffectMesh();
			foreach (MeshRenderer meshRenderer in new[] { bladeRenderer, trailRenderer })
			{
				meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
				meshRenderer.receiveShadows = false;
				meshRenderer.enabled = false;
			}
		}

		public void PlayPooled(ObjectPoolManager objectPool, GameObject attackOwner, Transform followTarget, SwordSwingArea hitArea)
		{
			pool = objectPool;
			owner = attackOwner;
			follow = followTarget;
			Prepare(hitArea);
			elapsed = 0;
			Evaluate(0);
		}

		// 풀 없이 호출 측 시간으로 Evaluate하는 연출도 같은 준비를 거침, 방향을 정한 뒤 호출
		public void Prepare(SwordSwingArea hitArea)
		{
			// 좌우 어느 방향이든 먼 쪽에서 카메라 쪽으로 베도록 회전 부호 결정
			side = Vector3.Dot(transform.right, Vector3.back) >= 0 ? 1f : -1f;
			FitArea(hitArea);
		}

		private void LateUpdate()
		{
			if (pool == null)
				return;
			elapsed += Time.deltaTime;
			if (owner == null || owner.activeInHierarchy == false || elapsed >= Duration)
			{
				pool.Despawn(gameObject);
				return;
			}
			if (follow != null)
			{
				Vector3 position = follow.position;
				position.y = transform.position.y;
				transform.position = position;
			}
			Evaluate(elapsed);
		}

		// 칼끝이 판정 끝에 닿도록 칼 길이를 정하고, 휘두르는 각을 판정 각도에 맞춤
		private void FitArea(SwordSwingArea hitArea)
		{
			area = hitArea;
			float length = Mathf.Clamp(area.OuterRadius - HiltRadius, minBladeLength, maxBladeLength);
			if (bladeMesh == null || Mathf.Approximately(length, bladeLength) == false)
			{
				bladeLength = length;
				if (bladeMesh != null)
					Destroy(bladeMesh);
				bladeMesh = BuildBladeMesh();
				bladeFilter.sharedMesh = bladeMesh;
			}
			float halfAngle = Mathf.Max(minHalfAngle, area.HalfAngle);
			startAngle = -(halfAngle + startOvershoot);
			endAngle = halfAngle + endOvershoot;
			if (style == SwordSwingStyle.FittedStroke)
				BuildStrokePath();
			float reach = Mathf.Max(BladeTip, area.OuterRadius, area.MaxZ) + height;
			effectMesh.bounds = new Bounds(Vector3.zero, Vector3.one * reach * 2);
		}

		public void Evaluate(float age)
		{
			bool visible = isActiveAndEnabled && age >= 0 && age < Duration;
			bladeRenderer.enabled = visible && age < swingTime + holdTime;
			trailRenderer.enabled = visible;
			if (visible == false)
				return;

			Vector3 pivot = Vector3.up * height;
			Vector3 direction = Direction(BladeAngle(age));
			Transform bladeTransform = bladeRenderer.transform;
			bladeTransform.localPosition = pivot + direction * handRadius;
			// 칼날 메시의 x축은 칼끝, y축은 월드 위
			bladeTransform.localRotation = Quaternion.LookRotation(Vector3.Cross(direction, Vector3.up), Vector3.up);
			UpdateEffect(age, pivot);
		}

		// 공격 방향 기준 칼의 각도, 오른쪽이 양수
		private float BladeAngle(float age)
		{
			float progress = Mathf.Clamp01(age / swingTime);
			float eased = 1f - Mathf.Pow(1f - progress, easeExponent);
			return Mathf.Lerp(startAngle, endAngle, eased) * side;
		}

		// 칼끝이 공격 방향을 지나는 시각, 검기와 베기 획의 출발점
		private float LaunchTime
		{
			get
			{
				float eased = Mathf.InverseLerp(startAngle, endAngle, 0f);
				return swingTime * (1f - Mathf.Pow(1f - eased, 1f / easeExponent));
			}
		}

		private float EffectEnd => style switch
		{
			SwordSwingStyle.FlyingWave => LaunchTime + waveTime,
			SwordSwingStyle.FittedStroke => LaunchTime + strokeReveal + strokeFade,
			_ => swingTime + trailTime,
		};

		private static Vector3 Direction(float angle) => Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward;

		private static float EaseOut(float value, float exponent) => 1f - Mathf.Pow(1f - Mathf.Clamp01(value), exponent);

		private static Color Fade(Color color, float alpha)
		{
			color.a *= alpha;
			return color;
		}

		private void OnDisable()
		{
			pool = null;
			owner = null;
			follow = null;
			if (bladeRenderer != null)
				bladeRenderer.enabled = false;
			if (trailRenderer != null)
				trailRenderer.enabled = false;
		}

		private void OnDestroy()
		{
			if (effectMesh != null)
				Destroy(effectMesh);
			if (bladeMesh != null)
				Destroy(bladeMesh);
		}

		// 손잡이, 가드, 마름모 단면 칼날의 면마다 단색인 로우폴리 검
		private Mesh BuildBladeMesh()
		{
			List<Vector3> vertices = new();
			List<Color> colors = new();
			List<int> triangles = new();

			void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
			{
				int start = vertices.Count;
				vertices.Add(a);
				vertices.Add(b);
				vertices.Add(c);
				for (int i = 0; i < 3; i++)
				{
					colors.Add(color);
					triangles.Add(start + i);
				}
			}

			void Box(Vector3 min, Vector3 max, Color top, Color sideColor)
			{
				Vector3[] corner = new Vector3[8];
				for (int i = 0; i < 8; i++)
					corner[i] = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
				int[,] faces = { { 2, 3, 7, 6 }, { 0, 4, 5, 1 }, { 0, 1, 3, 2 }, { 4, 6, 7, 5 }, { 0, 2, 6, 4 }, { 1, 5, 7, 3 } };
				for (int face = 0; face < 6; face++)
				{
					Color color = face == 0 ? top : sideColor;
					Triangle(corner[faces[face, 0]], corner[faces[face, 1]], corner[faces[face, 2]], color);
					Triangle(corner[faces[face, 0]], corner[faces[face, 2]], corner[faces[face, 3]], color);
				}
			}

			Color gripDark = gripColor * 0.7f;
			gripDark.a = 1f;
			Color guardDark = guardColor * 0.75f;
			guardDark.a = 1f;
			Box(new Vector3(-gripLength, -gripWidth * 0.5f, -gripWidth * 0.5f), new Vector3(0, gripWidth * 0.5f, gripWidth * 0.5f), gripColor, gripDark);
			Box(new Vector3(0, -guardDepth * 0.5f, -guardWidth * 0.5f), new Vector3(guardDepth, guardDepth * 0.5f, guardWidth * 0.5f), guardColor, guardDark);

			float baseX = guardDepth;
			float shoulderX = guardDepth + bladeLength - tipLength;
			float tipX = guardDepth + bladeLength;
			float halfWidth = bladeWidth * 0.5f;
			float halfThickness = bladeThickness * 0.5f;
			Vector3[] Ring(float x) => new[]
			{
				new Vector3(x, 0, halfWidth),
				new Vector3(x, halfThickness, 0),
				new Vector3(x, 0, -halfWidth),
				new Vector3(x, -halfThickness, 0),
			};
			Vector3[] root = Ring(baseX);
			Vector3[] shoulder = Ring(shoulderX);
			Vector3 tip = new(tipX, 0, 0);
			for (int i = 0; i < 4; i++)
			{
				int next = (i + 1) % 4;
				Color color = i < 2 ? bladeTopColor : bladeBottomColor;
				Triangle(root[i], shoulder[i], shoulder[next], color);
				Triangle(root[i], shoulder[next], root[next], color);
				Triangle(shoulder[i], tip, shoulder[next], color);
			}

			Mesh mesh = new() { name = "Low poly sword" };
			mesh.SetVertices(vertices);
			mesh.SetColors(colors);
			mesh.SetTriangles(triangles, 0);
			mesh.RecalculateBounds();
			return mesh;
		}
	}
}

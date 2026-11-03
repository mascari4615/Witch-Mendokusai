using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	// 로우폴리 검을 월드 수평면에서 휘두르고, 칼날이 지나간 자리만 각진 띠로 짧게 남기는 연출
	// 칼 길이, 휘두르는 각, 각도마다 궤적이 뻗는 거리는 실제 판정 범위(SwordSwingArea)에 맞춤
	// 판정과 독립된 수명, 소유자 위치를 따라가고 방향은 생성 시점 고정
	public class SwordSwing : MonoBehaviour
	{
		[SerializeField] private MeshFilter bladeFilter;
		[SerializeField] private MeshRenderer bladeRenderer;
		[SerializeField] private MeshFilter trailFilter;
		[SerializeField] private MeshRenderer trailRenderer;

		[Header("Blade")]
		// 판정 거리에 맞춘 칼 길이의 하한과 상한, 상한을 넘는 거리는 궤적이 칼끝 밖으로 뻗어 표시
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
		[SerializeField, Range(2, 32)] private int trailSegments = 8;
		// 칼날 길이 대비 궤적 안쪽 끝, 판정 끝의 밝은 테두리 폭
		[SerializeField, Range(0f, 1f)] private float trailInner = 0.35f;
		[SerializeField, Min(0.01f)] private float trailEdgeWidth = 0.15f;
		// 칼끝보다 먼 판정 구간은 몸통 색을 이 비율로 옅게 채움, 판정 밖 각도에서는 칼날 구간만 남김
		[SerializeField, Range(0f, 1f)] private float trailExtensionAlpha = 0.3f;
		// 칼날 쪽부터 꼬리 쪽 순서의 단계 색
		[SerializeField] private Color[] trailBodyColors =
		{
			new(0.72f, 0.86f, 1f, 0.55f),
			new(0.45f, 0.62f, 0.95f, 0.35f),
			new(0.28f, 0.38f, 0.75f, 0.18f),
		};
		[SerializeField] private Color[] trailEdgeColors =
		{
			new(1f, 1f, 1f, 0.95f),
			new(0.85f, 0.93f, 1f, 0.7f),
			new(0.6f, 0.75f, 1f, 0.4f),
		};

		// 칼날 구간, 칼끝 밖 판정 구간, 판정 끝 테두리
		private const int QUADS_PER_SEGMENT = 3;

		private Vector3[] trailVertices;
		private Color[] trailColors;
		private Mesh trailMesh;
		private Mesh bladeMesh;
		private ObjectPoolManager pool;
		private GameObject owner;
		private Transform follow;
		private float elapsed;
		private float side = 1f;
		private float bladeLength;
		private float startAngle;
		private float endAngle;
		private float trailInnerRadius;
		private float trailOuterRadius;
		private SwordSwingArea area;
		private readonly float[] reachProfile = new float[SwordSwingArea.REACH_SAMPLES];

		public float Duration => swingTime + Mathf.Max(holdTime, trailTime);

		private void Awake()
		{
			BuildTrailMesh();
			foreach (MeshRenderer meshRenderer in new[] { bladeRenderer, trailRenderer })
			{
				meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
				meshRenderer.receiveShadows = false;
				meshRenderer.enabled = false;
			}
		}

		public void PlayPooled(ObjectPoolManager objectPool, GameObject attackOwner, Transform followTarget, SwordSwingArea area)
		{
			pool = objectPool;
			owner = attackOwner;
			follow = followTarget;
			Prepare(area);
			elapsed = 0;
			Evaluate(0);
		}

		// 풀 없이 호출 측 시간으로 Evaluate하는 연출도 같은 준비를 거침, 방향을 정한 뒤 호출
		public void Prepare(SwordSwingArea area)
		{
			// 좌우 어느 방향이든 먼 쪽에서 카메라 쪽으로 베도록 회전 부호 결정
			side = Vector3.Dot(transform.right, Vector3.back) >= 0 ? 1f : -1f;
			FitArea(area);
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

		// 칼끝이 판정 끝에 닿도록 길이를 정하고, 칼보다 먼 판정은 궤적 바깥 반경으로 표시
		private void FitArea(SwordSwingArea hitArea)
		{
			area = hitArea.CopyReachTo(reachProfile);
			float hilt = handRadius + guardDepth;
			float length = Mathf.Clamp(area.OuterRadius - hilt, minBladeLength, maxBladeLength);
			if (bladeMesh == null || Mathf.Approximately(length, bladeLength) == false)
			{
				bladeLength = length;
				if (bladeMesh != null)
					Destroy(bladeMesh);
				bladeMesh = BuildBladeMesh();
				bladeFilter.sharedMesh = bladeMesh;
			}
			trailOuterRadius = Mathf.Max(hilt + bladeLength, area.OuterRadius);
			trailInnerRadius = Mathf.Clamp(Mathf.Max(area.InnerRadius, hilt + bladeLength * trailInner), hilt, trailOuterRadius);
			float halfAngle = Mathf.Max(minHalfAngle, area.HalfAngle);
			startAngle = -(halfAngle + startOvershoot);
			endAngle = halfAngle + endOvershoot;
			trailMesh.bounds = new Bounds(Vector3.zero, Vector3.one * (trailOuterRadius + height) * 2);
		}

		public void Evaluate(float age)
		{
			bool visible = isActiveAndEnabled && age >= 0 && age < Duration;
			bladeRenderer.enabled = visible && age < swingTime + holdTime;
			trailRenderer.enabled = visible && age < swingTime + trailTime;
			if (visible == false)
				return;

			Vector3 pivot = Vector3.up * height;
			Vector3 direction = Direction(BladeAngle(age));
			Transform bladeTransform = bladeRenderer.transform;
			bladeTransform.localPosition = pivot + direction * handRadius;
			// 칼날 메시의 x축은 칼끝, y축은 월드 위
			bladeTransform.localRotation = Quaternion.LookRotation(Vector3.Cross(direction, Vector3.up), Vector3.up);
			UpdateTrail(age, pivot);
		}

		// 공격 방향 기준 칼의 각도, 오른쪽이 양수
		private float BladeAngle(float age)
		{
			float progress = Mathf.Clamp01(age / swingTime);
			float eased = 1f - Mathf.Pow(1f - progress, easeExponent);
			return Mathf.Lerp(startAngle, endAngle, eased) * side;
		}

		private static Vector3 Direction(float angle) => Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward;

		private void UpdateTrail(float age, Vector3 pivot)
		{
			float head = Mathf.Min(age, swingTime);
			float tail = Mathf.Clamp(age - trailTime, 0f, head);
			float bladeTip = Mathf.Max(trailInnerRadius, handRadius + guardDepth + bladeLength);
			TrailRadii from = Radii(BladeAngle(tail), bladeTip);
			for (int i = 0; i < trailSegments; i++)
			{
				TrailRadii to = Radii(BladeAngle(Mathf.Lerp(tail, head, (float)(i + 1) / trailSegments)), bladeTip);
				// 칼날에 가까운 구간부터 단계 색, 구간 안에서는 단색
				int tier = Mathf.Min(trailBodyColors.Length - 1, (trailSegments - 1 - i) * trailBodyColors.Length / trailSegments);
				int vertex = i * QUADS_PER_SEGMENT * 4;
				Color body = trailBodyColors[tier];
				Color extension = body;
				extension.a *= trailExtensionAlpha;
				SetQuad(vertex, pivot, from, to, trailInnerRadius, trailInnerRadius, from.BodyEnd, to.BodyEnd, body);
				SetQuad(vertex + 4, pivot, from, to, from.BodyEnd, to.BodyEnd, from.Edge, to.Edge, extension);
				SetQuad(vertex + 8, pivot, from, to, from.Edge, to.Edge, from.Far, to.Far, trailEdgeColors[Mathf.Min(tier, trailEdgeColors.Length - 1)]);
				from = to;
			}
			trailMesh.vertices = trailVertices;
			trailMesh.colors = trailColors;
		}

		// 한 각도에서 칼날 구간 끝, 판정 끝 테두리 시작, 판정 끝까지의 거리
		private readonly struct TrailRadii
		{
			public readonly Vector3 Direction;
			public readonly float BodyEnd;
			public readonly float Edge;
			public readonly float Far;

			public TrailRadii(Vector3 direction, float bodyEnd, float edge, float far)
			{
				Direction = direction;
				BodyEnd = bodyEnd;
				Edge = edge;
				Far = far;
			}
		}

		// 판정이 닿는 각도는 판정 끝까지 뻗고 테두리를 그림, 판정 밖 각도는 칼날이 지나간 자리만 남김
		private TrailRadii Radii(float angle, float bladeTip)
		{
			Vector3 direction = Direction(angle);
			float reach = area.ReachAt(angle);
			if (reach <= 0f)
				return new TrailRadii(direction, bladeTip, bladeTip, bladeTip);
			float far = Mathf.Max(bladeTip, reach);
			float edge = Mathf.Max(trailInnerRadius, far - trailEdgeWidth);
			return new TrailRadii(direction, Mathf.Min(bladeTip, edge), edge, far);
		}

		private void SetQuad(int vertex, Vector3 pivot, TrailRadii from, TrailRadii to, float nearFrom, float nearTo, float farFrom, float farTo, Color color)
		{
			trailVertices[vertex] = pivot + from.Direction * nearFrom;
			trailVertices[vertex + 1] = pivot + from.Direction * farFrom;
			trailVertices[vertex + 2] = pivot + to.Direction * nearTo;
			trailVertices[vertex + 3] = pivot + to.Direction * farTo;
			for (int i = 0; i < 4; i++)
				trailColors[vertex + i] = color;
		}

		private void BuildTrailMesh()
		{
			int quads = trailSegments * QUADS_PER_SEGMENT;
			trailVertices = new Vector3[quads * 4];
			trailColors = new Color[quads * 4];
			int[] triangles = new int[quads * 6];
			for (int i = 0; i < quads; i++)
			{
				int vertex = i * 4;
				int triangle = i * 6;
				triangles[triangle] = vertex;
				triangles[triangle + 1] = vertex + 1;
				triangles[triangle + 2] = vertex + 2;
				triangles[triangle + 3] = vertex + 2;
				triangles[triangle + 4] = vertex + 1;
				triangles[triangle + 5] = vertex + 3;
			}
			trailMesh = new Mesh { name = "Sword swing trail" };
			trailMesh.MarkDynamic();
			trailMesh.vertices = trailVertices;
			trailMesh.colors = trailColors;
			trailMesh.triangles = triangles;
			trailFilter.sharedMesh = trailMesh;
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
			if (trailMesh != null)
				Destroy(trailMesh);
			if (bladeMesh != null)
				Destroy(bladeMesh);
		}
	}
}

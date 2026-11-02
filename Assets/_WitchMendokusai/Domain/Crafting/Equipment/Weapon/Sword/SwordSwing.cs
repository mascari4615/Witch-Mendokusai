using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	// 로우폴리 검을 월드 수평면에서 휘두르고, 칼날이 지나간 자리만 각진 띠로 짧게 남기는 연출
	// 판정과 독립된 수명, 소유자 위치를 따라가고 방향은 생성 시점 고정
	public class SwordSwing : MonoBehaviour
	{
		[SerializeField] private MeshFilter bladeFilter;
		[SerializeField] private MeshRenderer bladeRenderer;
		[SerializeField] private MeshFilter trailFilter;
		[SerializeField] private MeshRenderer trailRenderer;

		[Header("Blade")]
		[SerializeField, Min(0.1f)] private float bladeLength = 0.85f;
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
		// 0도는 공격 방향, 양수는 카메라 쪽
		[SerializeField] private float startAngle = -115f;
		[SerializeField] private float endAngle = 60f;
		[SerializeField, Min(1f)] private float easeExponent = 3f;
		[SerializeField, Min(0.01f)] private float swingTime = 0.14f;
		[SerializeField, Min(0f)] private float holdTime = 0.08f;

		[Header("Trail")]
		[SerializeField, Min(0.01f)] private float trailTime = 0.07f;
		[SerializeField, Range(2, 32)] private int trailSegments = 8;
		// 칼날 길이 대비 궤적 안쪽 끝과 밝은 테두리 시작
		[SerializeField, Range(0f, 1f)] private float trailInner = 0.35f;
		[SerializeField, Range(0f, 1f)] private float trailEdge = 0.85f;
		[SerializeField] private float trailOuter = 1.05f;
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

		private Vector3[] trailVertices;
		private Color[] trailColors;
		private Mesh trailMesh;
		private Mesh bladeMesh;
		private ObjectPoolManager pool;
		private GameObject owner;
		private Transform follow;
		private float elapsed;
		private float side = 1f;

		public float Duration => swingTime + Mathf.Max(holdTime, trailTime);

		private void Awake()
		{
			bladeMesh = BuildBladeMesh();
			bladeFilter.sharedMesh = bladeMesh;
			BuildTrailMesh();
			foreach (MeshRenderer meshRenderer in new[] { bladeRenderer, trailRenderer })
			{
				meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
				meshRenderer.receiveShadows = false;
				meshRenderer.enabled = false;
			}
		}

		public void PlayPooled(ObjectPoolManager objectPool, GameObject attackOwner, Transform followTarget)
		{
			pool = objectPool;
			owner = attackOwner;
			follow = followTarget;
			// 좌우 어느 방향이든 먼 쪽에서 카메라 쪽으로 베도록 회전 부호 결정
			side = Vector3.Dot(transform.right, Vector3.back) >= 0 ? 1f : -1f;
			elapsed = 0;
			Evaluate(0);
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

		public void Evaluate(float age)
		{
			bool visible = isActiveAndEnabled && age >= 0 && age < Duration;
			bladeRenderer.enabled = visible && age < swingTime + holdTime;
			trailRenderer.enabled = visible && age < swingTime + trailTime;
			if (visible == false)
				return;

			Vector3 pivot = Vector3.up * height;
			Vector3 direction = BladeDirection(age);
			Transform bladeTransform = bladeRenderer.transform;
			bladeTransform.localPosition = pivot + direction * handRadius;
			// 칼날 메시의 x축은 칼끝, y축은 월드 위
			bladeTransform.localRotation = Quaternion.LookRotation(Vector3.Cross(direction, Vector3.up), Vector3.up);
			UpdateTrail(age, pivot);
		}

		private Vector3 BladeDirection(float age)
		{
			float progress = Mathf.Clamp01(age / swingTime);
			float eased = 1f - Mathf.Pow(1f - progress, easeExponent);
			float angle = Mathf.Lerp(startAngle, endAngle, eased) * side;
			return Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward;
		}

		private void UpdateTrail(float age, Vector3 pivot)
		{
			float head = Mathf.Min(age, swingTime);
			float tail = Mathf.Clamp(age - trailTime, 0f, head);
			float reach = handRadius + guardDepth;
			float inner = reach + bladeLength * trailInner;
			float edge = reach + bladeLength * trailEdge;
			float outer = reach + bladeLength * trailOuter;
			for (int i = 0; i < trailSegments; i++)
			{
				Vector3 from = BladeDirection(Mathf.Lerp(tail, head, (float)i / trailSegments));
				Vector3 to = BladeDirection(Mathf.Lerp(tail, head, (float)(i + 1) / trailSegments));
				// 칼날에 가까운 구간부터 단계 색, 구간 안에서는 단색
				int tier = Mathf.Min(trailBodyColors.Length - 1, (trailSegments - 1 - i) * trailBodyColors.Length / trailSegments);
				int vertex = i * 8;
				SetQuad(vertex, pivot, from, to, inner, edge, trailBodyColors[tier]);
				SetQuad(vertex + 4, pivot, from, to, edge, outer, trailEdgeColors[Mathf.Min(tier, trailEdgeColors.Length - 1)]);
			}
			trailMesh.vertices = trailVertices;
			trailMesh.colors = trailColors;
		}

		private void SetQuad(int vertex, Vector3 pivot, Vector3 from, Vector3 to, float near, float far, Color color)
		{
			trailVertices[vertex] = pivot + from * near;
			trailVertices[vertex + 1] = pivot + from * far;
			trailVertices[vertex + 2] = pivot + to * near;
			trailVertices[vertex + 3] = pivot + to * far;
			for (int i = 0; i < 4; i++)
				trailColors[vertex + i] = color;
		}

		private void BuildTrailMesh()
		{
			int quads = trailSegments * 2;
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
			float reach = handRadius + guardDepth + bladeLength * trailOuter + height;
			trailMesh.bounds = new Bounds(Vector3.zero, Vector3.one * reach * 2);
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

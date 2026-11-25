using UnityEngine;

namespace WitchMendokusai
{
	// 초승달 베기 궤적. 바닥에 눕힌 호 메시를 만들고 켜질 때마다 셰이더 (WM/ArcSlash) 의 _Progress 를 0 에서 1 로 올림
	// 펼쳐짐, 꼬리 녹음, 가늘어짐은 셰이더가 그림. 이 부품은 모양과 수명만 앎
	// 쓰는 곳: 알리사 완드 빛 베기 (레퍼런스 memo wm/design/wand-attack-fx-20261123)
	[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
	public class ArcSlashMesh : MonoBehaviour
	{
		private static readonly int PROGRESS_ID = Shader.PropertyToID("_Progress");

		[SerializeField, Min(0.1f), Tooltip("바깥 날의 반지름")] private float radius = 2.6f;
		[SerializeField, Range(10f, 360f)] private float arcDegrees = 150f;
		[SerializeField, Min(0.01f), Tooltip("가운데의 폭. 양 끝으로 갈수록 0")] private float width = 0.9f;
		[SerializeField, Range(0.1f, 2f), Tooltip("작을수록 끝까지 두꺼움")] private float taperPower = 0.6f;
		[SerializeField, Range(4, 96)] private int segments = 40;
		[SerializeField, Tooltip("켜면 오른쪽에서 왼쪽으로 휘두름")] private bool mirror;
		[SerializeField, Min(0.01f)] private float duration = 0.3f;

		private MeshRenderer meshRenderer;
		private MaterialPropertyBlock propertyBlock;
		private Mesh mesh;
		private float elapsed;

		private void Awake()
		{
			mesh = BuildMesh();
			GetComponent<MeshFilter>().sharedMesh = mesh;
			meshRenderer = GetComponent<MeshRenderer>();
			propertyBlock = new MaterialPropertyBlock();
		}

		private void OnDestroy() => Destroy(mesh);

		private void OnEnable()
		{
			elapsed = 0f;
			Apply(0f);
		}

		private void Update()
		{
			elapsed += Time.deltaTime;
			Apply(Mathf.Clamp01(elapsed / duration));
		}

		private void Apply(float progress)
		{
			propertyBlock.SetFloat(PROGRESS_ID, progress);
			meshRenderer.SetPropertyBlock(propertyBlock);
		}

		// u 는 호를 따라 0 (시작) 에서 1 (머리), v 는 0 안쪽, 1 바깥 날. 앞 (+Z) 을 가운데로 두고 좌우로 펼침
		private Mesh BuildMesh()
		{
			int columns = segments + 1;
			Vector3[] vertices = new Vector3[columns * 2];
			Vector2[] uvs = new Vector2[columns * 2];
			int[] triangles = new int[segments * 6];
			float side = mirror ? -1f : 1f;
			float startRadians = (90f + side * arcDegrees * 0.5f) * Mathf.Deg2Rad;
			float sweepRadians = side * arcDegrees * Mathf.Deg2Rad;
			for (int i = 0; i < columns; i++)
			{
				float u = (float)i / segments;
				float angle = startRadians - sweepRadians * u;
				Vector3 direction = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
				float thickness = width * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * u)), taperPower);
				vertices[i * 2] = direction * (radius - thickness);
				vertices[i * 2 + 1] = direction * radius;
				uvs[i * 2] = new Vector2(u, 0f);
				uvs[i * 2 + 1] = new Vector2(u, 1f);
			}
			for (int i = 0; i < segments; i++)
			{
				int inner = i * 2;
				int triangle = i * 6;
				triangles[triangle] = inner;
				triangles[triangle + 1] = inner + 1;
				triangles[triangle + 2] = inner + 2;
				triangles[triangle + 3] = inner + 1;
				triangles[triangle + 4] = inner + 3;
				triangles[triangle + 5] = inner + 2;
			}
			Mesh built = new() { name = "ArcSlash" };
			built.SetVertices(vertices);
			built.SetUVs(0, uvs);
			built.SetTriangles(triangles, 0);
			built.RecalculateBounds();
			return built;
		}
	}
}

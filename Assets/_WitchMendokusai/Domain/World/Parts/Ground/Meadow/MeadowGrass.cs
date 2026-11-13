using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	// 지면 조각 위 풀. 풀 묶음 메시 하나를 카메라 시선 둘레 칸마다 GPU 인스턴싱으로 반복
	// 칸마다 90도 단위 회전, 색과 키는 셰이더의 세계 좌표 노이즈 (반복 감추기)
	// 근거와 판정: memo wm/design/art/stage-frames-20261113.md (W 풀, forest4), 계획 memo changes/wm-meadow-look.md
	public class MeadowGrass : MonoBehaviour
	{
		[SerializeField] private Material grassMaterial;
		[SerializeField, Min(1f)] private float patchSize = 10f;
		[SerializeField, Min(1)] private int bladesPerPatch = 6000;
		[SerializeField, Min(1f)] private float drawRadius = 45f;
		[SerializeField] private Vector2 bladeHeight = new(0.22f, 0.42f);
		[SerializeField] private Vector2 bladeWidth = new(0.035f, 0.07f);
		[SerializeField] private Vector2 bladeBend = new(0.5f, 0.85f);
		[SerializeField] private Vector2 windDirection = new(0.55f, 0.35f);
		[SerializeField] private int seed = 5;

		[Header("Mobile")]
		[SerializeField, Range(0.05f, 1f)] private float mobileDensity = 0.35f;
		[SerializeField, Min(1f)] private float mobileDrawRadius = 30f;

		private const int MAX_INSTANCES_PER_CALL = 1023;
		private static readonly Dictionary<int, Mesh> patchMeshes = new();
		private readonly List<Matrix4x4> matrices = new();

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetMeshes() => patchMeshes.Clear();

		private bool IsMobile => Application.isMobilePlatform;
		private float Radius => IsMobile ? mobileDrawRadius : drawRadius;
		private int BladeCount => IsMobile ? Mathf.Max(1, Mathf.RoundToInt(bladesPerPatch * mobileDensity)) : bladesPerPatch;

		private void LateUpdate()
		{
			Camera viewCamera = Camera.main;
			if (viewCamera == null || grassMaterial == null)
				return;

			Vector3 focus = GroundFocus(viewCamera);
			float halfSize = transform.lossyScale.x * 5f;
			Vector3 center = transform.position;
			float radius = Radius;
			if (Mathf.Abs(focus.x - center.x) > halfSize + radius || Mathf.Abs(focus.z - center.z) > halfSize + radius)
				return;

			matrices.Clear();
			int cells = Mathf.Max(1, Mathf.RoundToInt(halfSize * 2f / patchSize));
			for (int i = 0; i < cells; i++)
			{
				for (int j = 0; j < cells; j++)
				{
					Vector3 cellCenter = new(center.x - halfSize + (i + 0.5f) * patchSize, center.y, center.z - halfSize + (j + 0.5f) * patchSize);
					if ((cellCenter - focus).sqrMagnitude > radius * radius)
						continue;
					int turn = CellHash(Mathf.RoundToInt(cellCenter.x / patchSize), Mathf.RoundToInt(cellCenter.z / patchSize)) & 3;
					matrices.Add(Matrix4x4.TRS(cellCenter, Quaternion.Euler(0f, turn * 90f, 0f), Vector3.one));
				}
			}
			if (matrices.Count == 0)
				return;

			Mesh mesh = PatchMesh();
			RenderParams renderParams = new(grassMaterial)
			{
				shadowCastingMode = ShadowCastingMode.Off,
				receiveShadows = true,
				layer = gameObject.layer,
				worldBounds = new Bounds(center + Vector3.up, new Vector3(halfSize * 2f, 4f, halfSize * 2f)),
			};
			for (int start = 0; start < matrices.Count; start += MAX_INSTANCES_PER_CALL)
				Graphics.RenderMeshInstanced(renderParams, mesh, 0, matrices, Mathf.Min(MAX_INSTANCES_PER_CALL, matrices.Count - start), start);
		}

		// 카메라 시선이 지면과 만나는 점. 게임 시점과 낮은 시점 모두 대응
		private Vector3 GroundFocus(Camera viewCamera)
		{
			Transform cameraTransform = viewCamera.transform;
			Vector3 forward = cameraTransform.forward;
			float groundY = transform.position.y;
			if (forward.y < -0.05f)
			{
				float distance = (cameraTransform.position.y - groundY) / -forward.y;
				return cameraTransform.position + forward * Mathf.Min(distance, Radius);
			}
			return new Vector3(cameraTransform.position.x, groundY, cameraTransform.position.z) + new Vector3(forward.x, 0f, forward.z).normalized * Radius * 0.5f;
		}

		private static int CellHash(int x, int z)
		{
			unchecked
			{
				int hash = x * 73856093 ^ z * 19349663;
				return hash ^ (hash >> 13);
			}
		}

		private Mesh PatchMesh()
		{
			int bladeCount = BladeCount;
			int key = bladeCount * 31 + seed;
			if (patchMeshes.TryGetValue(key, out Mesh cached) && cached != null)
				return cached;

			System.Random random = new(seed);
			float RandomRange(Vector2 range) => Mathf.Lerp(range.x, range.y, (float)random.NextDouble());
			List<Vector3> vertices = new(bladeCount * 5);
			List<Color32> colors = new(bladeCount * 5);
			List<Vector2> roots = new(bladeCount * 5);
			List<int> triangles = new(bladeCount * 9);
			Color32 rootColor = new(0, 0, 0, 0);
			Color32 midColor = new(0, 0, 0, 140);
			Color32 tipColor = new(0, 0, 0, 255);
			float half = patchSize * 0.5f;
			for (int blade = 0; blade < bladeCount; blade++)
			{
				Vector3 root = new((float)random.NextDouble() * patchSize - half, 0f, (float)random.NextDouble() * patchSize - half);
				float height = RandomRange(bladeHeight);
				float width = RandomRange(bladeWidth);
				float angle = (float)random.NextDouble() * Mathf.PI * 2f;
				Vector3 side = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * width;
				float bend = RandomRange(bladeBend);
				Vector3 lean = new Vector3(windDirection.x + ((float)random.NextDouble() - 0.5f) * 0.5f, 0f, windDirection.y + ((float)random.NextDouble() - 0.5f) * 0.5f) * height * bend;
				Vector3 middle = root + Vector3.up * height * 0.6f + lean * 0.22f;
				int first = vertices.Count;
				vertices.Add(root - side);
				vertices.Add(root + side);
				vertices.Add(middle - side * 0.7f);
				vertices.Add(middle + side * 0.7f);
				vertices.Add(root + Vector3.up * height + lean);
				colors.Add(rootColor);
				colors.Add(rootColor);
				colors.Add(midColor);
				colors.Add(midColor);
				colors.Add(tipColor);
				Vector2 rootXZ = new(root.x, root.z);
				for (int k = 0; k < 5; k++)
					roots.Add(rootXZ);
				triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 1);
				triangles.Add(first + 1); triangles.Add(first + 2); triangles.Add(first + 3);
				triangles.Add(first + 2); triangles.Add(first + 4); triangles.Add(first + 3);
			}
			Mesh mesh = new() { name = "MeadowGrassPatch", indexFormat = IndexFormat.UInt32 };
			mesh.SetVertices(vertices);
			mesh.SetColors(colors);
			mesh.SetUVs(0, roots);
			mesh.SetTriangles(triangles, 0);
			mesh.RecalculateBounds();
			patchMeshes[key] = mesh;
			return mesh;
		}
	}
}

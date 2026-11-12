using UnityEngine;

namespace WitchMendokusai
{
	// 시작 화면 지형. 가운데 언덕, 잔물결 기복, 멀리 둘러싼 능선을 높이 함수로 만들어 메시 생성
	// 메시는 자산으로 저장하지 않고 켜질 때 생성
	[ExecuteAlways]
	[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
	public class TitleTerrain : MonoBehaviour
	{
		[Header("Grid")]
		[SerializeField] private float size = 360f;
		[SerializeField] private int resolution = 160;
		[SerializeField] private float uvTile = 8f;

		[Header("Hill")]
		[SerializeField] private Vector2 hillCenter = new(0f, 0f);
		[SerializeField] private float hillRadius = 34f;
		[SerializeField] private float hillHeight = 8f;

		[Header("Rolling")]
		[SerializeField] private int seed = 4615;
		[SerializeField] private float noiseScale = 0.022f;
		[SerializeField] private float noiseHeight = 2.2f;

		[Header("Far ridge")]
		[SerializeField] private float ridgeStart = 95f;
		[SerializeField] private float ridgeEnd = 175f;
		[SerializeField] private float ridgeHeight = 26f;
		[SerializeField] private float ridgeNoiseScale = 0.012f;

		[Header("Camera clearing")]
		[Tooltip("카메라 앞쪽 (음의 z) 을 평평하게 눌러 시야 확보")]
		[SerializeField] private float frontFlattenStart = -30f;
		[SerializeField] private float frontFlattenEnd = -90f;
		[SerializeField] private float frontDepth = 1.5f;

		private void Awake() => Build();

		public float HeightAt(float x, float z)
		{
			float offset = seed * 0.137f;
			float hillDistance = Vector2.Distance(new Vector2(x, z), hillCenter);
			float hill = hillHeight * Mathf.Exp(-(hillDistance / hillRadius) * (hillDistance / hillRadius));
			float rolling = (Mathf.PerlinNoise(x * noiseScale + offset, z * noiseScale + offset) - 0.5f) * 2f * noiseHeight;
			float distance = new Vector2(x, z).magnitude;
			float ridgeShape = 0.55f + 0.45f * Mathf.PerlinNoise(x * ridgeNoiseScale + offset * 2f, z * ridgeNoiseScale - offset);
			float ridge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ridgeStart, ridgeEnd, distance)) * ridgeHeight * ridgeShape;
			float front = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(frontFlattenStart, frontFlattenEnd, z)) * frontDepth;
			return hill + rolling + ridge - front;
		}

		public Vector3 SurfacePoint(float x, float z) => transform.TransformPoint(new Vector3(x, HeightAt(x, z), z));

		[ContextMenu("Build")]
		public void Build()
		{
			int count = resolution + 1;
			Vector3[] vertices = new Vector3[count * count];
			Vector2[] uvs = new Vector2[vertices.Length];
			int[] triangles = new int[resolution * resolution * 6];
			float half = size * 0.5f;
			float step = size / resolution;
			for (int j = 0; j < count; j++)
			{
				for (int i = 0; i < count; i++)
				{
					float x = -half + i * step;
					float z = -half + j * step;
					int index = j * count + i;
					vertices[index] = new Vector3(x, HeightAt(x, z), z);
					uvs[index] = new Vector2(x / size * uvTile, z / size * uvTile);
				}
			}
			int triangle = 0;
			for (int j = 0; j < resolution; j++)
			{
				for (int i = 0; i < resolution; i++)
				{
					int corner = j * count + i;
					triangles[triangle++] = corner;
					triangles[triangle++] = corner + count;
					triangles[triangle++] = corner + 1;
					triangles[triangle++] = corner + 1;
					triangles[triangle++] = corner + count;
					triangles[triangle++] = corner + count + 1;
				}
			}
			Mesh mesh = new() { name = "TitleTerrain", hideFlags = HideFlags.DontSave };
			mesh.indexFormat = vertices.Length > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
			mesh.vertices = vertices;
			mesh.uv = uvs;
			mesh.triangles = triangles;
			mesh.RecalculateNormals();
			mesh.RecalculateBounds();
			GetComponent<MeshFilter>().sharedMesh = mesh;
		}
	}
}

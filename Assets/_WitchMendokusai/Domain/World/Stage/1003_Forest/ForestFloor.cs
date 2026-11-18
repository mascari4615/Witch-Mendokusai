using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	// 숲 스테이지 바닥. 높이 함수로 지형 메시와 충돌체, 길 제어 텍스처, 풀잎, 꽃, 고사리, 노란 꽃대를 켜질 때 생성
	// 풀과 꽃은 수백만 장이라 자산으로 저장하지 않음. 생성식은 숲 시안 forestz21 (memo wm/design/style-pass-20261116)
	// 나무, 바위, 자갈은 같은 높이 함수로 굽은 프리팹 자식 (memo wm/design/lab-forest-stage-20261118)
	[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
	// 지형 충돌체를 땅으로 표시. 카메라 가림 해소 (CinemachineComponentDeoccluder) 가 언덕 앞에서 카메라를 당기고 언덕 위로 올림
	[RequireComponent(typeof(GroundSurface), typeof(CameraLiftGround))]
	public partial class ForestFloor : MonoBehaviour
	{
		[Header("Terrain")]
		[SerializeField] private Vector2 terrainOrigin = new(-85f, -30f);
		[SerializeField] private float terrainSize = 170f;
		[SerializeField] private int terrainResolution = 480;
		[SerializeField] private int controlResolution = 1024;

		[Header("Height")]
		[SerializeField] private float baseHeight = 2f;
		[SerializeField] private float rollingScale = 0.02f;
		[SerializeField] private float rollingHeight = 8f;
		[Tooltip("길에서 이 거리 (m) 부터 옆 언덕이 오르기 시작")]
		[SerializeField] private float hillStart = 18f;
		[SerializeField] private float hillRamp = 40f;
		[SerializeField] private float hillHeight = 16f;
		[Tooltip("이 z (m) 부터 먼 능선이 오르기 시작")]
		[SerializeField] private float farStart = 50f;
		[SerializeField] private float farRamp = 90f;
		[SerializeField] private float farHeight = 20f;
		[SerializeField] private float hillNoiseScale = 0.01f;
		[Tooltip("언덕 높이에 곱하는 잡음 범위")]
		[SerializeField] private Vector2 hillNoiseRange = new(0.6f, 1.4f);
		[SerializeField] private float farNoiseScale = 0.012f;
		[SerializeField] private float detailScale = 0.2f;
		[SerializeField] private float detailHeight = 0.15f;
		[Tooltip("이 z (m) 부터 잔기복 없이 매끈한 먼 배경")]
		[SerializeField] private float backgroundStartZ = 55f;
		[SerializeField] private float backgroundRampZ = 25f;
		[Tooltip("길 축에서 이 |x| (m) 부터 매끈한 배경")]
		[SerializeField] private float backgroundStartX = 45f;
		[SerializeField] private float backgroundRampX = 20f;

		[Header("Path")]
		[SerializeField] private float pathWaveA = 9f;
		[SerializeField] private float pathFrequencyA = 0.045f;
		[SerializeField] private float pathWaveB = 6f;
		[SerializeField] private float pathFrequencyB = 0.013f;
		[Tooltip("길 가운데에서 이 거리 (m) 까지 길 높이로 깎음")]
		[SerializeField] private Vector2 pathCutRange = new(2.2f, 3.6f);
		[SerializeField] private float pathSink = 0.3f;
		[Tooltip("흙길 텍스처가 섞이는 거리 (m)")]
		[SerializeField] private Vector2 pathPaintRange = new(0.4f, 3.6f);
		[Tooltip("길 옆 흙 섞인 풀이 끝나는 거리 (m)")]
		[SerializeField] private Vector2 pathEdgeRange = new(1.5f, 9f);
		[SerializeField] private float pathEdgeNoiseScale = 0.25f;
		[SerializeField] private float pathEdgeNoiseStrength = 2f;
		[SerializeField] private float pathEdgeWideNoiseScale = 0.08f;
		[SerializeField] private float pathEdgeWideNoiseStrength = 2.5f;
		[Tooltip("풀밭 사이 흙 얼룩 잡음")]
		[SerializeField] private float earthPatchScale = 0.045f;
		[SerializeField] private Vector2 earthPatchRange = new(0.3f, 0.8f);
		[SerializeField, Range(0f, 1f)] private float earthPatchStrength = 0.55f;

		[Header("Noise seeds")]
		[SerializeField] private int rollingSeed = 3;
		[SerializeField] private int hillSeed = 9;
		[SerializeField] private int farSeed = 21;
		[SerializeField] private int detailSeed = 67;
		[SerializeField] private int pathEdgeSeed = 41;
		[SerializeField] private int pathEdgeWideSeed = 43;
		[SerializeField] private int earthPatchSeed = 77;

		private bool isBuilt;
		private Color32[] controlPixels;
		private readonly List<Object> generated = new();

		private void Awake() => Build();

		// 생성한 메시와 텍스처는 자산이 아니라 스테이지가 사라질 때 같이 지움
		private void OnDestroy()
		{
			foreach (Object item in generated)
				Destroy(item);
			generated.Clear();
		}

		public void Build()
		{
			if (isBuilt)
				return;
			isBuilt = true;
			BuildTerrain();
			BuildControl();
			BuildGrass();
			BuildFlowers();
			BuildFerns();
			BuildSpikes();
		}

		public float PathX(float z) => Mathf.Sin(z * pathFrequencyA) * pathWaveA + Mathf.Sin(z * pathFrequencyB + 1f) * pathWaveB;

		public float DistanceToPath(float x, float z) => Mathf.Abs(x - PathX(z));

		public float HeightAt(float x, float z)
		{
			float continuous = HeightContinuous(x, z);
			float detailed = continuous + Fractal(x * detailScale, z * detailScale, detailSeed, 2) * detailHeight;
			float background = Mathf.Clamp01(Mathf.Clamp01((z - backgroundStartZ) / backgroundRampZ) + Mathf.Clamp01((Mathf.Abs(x) - backgroundStartX) / backgroundRampX));
			float height = Mathf.Lerp(detailed, continuous, background);
			float pathMask = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(pathCutRange.x, pathCutRange.y, DistanceToPath(x, z)));
			return Mathf.Lerp(height, continuous - pathSink, pathMask);
		}

		private float HeightContinuous(float x, float z)
		{
			float rolling = Fractal(x * rollingScale, z * rollingScale, rollingSeed, 3) * rollingHeight;
			float hills = Mathf.Clamp01((Mathf.Abs(x) - hillStart) / hillRamp);
			hills = hills * hills * hillHeight * Mathf.Lerp(hillNoiseRange.x, hillNoiseRange.y, Fractal(x * hillNoiseScale, z * hillNoiseScale, hillSeed, 3));
			float far = Mathf.Clamp01((z - farStart) / farRamp) * farHeight * Fractal(x * farNoiseScale + 5f, z * farNoiseScale, farSeed, 4);
			return baseHeight + rolling + hills + far;
		}

		private void BuildTerrain()
		{
			int count = terrainResolution + 1;
			float step = terrainSize / terrainResolution;
			Vector3[] vertices = new Vector3[count * count];
			int[] triangles = new int[terrainResolution * terrainResolution * 6];
			for (int j = 0; j < count; j++)
			{
				for (int i = 0; i < count; i++)
				{
					float x = terrainOrigin.x + i * step;
					float z = terrainOrigin.y + j * step;
					vertices[j * count + i] = new Vector3(x, HeightAt(x, z), z);
				}
			}
			int triangle = 0;
			for (int j = 0; j < terrainResolution; j++)
			{
				for (int i = 0; i < terrainResolution; i++)
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
			Mesh mesh = new() { name = "ForestTerrain", hideFlags = HideFlags.DontSave };
			mesh.indexFormat = IndexFormat.UInt32;
			mesh.vertices = vertices;
			mesh.triangles = triangles;
			mesh.RecalculateNormals();
			mesh.RecalculateBounds();
			generated.Add(mesh);
			GetComponent<MeshFilter>().sharedMesh = mesh;
			GetComponent<MeshCollider>().sharedMesh = mesh;
		}

		// 채널: R 풀, G 흙 섞인 풀, B 흙길, A 바위 (지금은 0)
		private void BuildControl()
		{
			controlPixels = new Color32[controlResolution * controlResolution];
			for (int j = 0; j < controlResolution; j++)
			{
				for (int i = 0; i < controlResolution; i++)
				{
					float x = terrainOrigin.x + (i + 0.5f) / controlResolution * terrainSize;
					float z = terrainOrigin.y + (j + 0.5f) / controlResolution * terrainSize;
					float edgeNoise = Fractal(x * pathEdgeNoiseScale, z * pathEdgeNoiseScale, pathEdgeSeed, 3) - 0.5f;
					float edgeNoiseWide = Fractal(x * pathEdgeWideNoiseScale, z * pathEdgeWideNoiseScale, pathEdgeWideSeed, 3) - 0.5f;
					float distance = DistanceToPath(x, z) + edgeNoise * pathEdgeNoiseStrength + edgeNoiseWide * pathEdgeWideNoiseStrength;
					float path = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(pathPaintRange.x, pathPaintRange.y, distance));
					float edge = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(pathEdgeRange.x, pathEdgeRange.y, distance));
					float patch = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(earthPatchRange.x, earthPatchRange.y, Fractal(x * earthPatchScale, z * earthPatchScale, earthPatchSeed, 4)));
					float earthWeight = Mathf.Max(edge - path, 0f) + patch * earthPatchStrength * (1f - edge);
					float grassWeight = Mathf.Max(0f, 1f - earthWeight - path);
					float sum = grassWeight + earthWeight + path;
					controlPixels[j * controlResolution + i] = new Color32((byte)(grassWeight / sum * 255f), (byte)(earthWeight / sum * 255f), (byte)(path / sum * 255f), 0);
				}
			}
			Texture2D control = new(controlResolution, controlResolution, TextureFormat.RGBA32, false, true)
			{
				name = "ForestControl",
				hideFlags = HideFlags.DontSave,
				wrapMode = TextureWrapMode.Clamp,
				filterMode = FilterMode.Bilinear
			};
			generated.Add(control);
			control.SetPixels32(controlPixels);
			control.Apply(false);
			MaterialPropertyBlock block = new();
			MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
			meshRenderer.GetPropertyBlock(block);
			block.SetTexture("_Control", control);
			block.SetVector("_WorldSize", new Vector4(terrainOrigin.x, terrainOrigin.y, terrainSize, terrainSize));
			meshRenderer.SetPropertyBlock(block);
		}

		private Color32 ControlAt(float x, float z)
		{
			int column = Mathf.Clamp((int)((x - terrainOrigin.x) / terrainSize * controlResolution), 0, controlResolution - 1);
			int row = Mathf.Clamp((int)((z - terrainOrigin.y) / terrainSize * controlResolution), 0, controlResolution - 1);
			return controlPixels[row * controlResolution + column];
		}

		private GameObject AddChildMesh(string objectName, Mesh mesh, Material material)
		{
			generated.Add(mesh);
			GameObject child = new(objectName);
			child.transform.SetParent(transform, false);
			child.AddComponent<MeshFilter>().sharedMesh = mesh;
			MeshRenderer childRenderer = child.AddComponent<MeshRenderer>();
			childRenderer.sharedMaterial = material;
			childRenderer.shadowCastingMode = ShadowCastingMode.Off;
			return child;
		}

		private static float Hash(int x, int y, int seed)
		{
			unchecked
			{
				int hash = x * 374761393 + y * 668265263 + seed * 2147483647;
				hash = (hash ^ (hash >> 13)) * 1274126177;
				return ((hash ^ (hash >> 16)) & 0xffff) / 65535f;
			}
		}

		private static float ValueNoise(float x, float y, int seed)
		{
			int cellX = Mathf.FloorToInt(x);
			int cellY = Mathf.FloorToInt(y);
			float fractionX = x - cellX;
			float fractionY = y - cellY;
			fractionX = fractionX * fractionX * (3f - 2f * fractionX);
			fractionY = fractionY * fractionY * (3f - 2f * fractionY);
			float bottom = Mathf.Lerp(Hash(cellX, cellY, seed), Hash(cellX + 1, cellY, seed), fractionX);
			float top = Mathf.Lerp(Hash(cellX, cellY + 1, seed), Hash(cellX + 1, cellY + 1, seed), fractionX);
			return Mathf.Lerp(bottom, top, fractionY);
		}

		public static float Fractal(float x, float y, int seed, int octaves)
		{
			float sum = 0f;
			float amplitude = 0.5f;
			float frequency = 1f;
			for (int i = 0; i < octaves; i++)
			{
				sum += ValueNoise(x * frequency, y * frequency, seed + i * 17) * amplitude;
				frequency *= 2f;
				amplitude *= 0.5f;
			}
			return sum;
		}
	}
}

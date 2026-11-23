using UnityEngine;

namespace WitchMendokusai
{
	// 숲속 빈터 연구소. 빈터 윤곽 하나로 바닥 칠, 숲 경계 벽, 나무 배치 (굽는 도구) 가 같은 모양을 씀
	// 바닥 칠은 숲 지형 셰이더의 제어 텍스처 (R 풀, G 흙 섞인 풀, B 흙길) 를 켜질 때 생성. 지형 메시는 TitleTerrain
	// 판정과 실험 기록: memo wm/design/lab-surround-20261123
	public class LabClearing : MonoBehaviour
	{
		[Header("Clearing")]
		[Tooltip("빈터 직사각형 (x 최소, z 최소, 너비, 깊이). 실험실 루트 기준 m")]
		[SerializeField] private Rect clearing = new(-21.1f, -22.4f, 42.2f, 48.8f);
		[Tooltip("관제홀 바닥 (x 최소, z 최소, 너비, 깊이)")]
		[SerializeField] private Rect hallFootprint = new(-14.1f, -4.4f, 28.2f, 23.8f);
		[Tooltip("윤곽 둥글기 반지름 (m). 직사각형 모서리를 이만큼 깎음")]
		[SerializeField] private float cornerRadius = 17f;
		[Tooltip("윤곽 흔들림 폭 (m) 과 각도 주파수")]
		[SerializeField] private float wobbleAmount = 7f;
		[SerializeField] private float wobbleFrequency = 1.7f;

		[Header("Ground paint")]
		[SerializeField] private MeshRenderer groundRenderer;
		[SerializeField] private Rect paintArea = new(-80f, -78f, 160f, 160f);
		[SerializeField] private int paintResolution = 512;
		[Tooltip("빈터 경계 거리 (m) 에서 숲 바닥 (흙 섞인 풀) 으로 바뀌는 구간")]
		[SerializeField] private Vector2 forestFloorRange = new(-1.5f, 4f);
		[SerializeField, Range(0f, 1f)] private float forestFloorEarth = 0.75f;
		[Tooltip("흙길 반폭 (m) 칠 구간")]
		[SerializeField] private Vector2 pathPaintRange = new(0.7f, 1.9f);
		[SerializeField] private float pathWave = 1.2f;
		[SerializeField] private float pathWaveFrequency = 0.16f;
		[Tooltip("홀 둘레 밟힌 흙 구간 (m)")]
		[SerializeField] private Vector2 wornRange = new(0.2f, 2.2f);
		[SerializeField, Range(0f, 1f)] private float wornEarth = 0.6f;
		[SerializeField] private float edgeNoiseScale = 0.3f;
		[SerializeField] private float edgeNoiseStrength = 2.2f;
		[SerializeField] private float patchScale = 0.09f;
		[SerializeField] private Vector2 patchRange = new(0.42f, 0.62f);
		[SerializeField, Range(0f, 1f)] private float patchStrength = 0.85f;
		[SerializeField] private int noiseSeed = 41;

		[Header("Boundary")]
		[SerializeField] private float wallHeight = 4f;
		[SerializeField] private float wallThickness = 1f;
		[Tooltip("경계 벽을 빈터 직사각형 바깥으로 미는 거리 (m)")]
		[SerializeField] private float wallOutset = 0.5f;

		private bool isBuilt;
		private Texture2D control;

		private void Awake() => Build();

		private void OnDestroy()
		{
			if (control != null)
				Destroy(control);
		}

		public void Build()
		{
			if (isBuilt)
				return;
			isBuilt = true;
			PaintGround();
			BuildWalls();
		}

		// 빈터 경계까지의 부호 거리. 안쪽 음수, 바깥 양수. 둥근 직사각형에 각도 잡음 흔들림
		public float EdgeDistance(float x, float z)
		{
			float radius = Mathf.Min(cornerRadius, clearing.width * 0.5f, clearing.height * 0.5f);
			float halfX = clearing.width * 0.5f - radius;
			float halfZ = clearing.height * 0.5f - radius;
			float localX = Mathf.Abs(x - clearing.center.x) - halfX;
			float localZ = Mathf.Abs(z - clearing.center.y) - halfZ;
			float outside = new Vector2(Mathf.Max(localX, 0f), Mathf.Max(localZ, 0f)).magnitude;
			float inside = Mathf.Min(Mathf.Max(localX, localZ), 0f);
			float angle = Mathf.Atan2(z - clearing.center.y, x - clearing.center.x);
			float wobble = (Mathf.PerlinNoise(angle * wobbleFrequency + 10f, 3.3f) - 0.5f) * wobbleAmount;
			return outside + inside - radius - wobble;
		}

		// 빈터 앞 끝에서 홀 문까지 이어지는 흙길의 가운데 x
		public float PathX(float z) => Mathf.Sin(z * pathWaveFrequency) * pathWave * Mathf.InverseLerp(hallFootprint.yMin, clearing.yMin, z);

		public float DistanceToPath(float x, float z)
		{
			if (z > hallFootprint.yMin)
				return Vector2.Distance(new Vector2(x, z), new Vector2(PathX(hallFootprint.yMin), hallFootprint.yMin));
			return Mathf.Abs(x - PathX(z));
		}

		public bool IsInPathGap(float x, float z, float halfWidth) => z < hallFootprint.yMin && DistanceToPath(x, z) < halfWidth;

		private float DistanceToHall(float x, float z)
		{
			float distanceX = Mathf.Max(hallFootprint.xMin - x, 0f, x - hallFootprint.xMax);
			float distanceZ = Mathf.Max(hallFootprint.yMin - z, 0f, z - hallFootprint.yMax);
			return new Vector2(distanceX, distanceZ).magnitude;
		}

		private void PaintGround()
		{
			Color32[] pixels = new Color32[paintResolution * paintResolution];
			for (int j = 0; j < paintResolution; j++)
			{
				for (int i = 0; i < paintResolution; i++)
				{
					float x = paintArea.xMin + (i + 0.5f) / paintResolution * paintArea.width;
					float z = paintArea.yMin + (j + 0.5f) / paintResolution * paintArea.height;
					float noise = (ForestFloor.Fractal(x * edgeNoiseScale, z * edgeNoiseScale, noiseSeed, 3) - 0.5f) * edgeNoiseStrength;
					float forest = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(forestFloorRange.x, forestFloorRange.y, EdgeDistance(x, z) + noise * 2f));
					float pathDistance = DistanceToPath(x, z) + noise;
					float path = (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(pathPaintRange.x, pathPaintRange.y, pathDistance))) * (1f - forest * 0.6f);
					float worn = (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(wornRange.x, wornRange.y, DistanceToHall(x, z) + noise))) * wornEarth;
					float patch = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(patchRange.x, patchRange.y, ForestFloor.Fractal(x * patchScale, z * patchScale, noiseSeed + 36, 4))) * patchStrength;
					float earth = Mathf.Max(forest * forestFloorEarth, worn, patch * (1f - forest));
					earth = Mathf.Max(earth - path, 0f);
					float grass = Mathf.Max(0f, 1f - earth - path);
					float sum = grass + earth + path;
					pixels[j * paintResolution + i] = new Color32((byte)(grass / sum * 255f), (byte)(earth / sum * 255f), (byte)(path / sum * 255f), 0);
				}
			}
			control = new Texture2D(paintResolution, paintResolution, TextureFormat.RGBA32, false, true)
			{
				name = "LabGroundControl",
				hideFlags = HideFlags.DontSave,
				wrapMode = TextureWrapMode.Clamp,
				filterMode = FilterMode.Bilinear
			};
			control.SetPixels32(pixels);
			control.Apply(false);
			MaterialPropertyBlock block = new();
			groundRenderer.GetPropertyBlock(block);
			block.SetTexture("_Control", control);
			block.SetVector("_WorldSize", new Vector4(paintArea.xMin, paintArea.yMin, paintArea.width, paintArea.height));
			groundRenderer.SetPropertyBlock(block);
		}

		// 숲으로 걸어 들어가지 않게 빈터 직사각형 네 변에 보이지 않는 벽
		private void BuildWalls()
		{
			Transform wallRoot = new GameObject("Boundary").transform;
			wallRoot.SetParent(transform, false);
			float width = clearing.width + wallOutset * 2f + wallThickness * 2f;
			float depth = clearing.height + wallOutset * 2f;
			float offsetX = clearing.width * 0.5f + wallOutset + wallThickness * 0.5f;
			float offsetZ = clearing.height * 0.5f + wallOutset + wallThickness * 0.5f;
			AddWall(wallRoot, new Vector3(clearing.center.x, 0f, clearing.center.y - offsetZ), new Vector3(width, wallHeight, wallThickness));
			AddWall(wallRoot, new Vector3(clearing.center.x, 0f, clearing.center.y + offsetZ), new Vector3(width, wallHeight, wallThickness));
			AddWall(wallRoot, new Vector3(clearing.center.x - offsetX, 0f, clearing.center.y), new Vector3(wallThickness, wallHeight, depth));
			AddWall(wallRoot, new Vector3(clearing.center.x + offsetX, 0f, clearing.center.y), new Vector3(wallThickness, wallHeight, depth));
		}

		private void AddWall(Transform parent, Vector3 position, Vector3 size)
		{
			GameObject wall = new("Wall");
			wall.transform.SetParent(parent, false);
			wall.transform.localPosition = position + Vector3.up * size.y * 0.5f;
			wall.AddComponent<BoxCollider>().size = size;
		}
	}
}

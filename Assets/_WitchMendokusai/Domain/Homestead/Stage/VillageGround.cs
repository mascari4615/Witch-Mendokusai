using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	// 마을 바닥. 평평한 빈터에 숲 지형 셰이더 칠 (R 풀, G 흙 섞인 풀, B 흙길), 풀잎과 꽃, 둘레 숲, 경계 벽
	// 풀잎과 판 묶음은 숲 바닥 (ForestFloor) 의 것을 그대로. 자리 판정만 마을 것
	public sealed class VillageGround : MonoBehaviour
	{
		private const int FLOWER_KIND_COUNT = 4;

		[Header("Clearing")]
		[SerializeField] private Rect clearing = new(-31f, -25f, 62f, 58f);
		[SerializeField] private float cornerRadius = 20f;
		[SerializeField] private float wobbleAmount = 6f;
		[SerializeField] private float wobbleFrequency = 1.5f;

		[Header("Paint")]
		[SerializeField] private Material terrainMaterial;
		[SerializeField] private Rect paintArea = new(-80f, -76f, 160f, 160f);
		[SerializeField] private int paintResolution = 512;
		[SerializeField] private Vector2 pathRange = new(0.8f, 1.9f);
		[SerializeField] private Vector2 forestRange = new(-2f, 4f);
		[SerializeField, Range(0f, 1f)] private float forestEarth = 0.75f;
		[SerializeField] private Vector2 wornRange = new(0f, 1.8f);
		[SerializeField, Range(0f, 1f)] private float wornEarth = 0.55f;
		[SerializeField] private float edgeNoiseScale = 0.3f;
		[SerializeField] private float edgeNoiseStrength = 1.6f;
		[SerializeField] private float patchScale = 0.09f;
		[SerializeField] private Vector2 patchRange = new(0.46f, 0.64f);
		[SerializeField, Range(0f, 1f)] private float patchStrength = 0.6f;
		[SerializeField] private int noiseSeed = 73;

		[Header("Grass")]
		[SerializeField] private Material grassMaterial;
		[SerializeField] private int grassSeed = 9;
		[SerializeField] private Rect grassArea = new(-40f, -34f, 80f, 76f);
		[SerializeField] private int grassAttempts = 900000;
		[SerializeField] private float grassCellSize = 16f;
		[SerializeField] private Vector2 grassHeight = new(0.28f, 0.6f);
		[SerializeField] private Vector2 grassWidth = new(0.05f, 0.1f);
		[SerializeField] private Vector2 grassWind = new(0.32f, 0.2f);
		[SerializeField] private float grassClumpScale = 0.12f;
		[SerializeField] private float grassHueScale = 0.04f;
		[SerializeField, Range(0f, 1f)] private float grassFlowerChance = 0.014f;
		[SerializeField] private Color32 grassRootA = new(0x1f, 0x58, 0x0c, 0xff);
		[SerializeField] private Color32 grassRootB = new(0x2a, 0x6a, 0x10, 0xff);
		[SerializeField] private Color32 grassTipA = new(0x8f, 0xd2, 0x2c, 0xff);
		[SerializeField] private Color32 grassTipB = new(0xb9, 0xe2, 0x4c, 0xff);
		[SerializeField] private Color32 grassDryRoot = new(0x7f, 0x9a, 0x3e, 0xff);
		[SerializeField] private Color32 grassDryTip = new(0xb9, 0xc4, 0x6a, 0xff);

		[Header("Flowers and ferns")]
		[Tooltip("흰, 노랑, 분홍, 하늘 순서 4개")]
		[SerializeField] private Material[] flowerMaterials = new Material[FLOWER_KIND_COUNT];
		[SerializeField] private Material fernMaterial;
		[SerializeField] private int flowerPatches = 90;
		[SerializeField] private Vector2Int flowerCount = new(25, 70);
		[SerializeField] private int fernPatches = 120;

		[Header("Forest")]
		[SerializeField] private GameObject[] treeTemplates = System.Array.Empty<GameObject>();
		[SerializeField] private int treeSeed = 31;
		[SerializeField] private int treeAttempts = 1500;
		[SerializeField] private float treeGap = 3.4f;
		[SerializeField] private Vector2 treeEdge = new(2.5f, 30f);
		[SerializeField] private Vector2 treeScale = new(0.8f, 1.25f);

		[Header("Boundary")]
		[SerializeField] private float wallHeight = 4f;
		[SerializeField] private int wallSegments = 48;

		private readonly List<Object> generated = new();
		private readonly List<(Vector2 From, Vector2 To)> paths = new();
		private readonly List<(Vector2 Center, float Radius)> blocks = new();
		private readonly List<(Vector2 Center, float Radius)> waters = new();
		private Color32[] controlPixels;

		private void OnDestroy()
		{
			foreach (Object item in generated)
			{
				if (item != null)
					Destroy(item);
			}
			generated.Clear();
		}

		public void AddPath(Vector2 from, Vector2 to) => paths.Add((from, to));

		// 풀을 비우고 흙을 밟힌 자리
		public void AddBlock(Vector2 center, float radius) => blocks.Add((center, radius));

		public void AddWater(Vector2 center, float radius) => waters.Add((center, radius));

		public void Build()
		{
			BuildFloor();
			BuildWalls();
			BuildGrass();
			BuildFlowers();
			BuildFerns();
			BuildForest();
		}

		// 빈터 경계까지의 부호 거리. 안쪽 음수
		public float EdgeDistance(float x, float z)
		{
			float radius = Mathf.Min(cornerRadius, clearing.width * 0.5f, clearing.height * 0.5f);
			float localX = Mathf.Abs(x - clearing.center.x) - (clearing.width * 0.5f - radius);
			float localZ = Mathf.Abs(z - clearing.center.y) - (clearing.height * 0.5f - radius);
			float outside = new Vector2(Mathf.Max(localX, 0f), Mathf.Max(localZ, 0f)).magnitude;
			float inside = Mathf.Min(Mathf.Max(localX, localZ), 0f);
			float angle = Mathf.Atan2(z - clearing.center.y, x - clearing.center.x);
			float wobble = (Mathf.PerlinNoise(Mathf.Cos(angle) * wobbleFrequency + 10f, Mathf.Sin(angle) * wobbleFrequency + 3.3f) - 0.5f) * wobbleAmount;
			return outside + inside - radius - wobble;
		}

		private float DistanceToPath(float x, float z)
		{
			float nearest = float.MaxValue;
			Vector2 point = new(x, z);
			foreach ((Vector2 from, Vector2 to) in paths)
			{
				Vector2 line = to - from;
				float along = Mathf.Clamp01(Vector2.Dot(point - from, line) / Mathf.Max(line.sqrMagnitude, 0.001f));
				// 곧은 선이 아니라 조금 굽은 길
				Vector2 side = new Vector2(-line.y, line.x).normalized * Mathf.Sin(along * Mathf.PI) * line.magnitude * 0.07f;
				nearest = Mathf.Min(nearest, Vector2.Distance(point, from + line * along + side));
			}
			return nearest;
		}

		private static float Nearest(List<(Vector2 Center, float Radius)> circles, float x, float z)
		{
			float nearest = float.MaxValue;
			foreach ((Vector2 center, float radius) in circles)
				nearest = Mathf.Min(nearest, Vector2.Distance(new Vector2(x, z), center) - radius);
			return nearest;
		}

		private bool IsBlocked(float x, float z, float margin) =>
			Nearest(blocks, x, z) < margin || Nearest(waters, x, z) < margin || DistanceToPath(x, z) < pathRange.x + margin;

		private void BuildFloor()
		{
			GameObject floor = new("Floor");
			floor.transform.SetParent(transform, false);
			Mesh mesh = new() { name = "VillageFloor", hideFlags = HideFlags.DontSave };
			mesh.vertices = new[] { new Vector3(paintArea.xMin, 0f, paintArea.yMin), new Vector3(paintArea.xMin, 0f, paintArea.yMax), new Vector3(paintArea.xMax, 0f, paintArea.yMax), new Vector3(paintArea.xMax, 0f, paintArea.yMin) };
			mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
			mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
			mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
			generated.Add(mesh);
			floor.AddComponent<MeshFilter>().sharedMesh = mesh;
			MeshRenderer floorRenderer = floor.AddComponent<MeshRenderer>();
			floorRenderer.sharedMaterial = terrainMaterial;
			floorRenderer.shadowCastingMode = ShadowCastingMode.Off;
			BoxCollider box = floor.AddComponent<BoxCollider>();
			box.center = new Vector3(paintArea.center.x, -0.5f, paintArea.center.y);
			box.size = new Vector3(paintArea.width, 1f, paintArea.height);
			Paint(floorRenderer);
		}

		private void Paint(MeshRenderer floorRenderer)
		{
			controlPixels = new Color32[paintResolution * paintResolution];
			for (int j = 0; j < paintResolution; j++)
			{
				for (int i = 0; i < paintResolution; i++)
				{
					float x = paintArea.xMin + (i + 0.5f) / paintResolution * paintArea.width;
					float z = paintArea.yMin + (j + 0.5f) / paintResolution * paintArea.height;
					float noise = (ForestFloor.Fractal(x * edgeNoiseScale, z * edgeNoiseScale, noiseSeed, 3) - 0.5f) * edgeNoiseStrength;
					float forest = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(forestRange.x, forestRange.y, EdgeDistance(x, z) + noise * 2f));
					float pathDistance = Mathf.Min(DistanceToPath(x, z), Nearest(waters, x, z) + pathRange.x) + noise * 0.6f;
					float path = (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(pathRange.x, pathRange.y, pathDistance))) * (1f - forest * 0.6f);
					float worn = (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(wornRange.x, wornRange.y, Nearest(blocks, x, z) + noise))) * wornEarth;
					float patch = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(patchRange.x, patchRange.y, ForestFloor.Fractal(x * patchScale, z * patchScale, noiseSeed + 36, 4))) * patchStrength;
					float earth = Mathf.Max(Mathf.Max(forest * forestEarth, worn), patch * (1f - forest));
					earth = Mathf.Max(earth - path, 0f);
					float grass = Mathf.Max(0f, 1f - earth - path);
					float sum = grass + earth + path;
					controlPixels[j * paintResolution + i] = new Color32((byte)(grass / sum * 255f), (byte)(earth / sum * 255f), (byte)(path / sum * 255f), 0);
				}
			}
			Texture2D control = new(paintResolution, paintResolution, TextureFormat.RGBA32, false, true)
			{
				name = "VillageControl",
				hideFlags = HideFlags.DontSave,
				wrapMode = TextureWrapMode.Clamp,
				filterMode = FilterMode.Bilinear,
			};
			control.SetPixels32(controlPixels);
			control.Apply(false);
			generated.Add(control);
			MaterialPropertyBlock block = new();
			block.SetTexture("_Control", control);
			block.SetVector("_WorldSize", new Vector4(paintArea.xMin, paintArea.yMin, paintArea.width, paintArea.height));
			floorRenderer.SetPropertyBlock(block);
		}

		private Color32 ControlAt(float x, float z)
		{
			int column = Mathf.Clamp((int)((x - paintArea.xMin) / paintArea.width * paintResolution), 0, paintResolution - 1);
			int row = Mathf.Clamp((int)((z - paintArea.yMin) / paintArea.height * paintResolution), 0, paintResolution - 1);
			return controlPixels[row * paintResolution + column];
		}

		// 빈터 윤곽을 따라 선 보이지 않는 벽
		private void BuildWalls()
		{
			Transform root = new GameObject("Boundary").transform;
			root.SetParent(transform, false);
			Vector3 previous = EdgePoint(0f);
			for (int i = 1; i <= wallSegments; i++)
			{
				Vector3 next = EdgePoint(i / (float)wallSegments * Mathf.PI * 2f);
				Vector3 delta = next - previous;
				GameObject wall = new("Wall");
				wall.transform.SetParent(root, false);
				wall.transform.SetLocalPositionAndRotation((previous + next) * 0.5f + Vector3.up * wallHeight * 0.5f, Quaternion.LookRotation(delta));
				wall.AddComponent<BoxCollider>().size = new Vector3(0.6f, wallHeight, delta.magnitude + 0.6f);
				previous = next;
			}
		}

		private Vector3 EdgePoint(float angle)
		{
			Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
			float near = 0f;
			float far = Mathf.Max(clearing.width, clearing.height);
			for (int i = 0; i < 24; i++)
			{
				float middle = (near + far) * 0.5f;
				Vector2 point = clearing.center + direction * middle;
				if (EdgeDistance(point.x, point.y) < 0f)
					near = middle;
				else
					far = middle;
			}
			Vector2 edge = clearing.center + direction * near;
			return new Vector3(edge.x, 0f, edge.y);
		}

		private static float Range01(System.Random random, Vector2 range) => Mathf.Lerp(range.x, range.y, (float)random.NextDouble());

		private static Color32 Shaded(Color32 color, float shade) =>
			new((byte)Mathf.Min(255f, color.r * shade), (byte)Mathf.Min(255f, color.g * shade), (byte)Mathf.Min(255f, color.b * shade), color.a);

		private void BuildGrass()
		{
			System.Random random = new(grassSeed);
			int columns = Mathf.CeilToInt(grassArea.width / grassCellSize);
			int rows = Mathf.CeilToInt(grassArea.height / grassCellSize);
			ForestFloor.GrassCell[] cells = new ForestFloor.GrassCell[columns * rows];
			for (int i = 0; i < cells.Length; i++)
				cells[i] = new ForestFloor.GrassCell();
			for (int attempt = 0; attempt < grassAttempts; attempt++)
			{
				float x = grassArea.xMin + (float)random.NextDouble() * grassArea.width;
				float z = grassArea.yMin + (float)random.NextDouble() * grassArea.height;
				Color32 control = ControlAt(x, z);
				float path = control.b / 255f;
				float earth = control.g / 255f;
				if ((float)random.NextDouble() < path * 1.6f + earth * 0.5f || Nearest(waters, x, z) < 0.2f || Nearest(blocks, x, z) < 0f)
					continue;
				float clump = ForestFloor.Fractal(x * grassClumpScale, z * grassClumpScale, grassSeed + 76, 3);
				if ((float)random.NextDouble() > clump * 1.1f + 0.15f)
					continue;
				float hue = ForestFloor.Fractal(x * grassHueScale, z * grassHueScale, grassSeed + 78, 2);
				float height = Range01(random, grassHeight) * (0.55f + clump * 0.8f) * (1f - earth * 0.45f);
				float angle = (float)random.NextDouble() * Mathf.PI * 2f;
				Vector3 side = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Range01(random, grassWidth);
				Vector3 lean = new Vector3(grassWind.x + (float)random.NextDouble() * 0.5f - 0.25f, 0f, grassWind.y + (float)random.NextDouble() * 0.5f - 0.25f) * (height * 0.65f);
				bool isFlower = random.NextDouble() < grassFlowerChance && earth < 0.4f;
				float shade = Mathf.Lerp(0.55f, 1.05f, Mathf.Clamp01(clump * 1.4f - 0.2f));
				Color32 root = Shaded(Color32.Lerp(Color32.Lerp(grassRootA, grassRootB, hue), grassDryRoot, earth), shade);
				Color32 tip = isFlower ? new Color32(0xff, 0xf2, 0xc0, 0xff) : Shaded(Color32.Lerp(Color32.Lerp(grassTipA, grassTipB, hue), grassDryTip, earth * 0.7f), shade);
				Color32 middle = Color32.Lerp(root, tip, 0.55f);
				root.a = 0;
				middle.a = 140;
				tip.a = 255;
				int column = Mathf.Min(columns - 1, (int)((x - grassArea.xMin) / grassCellSize));
				int row = Mathf.Min(rows - 1, (int)((z - grassArea.yMin) / grassCellSize));
				cells[row * columns + column].AddBlade(new Vector3(x, -0.03f, z), side, lean, height, isFlower, root, middle, tip);
			}
			for (int i = 0; i < cells.Length; i++)
			{
				if (cells[i].IsEmpty == false)
					AddChildMesh("Grass " + i, cells[i].ToMesh("VillageGrass" + i), grassMaterial);
			}
		}

		private void BuildFlowers()
		{
			System.Random random = new(grassSeed + 100);
			ForestFloor.CardBatch[] batches = new ForestFloor.CardBatch[FLOWER_KIND_COUNT];
			for (int i = 0; i < FLOWER_KIND_COUNT; i++)
				batches[i] = new ForestFloor.CardBatch();
			for (int patch = 0; patch < flowerPatches; patch++)
			{
				float centerX = grassArea.xMin + (float)random.NextDouble() * grassArea.width;
				float centerZ = grassArea.yMin + (float)random.NextDouble() * grassArea.height;
				if (EdgeDistance(centerX, centerZ) > 2f || IsBlocked(centerX, centerZ, 1.5f))
					continue;
				int kind = random.Next(FLOWER_KIND_COUNT);
				int count = random.Next(flowerCount.x, flowerCount.y);
				for (int k = 0; k < count; k++)
				{
					float angle = (float)random.NextDouble() * Mathf.PI * 2f;
					float distance = Mathf.Sqrt((float)random.NextDouble()) * 1.8f;
					float x = centerX + Mathf.Cos(angle) * distance;
					float z = centerZ + Mathf.Sin(angle) * distance;
					if (IsBlocked(x, z, 0.8f))
						continue;
					float size = 0.18f + (float)random.NextDouble() * 0.1f;
					for (int card = 0; card < 2; card++)
					{
						float cardAngle = (float)random.NextDouble() * Mathf.PI + card * Mathf.PI * 0.5f;
						Vector3 side = new Vector3(Mathf.Cos(cardAngle), 0f, Mathf.Sin(cardAngle)) * (size * 0.5f);
						batches[kind].Add(new Vector3(x, 0.22f, z), side, Vector3.up * (size * 1.5f));
					}
				}
			}
			for (int i = 0; i < FLOWER_KIND_COUNT; i++)
				AddChildMesh("Flowers " + i, batches[i].ToMesh("VillageFlowers" + i), flowerMaterials[i]);
		}

		private void BuildFerns()
		{
			System.Random random = new(grassSeed + 200);
			ForestFloor.CardBatch batch = new();
			for (int patch = 0; patch < fernPatches; patch++)
			{
				float centerX = grassArea.xMin + (float)random.NextDouble() * grassArea.width;
				float centerZ = grassArea.yMin + (float)random.NextDouble() * grassArea.height;
				float edge = EdgeDistance(centerX, centerZ);
				if (edge < -2f || edge > 10f || IsBlocked(centerX, centerZ, 1f))
					continue;
				for (int k = 0; k < 5; k++)
				{
					float x = centerX + ((float)random.NextDouble() - 0.5f) * 2f;
					float z = centerZ + ((float)random.NextDouble() - 0.5f) * 2f;
					float size = 0.45f + (float)random.NextDouble() * 0.4f;
					for (int frond = 0; frond < 4; frond++)
					{
						float angle = frond * Mathf.PI * 0.5f + (float)random.NextDouble() * 0.6f;
						Vector3 direction = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
						batch.Add(new Vector3(x, -0.03f, z), Vector3.Cross(direction, Vector3.up) * (size * 0.45f), (direction * 0.75f + Vector3.up * 0.75f) * size);
					}
				}
			}
			AddChildMesh("Ferns", batch.ToMesh("VillageFerns"), fernMaterial);
		}

		private void AddChildMesh(string objectName, Mesh mesh, Material material)
		{
			generated.Add(mesh);
			GameObject child = new(objectName);
			child.transform.SetParent(transform, false);
			child.AddComponent<MeshFilter>().sharedMesh = mesh;
			MeshRenderer childRenderer = child.AddComponent<MeshRenderer>();
			childRenderer.sharedMaterial = material;
			childRenderer.shadowCastingMode = ShadowCastingMode.Off;
		}

		private void BuildForest()
		{
			System.Random random = new(treeSeed);
			Transform root = new GameObject("Forest").transform;
			root.SetParent(transform, false);
			List<Vector2> placed = new();
			Rect area = new(clearing.xMin - treeEdge.y, clearing.yMin - treeEdge.y, clearing.width + treeEdge.y * 2f, clearing.height + treeEdge.y * 2f);
			for (int attempt = 0; attempt < treeAttempts; attempt++)
			{
				Vector2 point = new(area.xMin + (float)random.NextDouble() * area.width, area.yMin + (float)random.NextDouble() * area.height);
				float edge = EdgeDistance(point.x, point.y);
				if (edge < treeEdge.x || edge > treeEdge.y || placed.Exists(other => Vector2.Distance(other, point) < treeGap))
					continue;
				placed.Add(point);
				GameObject tree = Instantiate(treeTemplates[random.Next(treeTemplates.Length)], root);
				tree.transform.SetLocalPositionAndRotation(new Vector3(point.x, 0f, point.y), Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f));
				tree.transform.localScale = Vector3.one * Range01(random, treeScale);
			}
		}

		public GameObject SpawnTree(int template, Transform parent, Vector3 localPosition, float yaw, float scale)
		{
			GameObject tree = Instantiate(treeTemplates[template % treeTemplates.Length], parent);
			tree.transform.SetLocalPositionAndRotation(localPosition, Quaternion.Euler(0f, yaw, 0f));
			tree.transform.localScale = Vector3.one * scale;
			return tree;
		}
	}
}

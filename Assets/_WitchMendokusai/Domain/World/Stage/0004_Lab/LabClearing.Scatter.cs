using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	// 빈터 바닥 흩뿌림: 풀잎 (정점 색), 꽃 무리, 고사리. 재질과 생성 방식은 숲 바닥 (ForestFloor.Scatter) 과 같음
	// 풀잎 메시와 판 묶음은 ForestFloor 의 GrassCell, CardBatch 를 그대로 씀. 다른 점은 자리 판정. 숲은 길 하나를 따라가고, 여기는 빈터 제어 텍스처, 흙길, 홀 바닥을 피함
	public partial class LabClearing
	{
		private const int FLOWER_KIND_COUNT = 4;

		[Header("Scatter")]
		[SerializeField] private TitleTerrain terrain;
		[SerializeField] private Rect scatterArea = new(-48f, -50f, 96f, 104f);
		[Tooltip("홀 바닥에서 이 거리 (m) 안쪽은 비움")]
		[SerializeField] private float hallMargin = 0.4f;

		[Header("Grass")]
		[SerializeField] private Material grassMaterial;
		[SerializeField] private int grassSeed = 5;
		[SerializeField] private int grassAttempts = 3200000;
		[SerializeField] private float grassCellSize = 16f;
		[SerializeField] private float grassPathCull = 1.5f;
		[SerializeField] private float grassEarthCull = 0.5f;
		[SerializeField] private float grassClumpScale = 0.12f;
		[SerializeField] private float grassHueScale = 0.04f;
		[Tooltip("빈터 안 풀 키 배율. 숲 쪽은 1")]
		[SerializeField] private float grassClearingHeight = 0.7f;
		[SerializeField] private float grassSink = 0.03f;
		[SerializeField] private Vector2 grassHeightRange = new(0.5f, 0.92f);
		[SerializeField] private Vector2 grassWidthRange = new(0.04f, 0.08f);
		[SerializeField] private Vector2 grassBendRange = new(0.5f, 0.85f);
		[SerializeField] private float grassLeanJitter = 0.5f;
		[SerializeField] private Vector2 grassWind = new(0.32f, 0.2f);
		[SerializeField] private Vector2 grassShadeRange = new(0.5f, 1.05f);
		[SerializeField, Range(0f, 1f)] private float grassFlowerChance = 0.012f;
		[SerializeField] private Color32 grassRootA = new(0x1f, 0x58, 0x0c, 0xff);
		[SerializeField] private Color32 grassRootB = new(0x2a, 0x6a, 0x10, 0xff);
		[SerializeField] private Color32 grassTipA = new(0x8f, 0xd2, 0x2c, 0xff);
		[SerializeField] private Color32 grassTipB = new(0xb9, 0xe2, 0x4c, 0xff);
		[SerializeField] private Color32 grassDryRoot = new(0x7f, 0x9a, 0x3e, 0xff);
		[SerializeField] private Color32 grassDryTip = new(0xb9, 0xc4, 0x6a, 0xff);
		[SerializeField] private Color32[] grassFlowerColors =
		{
			new(0xff, 0xff, 0xff, 0xff), new(0xff, 0xd2, 0x1f, 0xff), new(0xff, 0xd2, 0x1f, 0xff), new(0xff, 0xc8, 0xda, 0xff)
		};

		[Header("Flowers")]
		[Tooltip("흰, 노랑, 분홍, 하늘 순서 4개")]
		[SerializeField] private Material[] flowerMaterials = new Material[FLOWER_KIND_COUNT];
		[SerializeField] private int flowerSeed = 31;
		[SerializeField] private int flowerPatches = 160;
		[SerializeField, Range(0f, 1f)] private float flowerYellowChance = 0.45f;
		[SerializeField] private Vector2 flowerPatchRadius = new(0.8f, 2.4f);
		[SerializeField] private Vector2Int flowerPatchCount = new(40, 130);
		[SerializeField] private Vector2 flowerSize = new(0.2f, 0.32f);
		[SerializeField] private float flowerLift = 0.3f;
		[SerializeField] private float flowerPathMargin = 2.2f;
		[Tooltip("꽃 무리 중심이 놓이는 빈터 경계 거리 (m) 범위. 빈터 안 가장자리와 숲 초입")]
		[SerializeField] private Vector2 flowerEdgeRange = new(-12f, 3f);

		[Header("Ferns")]
		[SerializeField] private Material fernMaterial;
		[SerializeField] private int fernSeed = 77;
		[SerializeField] private int fernPatches = 220;
		[Tooltip("고사리 무리 중심이 놓이는 빈터 경계 거리 (m) 범위. 숲 초입과 나무 아래")]
		[SerializeField] private Vector2 fernEdgeRange = new(-1.5f, 14f);
		[SerializeField] private Vector2Int fernPatchCount = new(3, 9);
		[SerializeField] private float fernPatchSpread = 2.2f;
		[SerializeField] private Vector2 fernSize = new(0.45f, 0.85f);

		private readonly List<Object> generated = new();

		private float GroundHeight(float x, float z) => terrain.HeightAt(x, z);

		private bool IsBlocked(float x, float z, float pathMargin)
			=> DistanceToHall(x, z) < hallMargin || DistanceToPath(x, z) < pathMargin;

		private static float Range01(System.Random random, Vector2 range) => Mathf.Lerp(range.x, range.y, (float)random.NextDouble());

		private static Color32 Shaded(Color32 color, float shade)
			=> new((byte)Mathf.Min(255f, color.r * shade), (byte)Mathf.Min(255f, color.g * shade), (byte)Mathf.Min(255f, color.b * shade), color.a);

		private void BuildGrass()
		{
			System.Random random = new(grassSeed);
			int columns = Mathf.CeilToInt(scatterArea.width / grassCellSize);
			int rows = Mathf.CeilToInt(scatterArea.height / grassCellSize);
			ForestFloor.GrassCell[] cells = new ForestFloor.GrassCell[columns * rows];
			for (int i = 0; i < cells.Length; i++)
				cells[i] = new ForestFloor.GrassCell();
			for (int attempt = 0; attempt < grassAttempts; attempt++)
			{
				float x = scatterArea.xMin + (float)random.NextDouble() * scatterArea.width;
				float z = scatterArea.yMin + (float)random.NextDouble() * scatterArea.height;
				if (DistanceToHall(x, z) < hallMargin)
					continue;
				Color32 control = ControlAt(x, z);
				float path = control.b / 255f;
				float earth = control.g / 255f;
				if ((float)random.NextDouble() < path * grassPathCull + earth * grassEarthCull)
					continue;
				float clump = ForestFloor.Fractal(x * grassClumpScale, z * grassClumpScale, grassSeed + 76, 3);
				if ((float)random.NextDouble() > clump * 1.1f + 0.1f)
					continue;
				float hue = ForestFloor.Fractal(x * grassHueScale, z * grassHueScale, grassSeed + 78, 2);
				float inClearing = Mathf.Clamp01(-EdgeDistance(x, z) / 4f);
				float height = Range01(random, grassHeightRange) * (0.55f + clump * 0.8f) * (1f - earth * 0.45f) * Mathf.Lerp(1f, grassClearingHeight, inClearing);
				float width = Range01(random, grassWidthRange);
				float angle = (float)random.NextDouble() * Mathf.PI * 2f;
				Vector3 side = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * width;
				float bend = Range01(random, grassBendRange);
				Vector3 lean = new Vector3(grassWind.x + ((float)random.NextDouble() - 0.5f) * grassLeanJitter, 0f, grassWind.y + ((float)random.NextDouble() - 0.5f) * grassLeanJitter) * height * bend;
				Vector3 basePoint = new(x, GroundHeight(x, z) - grassSink, z);
				bool isFlower = random.NextDouble() < grassFlowerChance && earth < 0.4f;
				Color32 root = Color32.Lerp(Color32.Lerp(grassRootA, grassRootB, hue), grassDryRoot, earth);
				Color32 tip = isFlower ? grassFlowerColors[random.Next(grassFlowerColors.Length)] : Color32.Lerp(Color32.Lerp(grassTipA, grassTipB, hue), grassDryTip, earth * 0.7f);
				float shade = Mathf.Lerp(grassShadeRange.x, grassShadeRange.y, Mathf.Clamp01(clump * 1.4f - 0.2f));
				root = Shaded(root, shade);
				if (isFlower == false)
					tip = Shaded(tip, shade);
				Color32 middle = Color32.Lerp(root, tip, 0.55f);
				root.a = 0;
				middle.a = 140;
				tip.a = 255;
				int column = Mathf.Min(columns - 1, (int)((x - scatterArea.xMin) / grassCellSize));
				int row = Mathf.Min(rows - 1, (int)((z - scatterArea.yMin) / grassCellSize));
				cells[row * columns + column].AddBlade(basePoint, side, lean, height, isFlower, root, middle, tip);
			}
			for (int i = 0; i < cells.Length; i++)
			{
				if (cells[i].IsEmpty)
					continue;
				AddChildMesh("Grass " + i, cells[i].ToMesh("LabGrass" + i), grassMaterial);
			}
		}

		private void BuildFlowers()
		{
			System.Random random = new(flowerSeed);
			ForestFloor.CardBatch[] batches = new ForestFloor.CardBatch[FLOWER_KIND_COUNT];
			for (int i = 0; i < FLOWER_KIND_COUNT; i++)
				batches[i] = new ForestFloor.CardBatch();
			for (int patch = 0; patch < flowerPatches; patch++)
			{
				float centerX = scatterArea.xMin + (float)random.NextDouble() * scatterArea.width;
				float centerZ = scatterArea.yMin + (float)random.NextDouble() * scatterArea.height;
				float edge = EdgeDistance(centerX, centerZ);
				if (edge < flowerEdgeRange.x || edge > flowerEdgeRange.y || IsBlocked(centerX, centerZ, flowerPathMargin + 1f))
					continue;
				int kind = random.NextDouble() < flowerYellowChance ? 1 : random.Next(FLOWER_KIND_COUNT);
				float radius = Range01(random, flowerPatchRadius);
				int count = random.Next(flowerPatchCount.x, flowerPatchCount.y);
				for (int k = 0; k < count; k++)
				{
					float angle = (float)random.NextDouble() * Mathf.PI * 2f;
					float distance = Mathf.Sqrt((float)random.NextDouble()) * radius;
					float x = centerX + Mathf.Cos(angle) * distance;
					float z = centerZ + Mathf.Sin(angle) * distance;
					if (IsBlocked(x, z, flowerPathMargin))
						continue;
					float size = Range01(random, flowerSize);
					Vector3 basePoint = new(x, GroundHeight(x, z) + flowerLift * 0.8f, z);
					for (int card = 0; card < 2; card++)
					{
						float cardAngle = (float)random.NextDouble() * Mathf.PI + card * Mathf.PI * 0.5f;
						Vector3 side = new Vector3(Mathf.Cos(cardAngle), 0f, Mathf.Sin(cardAngle)) * size * 0.5f;
						batches[kind].Add(basePoint, side, Vector3.up * (size * 1.3f + flowerLift * 0.2f));
					}
				}
			}
			for (int i = 0; i < FLOWER_KIND_COUNT; i++)
				AddChildMesh("Flowers " + i, batches[i].ToMesh("LabFlowers" + i), flowerMaterials[i]);
		}

		private void BuildFerns()
		{
			System.Random random = new(fernSeed);
			ForestFloor.CardBatch batch = new();
			for (int patch = 0; patch < fernPatches; patch++)
			{
				float centerX = scatterArea.xMin + (float)random.NextDouble() * scatterArea.width;
				float centerZ = scatterArea.yMin + (float)random.NextDouble() * scatterArea.height;
				float edge = EdgeDistance(centerX, centerZ);
				if (edge < fernEdgeRange.x || edge > fernEdgeRange.y || IsBlocked(centerX, centerZ, 3f))
					continue;
				int count = random.Next(fernPatchCount.x, fernPatchCount.y);
				for (int k = 0; k < count; k++)
				{
					float x = centerX + ((float)random.NextDouble() - 0.5f) * fernPatchSpread;
					float z = centerZ + ((float)random.NextDouble() - 0.5f) * fernPatchSpread;
					Vector3 basePoint = new(x, GroundHeight(x, z) - grassSink, z);
					float size = Range01(random, fernSize);
					for (int frond = 0; frond < 4; frond++)
					{
						float angle = frond * Mathf.PI * 0.5f + (float)random.NextDouble() * 0.6f;
						Vector3 direction = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
						Vector3 side = Vector3.Cross(direction, Vector3.up) * size * 0.45f;
						batch.Add(basePoint, side, (direction * 0.75f + Vector3.up * 0.75f) * size);
					}
				}
			}
			AddChildMesh("Ferns", batch.ToMesh("LabFerns"), fernMaterial);
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
	}
}

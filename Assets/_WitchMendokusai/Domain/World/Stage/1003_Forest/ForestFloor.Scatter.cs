using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	// 숲 바닥 위 흩뿌림: 풀잎 (정점 색), 꽃 무리, 고사리, 노란 꽃대. 판 수가 많아 켜질 때 생성
	// 풀은 칸별 메시로 나눠 카메라 밖 칸은 그리지 않음
	public partial class ForestFloor
	{
		private const int FLOWER_KIND_COUNT = 4;

		[Header("Grass")]
		[SerializeField] private Material grassMaterial;
		[SerializeField] private int grassSeed = 5;
		[SerializeField] private int grassClumpSeed = 81;
		[SerializeField] private int grassHueSeed = 83;
		[Tooltip("풀잎 후보 수. 줄이면 생성이 빨라지고 풀이 성겨짐")]
		[SerializeField] private int grassAttempts = 3600000;
		[SerializeField] private Rect grassArea = new(-45f, -12f, 90f, 80f);
		[Tooltip("풀 메시 한 칸의 한 변 (m)")]
		[SerializeField] private float grassCellSize = 16f;
		[Tooltip("흙길과 흙 섞인 풀에서 풀잎을 빼는 정도")]
		[SerializeField] private float grassPathCull = 1.5f;
		[SerializeField] private float grassEarthCull = 0.45f;
		[SerializeField] private float grassClumpScale = 0.12f;
		[SerializeField] private float grassHueScale = 0.04f;
		[Tooltip("길 시작에서 이 거리 (m) 안쪽은 풀이 더 빽빽")]
		[SerializeField] private Vector2 grassNearRange = new(4f, 34f);
		[SerializeField] private float grassSink = 0.03f;
		[SerializeField] private Vector2 grassHeightRange = new(0.5f, 0.92f);
		[SerializeField] private Vector2 grassWidthRange = new(0.04f, 0.08f);
		[SerializeField] private Vector2 grassBendRange = new(0.5f, 0.85f);
		[SerializeField] private float grassLeanJitter = 0.5f;
		[SerializeField] private Vector2 grassWind = new(0.32f, 0.2f);
		[Tooltip("풀 무리 잡음이 낮은 곳의 어둡기와 높은 곳의 밝기")]
		[SerializeField] private Vector2 grassShadeRange = new(0.5f, 1.05f);
		[Range(0f, 1f)]
		[SerializeField] private float grassFlowerChance = 0.012f;
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
		[SerializeField] private int flowerPatches = 240;
		[SerializeField] private Rect flowerArea = new(-40f, -8f, 80f, 70f);
		[Range(0f, 1f), Tooltip("꽃 무리가 노란 꽃일 확률. 나머지는 네 색 고르게")]
		[SerializeField] private float flowerYellowChance = 0.5f;
		[SerializeField] private Vector2 flowerPatchRadius = new(0.8f, 2.6f);
		[SerializeField] private Vector2Int flowerPatchCount = new(60, 170);
		[SerializeField] private Vector2 flowerSize = new(0.2f, 0.32f);
		[Tooltip("꽃 판을 풀 위로 올리는 높이 (m)")]
		[SerializeField] private float flowerLift = 0.34f;
		[Tooltip("길에서 이 거리 (m) 안쪽엔 무리 중심을 두지 않음")]
		[SerializeField] private float flowerPatchPathMargin = 3f;
		[SerializeField] private float flowerPathMargin = 2.2f;

		[Header("Ferns")]
		[SerializeField] private Material fernMaterial;
		[SerializeField] private int fernSeed = 77;
		[SerializeField] private int fernPatches = 170;
		[SerializeField] private Rect fernArea = new(-40f, -6f, 80f, 70f);
		[SerializeField] private float fernPathMargin = 3f;
		[SerializeField] private Vector2Int fernPatchCount = new(3, 9);
		[SerializeField] private float fernPatchSpread = 2.2f;
		[SerializeField] private Vector2 fernSize = new(0.45f, 0.85f);

		[Header("Yellow spikes")]
		[SerializeField] private Material spikeMaterial;
		[SerializeField] private int spikeSeed = 78;
		[SerializeField] private int spikePatches = 80;
		[SerializeField] private Rect spikeArea = new(-38f, -4f, 76f, 60f);
		[SerializeField] private float spikePatchPathMargin = 3.2f;
		[SerializeField] private float spikePathMargin = 2.4f;
		[SerializeField] private Vector2 spikePatchRadius = new(1f, 3.5f);
		[SerializeField] private Vector2Int spikePatchCount = new(15, 50);
		[SerializeField] private Vector2 spikeSize = new(0.5f, 0.85f);
		[Tooltip("꽃대 판 너비 / 높이")]
		[SerializeField] private float spikeWidthRatio = 0.34f;

		private static float Range01(System.Random random, Vector2 range) => Mathf.Lerp(range.x, range.y, (float)random.NextDouble());

		private void BuildGrass()
		{
			System.Random random = new(grassSeed);
			int columns = Mathf.CeilToInt(grassArea.width / grassCellSize);
			int rows = Mathf.CeilToInt(grassArea.height / grassCellSize);
			GrassCell[] cells = new GrassCell[columns * rows];
			for (int i = 0; i < cells.Length; i++)
				cells[i] = new GrassCell();
			for (int attempt = 0; attempt < grassAttempts; attempt++)
			{
				float x = grassArea.xMin + (float)random.NextDouble() * grassArea.width;
				float z = grassArea.yMin + (float)random.NextDouble() * grassArea.height;
				Color32 control = ControlAt(x, z);
				float path = control.b / 255f;
				float earth = control.g / 255f;
				if ((float)random.NextDouble() < path * grassPathCull + earth * grassEarthCull)
					continue;
				float clump = Fractal(x * grassClumpScale, z * grassClumpScale, grassClumpSeed, 3);
				float near = Mathf.Clamp01(1f - Mathf.InverseLerp(grassNearRange.x, grassNearRange.y, z));
				if ((float)random.NextDouble() > (clump * 1.1f + 0.1f) * (0.45f + near * 0.55f))
					continue;
				float hue = Fractal(x * grassHueScale, z * grassHueScale, grassHueSeed, 2);
				float height = Range01(random, grassHeightRange) * (0.55f + clump * 0.8f) * (1f - earth * 0.45f);
				float width = Range01(random, grassWidthRange);
				float angle = (float)random.NextDouble() * Mathf.PI * 2f;
				Vector3 side = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * width;
				float bend = Range01(random, grassBendRange);
				Vector3 lean = new Vector3(grassWind.x + ((float)random.NextDouble() - 0.5f) * grassLeanJitter, 0f, grassWind.y + ((float)random.NextDouble() - 0.5f) * grassLeanJitter) * height * bend;
				Vector3 basePoint = new(x, HeightAt(x, z) - grassSink, z);
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
				int column = Mathf.Min(columns - 1, (int)((x - grassArea.xMin) / grassCellSize));
				int row = Mathf.Min(rows - 1, (int)((z - grassArea.yMin) / grassCellSize));
				cells[row * columns + column].AddBlade(basePoint, side, lean, height, isFlower, root, middle, tip);
			}
			for (int i = 0; i < cells.Length; i++)
			{
				if (cells[i].IsEmpty)
					continue;
				AddChildMesh("Grass " + i, cells[i].ToMesh("ForestGrass" + i), grassMaterial);
			}
		}

		private static Color32 Shaded(Color32 color, float shade)
			=> new((byte)Mathf.Min(255f, color.r * shade), (byte)Mathf.Min(255f, color.g * shade), (byte)Mathf.Min(255f, color.b * shade), color.a);

		private void BuildFlowers()
		{
			System.Random random = new(flowerSeed);
			CardBatch[] batches = new CardBatch[FLOWER_KIND_COUNT];
			for (int i = 0; i < FLOWER_KIND_COUNT; i++)
				batches[i] = new CardBatch();
			for (int patch = 0; patch < flowerPatches; patch++)
			{
				float centerX = flowerArea.xMin + (float)random.NextDouble() * flowerArea.width;
				float centerZ = flowerArea.yMin + (float)random.NextDouble() * flowerArea.height;
				if (DistanceToPath(centerX, centerZ) < flowerPatchPathMargin)
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
					if (DistanceToPath(x, z) < flowerPathMargin)
						continue;
					float size = Range01(random, flowerSize);
					Vector3 basePoint = new(x, HeightAt(x, z) + flowerLift * 0.8f, z);
					for (int card = 0; card < 2; card++)
					{
						float cardAngle = (float)random.NextDouble() * Mathf.PI + card * Mathf.PI * 0.5f;
						Vector3 side = new Vector3(Mathf.Cos(cardAngle), 0f, Mathf.Sin(cardAngle)) * size * 0.5f;
						batches[kind].Add(basePoint, side, Vector3.up * (size * 1.3f + flowerLift * 0.2f));
					}
				}
			}
			for (int i = 0; i < FLOWER_KIND_COUNT; i++)
				AddChildMesh("Flowers " + i, batches[i].ToMesh("ForestFlowers" + i), flowerMaterials[i]);
		}

		private void BuildFerns()
		{
			System.Random random = new(fernSeed);
			CardBatch batch = new();
			for (int patch = 0; patch < fernPatches; patch++)
			{
				float centerX = fernArea.xMin + (float)random.NextDouble() * fernArea.width;
				float centerZ = fernArea.yMin + (float)random.NextDouble() * fernArea.height;
				if (DistanceToPath(centerX, centerZ) < fernPathMargin)
					continue;
				int count = random.Next(fernPatchCount.x, fernPatchCount.y);
				for (int k = 0; k < count; k++)
				{
					float x = centerX + ((float)random.NextDouble() - 0.5f) * fernPatchSpread;
					float z = centerZ + ((float)random.NextDouble() - 0.5f) * fernPatchSpread;
					Vector3 basePoint = new(x, HeightAt(x, z) - grassSink, z);
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
			AddChildMesh("Ferns", batch.ToMesh("ForestFerns"), fernMaterial);
		}

		private void BuildSpikes()
		{
			System.Random random = new(spikeSeed);
			CardBatch batch = new();
			for (int patch = 0; patch < spikePatches; patch++)
			{
				float centerX = spikeArea.xMin + (float)random.NextDouble() * spikeArea.width;
				float centerZ = spikeArea.yMin + (float)random.NextDouble() * spikeArea.height;
				if (DistanceToPath(centerX, centerZ) < spikePatchPathMargin)
					continue;
				float radius = Range01(random, spikePatchRadius);
				int count = random.Next(spikePatchCount.x, spikePatchCount.y);
				for (int k = 0; k < count; k++)
				{
					float angle = (float)random.NextDouble() * Mathf.PI * 2f;
					float distance = Mathf.Sqrt((float)random.NextDouble()) * radius;
					float x = centerX + Mathf.Cos(angle) * distance;
					float z = centerZ + Mathf.Sin(angle) * distance;
					if (DistanceToPath(x, z) < spikePathMargin)
						continue;
					float size = Range01(random, spikeSize);
					Vector3 basePoint = new(x, HeightAt(x, z), z);
					for (int card = 0; card < 2; card++)
					{
						float cardAngle = (float)random.NextDouble() * Mathf.PI + card * Mathf.PI * 0.5f;
						batch.Add(basePoint, new Vector3(Mathf.Cos(cardAngle), 0f, Mathf.Sin(cardAngle)) * size * spikeWidthRatio * 0.5f, Vector3.up * size);
					}
				}
			}
			AddChildMesh("Yellow spikes", batch.ToMesh("ForestSpikes"), spikeMaterial);
		}

		// 풀잎 다섯 꼭짓점: 뿌리 둘, 허리 둘, 끝 하나. 정점 알파는 뿌리 0, 끝 255 (셰이더 명암)
		private class GrassCell
		{
			private readonly List<Vector3> vertices = new();
			private readonly List<Color32> colors = new();
			private readonly List<int> triangles = new();

			public bool IsEmpty => vertices.Count == 0;

			public void AddBlade(Vector3 basePoint, Vector3 side, Vector3 lean, float height, bool isFlower, Color32 root, Color32 middle, Color32 tip)
			{
				int first = vertices.Count;
				Vector3 middlePoint = basePoint + Vector3.up * height * 0.6f + lean * 0.22f;
				vertices.Add(basePoint - side);
				vertices.Add(basePoint + side);
				vertices.Add(middlePoint - side * 0.7f);
				vertices.Add(middlePoint + side * 0.7f);
				vertices.Add(basePoint + Vector3.up * height + lean + side * (isFlower ? 0.9f : 0f));
				colors.Add(root);
				colors.Add(root);
				colors.Add(middle);
				colors.Add(middle);
				colors.Add(tip);
				triangles.AddRange(new[] { first, first + 2, first + 1, first + 1, first + 2, first + 3, first + 2, first + 4, first + 3 });
			}

			public Mesh ToMesh(string meshName)
			{
				Mesh mesh = new() { name = meshName, hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };
				mesh.SetVertices(vertices);
				mesh.SetColors(colors);
				mesh.SetTriangles(triangles, 0);
				mesh.RecalculateBounds();
				return mesh;
			}
		}

		// 세운 사각 판 묶음. 법선은 위로 고정해 판 방향과 무관하게 부드러운 명암
		private class CardBatch
		{
			private readonly List<Vector3> vertices = new();
			private readonly List<Vector2> uvs = new();
			private readonly List<Vector3> normals = new();
			private readonly List<int> triangles = new();

			public void Add(Vector3 basePoint, Vector3 side, Vector3 up)
			{
				int first = vertices.Count;
				vertices.Add(basePoint - side);
				vertices.Add(basePoint + side);
				vertices.Add(basePoint + side + up);
				vertices.Add(basePoint - side + up);
				uvs.Add(new Vector2(0f, 0f));
				uvs.Add(new Vector2(1f, 0f));
				uvs.Add(new Vector2(1f, 1f));
				uvs.Add(new Vector2(0f, 1f));
				for (int i = 0; i < 4; i++)
					normals.Add(Vector3.up);
				triangles.AddRange(new[] { first, first + 2, first + 1, first, first + 3, first + 2 });
			}

			public Mesh ToMesh(string meshName)
			{
				Mesh mesh = new() { name = meshName, hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };
				mesh.SetVertices(vertices);
				mesh.SetUVs(0, uvs);
				mesh.SetNormals(normals);
				mesh.SetTriangles(triangles, 0);
				mesh.RecalculateBounds();
				return mesh;
			}
		}
	}
}

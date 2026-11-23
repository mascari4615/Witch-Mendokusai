using UnityEngine;

namespace WitchMendokusai
{
	// 부서진 물건의 조각 모양. 상자는 판자와 쪼개진 끝, 통은 휜 널판과 쇠띠, 항아리는 도기 조각, 바구니는 엮은 조각과 열매, 책은 종이와 표지, 약병 상자는 판자와 유리 조각
	// 크기 1 기준 (가장 긴 변 약 1), 중심이 원점, 납작한 면이 아래 (누우면 얇은 쪽이 위아래)
	public static class BreakableShardShapes
	{
		// 종류마다 만드는 조각 모양 수
		public const int SHARDS_PER_VARIANT = 3;

		public static void Build(BreakableMeshBuilder builder, BreakableShape shape, int[] rows, int shardIndex)
		{
			int needed = MinimumRows(shape);
			if (rows.Length < needed)
				throw new System.ArgumentException($"{shape} 조각은 색 칸이 {needed} 개 이상 필요한데 {rows.Length} 개", nameof(rows));
			builder.SetMatrix(Matrix4x4.identity);
			switch (shape)
			{
				case BreakableShape.Crate:
					if (shardIndex == 2)
						AddStick(builder, rows[1]);
					else
						AddPlank(builder, rows[0]);
					break;
				case BreakableShape.Barrel:
					if (shardIndex == 2)
						AddBand(builder, rows[1]);
					else
						AddStave(builder, rows[0]);
					break;
				case BreakableShape.Pot:
					AddPottery(builder, shardIndex == 1 ? rows[1] : rows[0], rows[2]);
					break;
				case BreakableShape.Basket:
					if (shardIndex == 2 && rows.Length > 3)
						AddFruit(builder, rows[3]);
					else
						AddWeave(builder, rows[0], rows[1]);
					break;
				case BreakableShape.BookStack:
					if (shardIndex == 0)
						AddPage(builder, rows[0]);
					else
						AddCover(builder, rows[1 + builder.Random.Next(rows.Length - 1)], rows[0]);
					break;
				case BreakableShape.PotionCrate:
					if (shardIndex == 2)
						AddGlass(builder, rows[3 + builder.Random.Next(rows.Length - 3)]);
					else
						AddPlank(builder, rows[0]);
					break;
				default:
					throw new System.ArgumentOutOfRangeException(nameof(shape), shape, null);
			}
		}

		// 모양마다 읽는 색 칸 수. 바구니 열매 (4번째) 는 없으면 엮은 조각으로 대신해서 셈에 안 넣음
		private static int MinimumRows(BreakableShape shape) => shape switch
		{
			BreakableShape.Crate => 2,
			BreakableShape.Barrel => 2,
			BreakableShape.Pot => 3,
			BreakableShape.Basket => 2,
			BreakableShape.BookStack => 2,
			BreakableShape.PotionCrate => 4,
			_ => throw new System.ArgumentOutOfRangeException(nameof(shape), shape, null),
		};

		// 판자 한 쪽 끝이 쪼개져 가시 둘
		private static void AddPlank(BreakableMeshBuilder builder, int row)
		{
			float length = builder.Range(.75f, .9f);
			float width = builder.Range(.26f, .34f);
			float thick = builder.Range(.08f, .12f);
			builder.AddBox(new Vector3(-(1f - length) * .5f, 0f, 0f), new Vector3(length, thick, width), Quaternion.identity, row);
			float splinter = 1f - length;
			builder.AddBox(new Vector3(length * .5f - (1f - length) * .5f + splinter * .45f, 0f, width * .22f), new Vector3(splinter * 1.1f, thick * .8f, width * .38f), Quaternion.Euler(0f, builder.Range(-12f, 12f), 0f), row);
			builder.AddBox(new Vector3(length * .5f - (1f - length) * .5f + splinter * .25f, 0f, -width * .25f), new Vector3(splinter * .6f, thick * .8f, width * .3f), Quaternion.Euler(0f, builder.Range(-12f, 12f), 0f), row);
		}

		// 틀 기둥 조각. 가늘고 길게
		private static void AddStick(BreakableMeshBuilder builder, int row)
		{
			builder.AddBox(Vector3.zero, new Vector3(1f, builder.Range(.12f, .16f), builder.Range(.12f, .16f)), Quaternion.Euler(0f, 0f, builder.Range(-4f, 4f)), row);
		}

		// 통 널판. 짧은 판 셋을 살짝 굽혀 이음
		private static void AddStave(BreakableMeshBuilder builder, int row)
		{
			float bend = builder.Range(6f, 12f);
			float segment = .34f;
			float width = builder.Range(.26f, .32f);
			for (int i = -1; i <= 1; i++)
			{
				float angle = bend * i;
				float lift = Mathf.Abs(i) * .035f;
				builder.AddBox(new Vector3(i * segment * .96f, lift, 0f), new Vector3(segment, .09f, width), Quaternion.Euler(0f, 0f, -angle), row);
			}
		}

		// 쇠띠 토막
		private static void AddBand(BreakableMeshBuilder builder, int row)
		{
			builder.AddBox(Vector3.zero, new Vector3(builder.Range(.7f, .95f), .05f, .14f), Quaternion.Euler(0f, 0f, builder.Range(-8f, 8f)), row);
		}

		// 도기 조각. 들쭉날쭉한 다각형 판, 겉은 흙색이나 무늬, 속은 안쪽 색
		private static void AddPottery(BreakableMeshBuilder builder, int outerRow, int innerRow)
		{
			int corners = builder.Random.Next(3, 6);
			Vector3[] outline = new Vector3[corners];
			for (int i = 0; i < corners; i++)
			{
				float angle = (i + builder.Range(-.25f, .25f)) / corners * Mathf.PI * 2f;
				float radius = builder.Range(.32f, .55f);
				outline[i] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
			}
			AddPrism(builder, outline, .08f, outerRow, innerRow, outerRow, .06f);
		}

		// 엮은 조각. 판에 가로 띠 둘
		private static void AddWeave(BreakableMeshBuilder builder, int weaveRow, int rimRow)
		{
			Vector3[] outline = { new(-.5f, 0f, -.3f), new(.45f, 0f, -.35f), new(.5f, 0f, .25f), new(-.4f, 0f, .32f) };
			AddPrism(builder, outline, .07f, weaveRow, weaveRow, weaveRow, 0f);
			builder.AddBox(new Vector3(0f, .045f, -.12f), new Vector3(.9f, .03f, .07f), Quaternion.identity, rimRow);
			builder.AddBox(new Vector3(0f, .045f, .12f), new Vector3(.86f, .03f, .07f), Quaternion.identity, rimRow);
		}

		// 바구니에서 쏟아진 열매
		private static void AddFruit(BreakableMeshBuilder builder, int row)
		{
			int sides = 7;
			float radius = .5f;
			float[] heights = { -radius, -radius * .7f, 0f, radius * .7f, radius };
			float[] radii = { 0f, radius * .7f, radius, radius * .7f, 0f };
			builder.AddLathe(Vector3.zero, heights, radii, sides, new[] { row, row, row, row }, -1, -1, 0f);
		}

		// 찢긴 종이 한 장
		private static void AddPage(BreakableMeshBuilder builder, int row)
		{
			Vector3[] outline = { new(-.42f, 0f, -.5f), new(.38f, 0f, -.46f), new(.42f, 0f, .2f), new(.2f, 0f, .5f), new(-.4f, 0f, .44f) };
			AddPrism(builder, outline, .025f, row, row, row, 0f);
		}

		// 표지 조각. 겉은 표지 색, 붙은 종이 몇 장
		private static void AddCover(BreakableMeshBuilder builder, int coverRow, int pageRow)
		{
			builder.AddBox(Vector3.zero, new Vector3(.8f, .05f, .6f), Quaternion.identity, coverRow);
			builder.AddBox(new Vector3(.03f, .045f, 0f), new Vector3(.7f, .04f, .54f), Quaternion.identity, pageRow);
		}

		// 유리 조각. 얇고 뾰족한 삼각
		private static void AddGlass(BreakableMeshBuilder builder, int row)
		{
			Vector3[] outline = { new(-.5f, 0f, -.15f), new(.5f, 0f, -.05f), new(builder.Range(-.1f, .2f), 0f, .35f) };
			AddPrism(builder, outline, .04f, row, row, row, 0f);
		}

		// 바닥 다각형을 두께만큼 올린 판. 윗면 가운데를 bulge 만큼 띄워 살짝 휜 조각처럼
		private static void AddPrism(BreakableMeshBuilder builder, Vector3[] outline, float thickness, int topRow, int bottomRow, int sideRow, float bulge)
		{
			Vector3 up = Vector3.up * thickness * .5f;
			Vector3 center = Vector3.zero;
			foreach (Vector3 point in outline)
				center += point;
			center /= outline.Length;
			Vector3 topCenter = center + up + Vector3.up * bulge;
			for (int i = 0; i < outline.Length; i++)
			{
				Vector3 a = outline[i];
				Vector3 b = outline[(i + 1) % outline.Length];
				Facing(builder, topCenter, a + up, b + up, Vector3.up, topRow);
				Facing(builder, center - up, a - up, b - up, Vector3.down, bottomRow);
				Vector3 outward = Vector3.Cross(Vector3.up, b - a);
				if (Vector3.Dot(outward, (a + b) * .5f - center) < 0f)
					outward = -outward;
				FacingQuad(builder, a - up, b - up, b + up, a + up, outward, sideRow);
			}
		}

		// 바깥 방향을 보도록 순서를 맞춘 삼각형
		private static void Facing(BreakableMeshBuilder builder, Vector3 a, Vector3 b, Vector3 c, Vector3 outward, int row)
		{
			if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f)
				builder.AddTriangle(a, c, b, row);
			else
				builder.AddTriangle(a, b, c, row);
		}

		private static void FacingQuad(BreakableMeshBuilder builder, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, int row)
		{
			if (Vector3.Dot(Vector3.Cross(b - a, d - a), outward) < 0f)
				builder.AddQuad(a, d, c, b, row);
			else
				builder.AddQuad(a, b, c, d, row);
		}
	}
}

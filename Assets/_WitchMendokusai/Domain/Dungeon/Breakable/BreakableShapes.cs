using UnityEngine;

namespace WitchMendokusai
{
	// 종류별 모양 조립. 비율과 개수는 씨앗 난수로 변주, 원점은 바닥 가운데
	// rows 는 팔레트 칸 순서 그대로의 색 줄 (BreakableKind.Palette 툴팁)
	public static class BreakableShapes
	{
		// 모양마다 필요한 팔레트 칸 최소 수. 모자라면 생성 전에 실패
		public static int RequiredPalette(BreakableShape shape) => shape switch
		{
			BreakableShape.BookStack => 2,
			BreakableShape.PotionCrate => 4,
			_ => 3,
		};

		public static void Build(BreakableMeshBuilder builder, BreakableShape shape, Vector3 size, int[] rows)
		{
			switch (shape)
			{
				case BreakableShape.Crate:
					BuildCrate(builder, size, rows, rows[2], true);
					break;
				case BreakableShape.Barrel:
					BuildBarrel(builder, size, rows);
					break;
				case BreakableShape.Pot:
					BuildPot(builder, size, rows);
					break;
				case BreakableShape.Basket:
					BuildBasket(builder, size, rows);
					break;
				case BreakableShape.BookStack:
					BuildBookStack(builder, size, rows);
					break;
				case BreakableShape.PotionCrate:
					BuildPotionCrate(builder, size, rows);
					break;
				default:
					throw new System.ArgumentOutOfRangeException(nameof(shape), shape, null);
			}
		}

		private static int Row(int[] rows, int index) => rows[index];

		private static int PickRow(BreakableMeshBuilder builder, int[] rows, int firstIndex) => rows[firstIndex + builder.Random.Next(rows.Length - firstIndex)];

		// 판자 상자. 판자 수, 틀 굵기, 대각 보강대, 판자마다 살짝 어긋남
		private static void BuildCrate(BreakableMeshBuilder builder, Vector3 size, int[] rows, int innerRow, bool withLid)
		{
			int plankRow = Row(rows, 0);
			int frameRow = Row(rows, 1);
			float frame = Mathf.Min(size.x, size.z) * builder.Range(.08f, .12f);
			float plankDepth = frame * .55f;
			int plankCount = builder.Random.Next(3, 6);
			float gap = size.y * builder.Range(.015f, .035f);

			builder.AddBox(new Vector3(0f, size.y * .5f, 0f), new Vector3(size.x - frame, size.y - frame * .5f, size.z - frame), Quaternion.identity, innerRow);

			float plankHeight = (size.y - frame * 2f - gap * (plankCount - 1)) / plankCount;
			for (int side = 0; side < 4; side++)
			{
				Quaternion facing = Quaternion.Euler(0f, side * 90f, 0f);
				float width = side % 2 == 0 ? size.x : size.z;
				float depth = side % 2 == 0 ? size.z : size.x;
				for (int plank = 0; plank < plankCount; plank++)
				{
					float y = frame + plankHeight * .5f + plank * (plankHeight + gap);
					float skew = builder.Range(-2.5f, 2.5f);
					Vector3 center = facing * new Vector3(0f, 0f, depth * .5f - plankDepth * .5f) + Vector3.up * (y + builder.Range(-gap, gap) * .3f);
					builder.AddBox(center, new Vector3(width - frame * 2f, plankHeight, plankDepth), facing * Quaternion.Euler(0f, 0f, skew), plankRow);
				}
				if (builder.Random.NextDouble() < .5)
				{
					float diagonal = Mathf.Sqrt(width * width + size.y * size.y) - frame * 2f;
					float angle = Mathf.Atan2(size.y - frame * 2f, width - frame * 2f) * Mathf.Rad2Deg * (builder.Random.NextDouble() < .5 ? 1f : -1f);
					Vector3 center = facing * new Vector3(0f, 0f, depth * .5f) + Vector3.up * size.y * .5f;
					builder.AddBox(center, new Vector3(diagonal, frame * .8f, plankDepth * .8f), facing * Quaternion.Euler(0f, 0f, angle), frameRow);
				}
			}

			for (int corner = 0; corner < 4; corner++)
			{
				float x = (corner % 2 == 0 ? -1f : 1f) * (size.x - frame) * .5f;
				float z = (corner < 2 ? -1f : 1f) * (size.z - frame) * .5f;
				builder.AddBox(new Vector3(x, size.y * .5f, z), new Vector3(frame, size.y, frame), Quaternion.identity, frameRow);
			}
			// 가로대는 기둥 안쪽에서 끝나고 높이를 달리해 같은 평면 겹침 없음
			for (int level = 0; level < 2; level++)
			{
				float y = level == 0 ? frame * .5f : size.y - frame * .5f;
				float alongX = frame * .94f;
				float alongZ = frame * .88f;
				builder.AddBox(new Vector3(0f, y, (size.z - frame) * .5f), new Vector3(size.x - frame * 1.5f, alongX, frame * 1.05f), Quaternion.identity, frameRow);
				builder.AddBox(new Vector3(0f, y, -(size.z - frame) * .5f), new Vector3(size.x - frame * 1.5f, alongX, frame * 1.05f), Quaternion.identity, frameRow);
				builder.AddBox(new Vector3((size.x - frame) * .5f, y, 0f), new Vector3(frame * 1.05f, alongZ, size.z - frame * 1.5f), Quaternion.identity, frameRow);
				builder.AddBox(new Vector3(-(size.x - frame) * .5f, y, 0f), new Vector3(frame * 1.05f, alongZ, size.z - frame * 1.5f), Quaternion.identity, frameRow);
			}

			if (withLid == false)
				return;
			int lidCount = builder.Random.Next(3, 5);
			float lidWidth = (size.x - frame * 2f) / lidCount;
			for (int lid = 0; lid < lidCount; lid++)
			{
				float x = -size.x * .5f + frame + lidWidth * (lid + .5f);
				float lift = builder.Range(0f, frame * .25f);
				builder.AddBox(new Vector3(x, size.y - frame * .3f + lift, 0f), new Vector3(lidWidth * .92f, frame * .5f, size.z - frame * 2f), Quaternion.Euler(0f, builder.Range(-1.5f, 1.5f), 0f), plankRow);
			}
		}

		// 배가 부른 통. 판자 면 수, 배 둘레, 쇠띠 수
		private static void BuildBarrel(BreakableMeshBuilder builder, Vector3 size, int[] rows)
		{
			int staveRow = Row(rows, 0);
			int bandRow = Row(rows, 1);
			int lidRow = Row(rows, 2);
			int sides = builder.Random.Next(8, 11);
			float radius = Mathf.Min(size.x, size.z) * .5f;
			float bulge = builder.Range(.1f, .2f);
			float phase = (float)builder.Random.NextDouble();
			float[] heights = { 0f, size.y * .2f, size.y * .5f, size.y * .8f, size.y };
			float[] radii = { radius * (1f - bulge), radius * (1f - bulge * .35f), radius, radius * (1f - bulge * .35f), radius * (1f - bulge) };
			builder.AddLathe(Vector3.zero, heights, radii, sides, new[] { staveRow, staveRow, staveRow, staveRow }, staveRow, lidRow, phase);

			int bandCount = builder.Random.Next(2, 4);
			for (int band = 0; band < bandCount; band++)
			{
				float ratio = bandCount == 2 ? (band == 0 ? .14f : .86f) : (band == 0 ? .12f : band == 1 ? .5f : .88f);
				float bandHeight = size.y * .07f;
				float bandRadius = Mathf.Lerp(radius * (1f - bulge), radius, 1f - Mathf.Abs(ratio - .5f) * 2f) * 1.04f;
				float low = size.y * ratio - bandHeight * .5f;
				builder.AddLathe(Vector3.zero, new[] { low, low + bandHeight }, new[] { bandRadius, bandRadius }, sides, new[] { bandRow }, -1, -1, phase);
			}
		}

		// 항아리. 몸통, 어깨, 목, 입술 단면을 변주, 무늬 띠 하나
		private static void BuildPot(BreakableMeshBuilder builder, Vector3 size, int[] rows)
		{
			int clayRow = Row(rows, 0);
			int stripeRow = Row(rows, 1);
			int innerRow = Row(rows, 2);
			int sides = builder.Random.Next(7, 10);
			float radius = Mathf.Min(size.x, size.z) * .5f;
			float belly = builder.Range(.3f, .45f);
			float neck = builder.Range(.38f, .55f);
			float phase = (float)builder.Random.NextDouble();
			float[] heights = { 0f, size.y * belly * .45f, size.y * belly, size.y * (belly + .2f), size.y * .74f, size.y * .86f, size.y };
			float[] radii = { radius * .5f, radius * .85f, radius, radius * .96f, radius * .72f, radius * neck, radius * (neck + .12f) };
			int stripeBand = builder.Random.Next(2, 4);
			int[] bandRows = new int[heights.Length - 1];
			for (int band = 0; band < bandRows.Length; band++)
				bandRows[band] = band == stripeBand ? stripeRow : clayRow;
			builder.AddLathe(Vector3.zero, heights, radii, sides, bandRows, clayRow, innerRow, phase);
		}

		// 바구니. 위로 벌어진 몸통, 굵은 테두리, 위에 담긴 둥근 열매
		private static void BuildBasket(BreakableMeshBuilder builder, Vector3 size, int[] rows)
		{
			int weaveRow = Row(rows, 0);
			int rimRow = Row(rows, 1);
			int fillRow = Row(rows, 2);
			int sides = builder.Random.Next(9, 13);
			float radius = Mathf.Min(size.x, size.z) * .5f;
			float taper = builder.Range(.68f, .82f);
			float phase = (float)builder.Random.NextDouble();
			int bands = builder.Random.Next(3, 5);
			float[] heights = new float[bands + 1];
			float[] radii = new float[bands + 1];
			int[] bandRows = new int[bands];
			for (int band = 0; band <= bands; band++)
			{
				float ratio = (float)band / bands;
				heights[band] = size.y * .8f * ratio;
				radii[band] = radius * Mathf.Lerp(taper, 1f, ratio);
			}
			for (int band = 0; band < bands; band++)
				bandRows[band] = weaveRow;
			builder.AddLathe(Vector3.zero, heights, radii, sides, bandRows, weaveRow, -1, phase);
			builder.AddDisc(Vector3.up * size.y * .78f, radius * Mathf.Lerp(taper, 1f, .78f / .8f), sides, weaveRow, phase, true);
			float rimLow = size.y * .8f - size.y * .05f;
			float rimHigh = size.y * .8f + size.y * .04f;
			builder.AddLathe(Vector3.zero, new[] { rimLow, rimHigh }, new[] { radius * 1.07f, radius * 1.07f }, sides, new[] { rimRow }, rimRow, -1, phase);
			builder.AddLathe(Vector3.zero, new[] { rimHigh, rimHigh }, new[] { radius * 1.07f, radius * .9f }, sides, new[] { rimRow }, -1, -1, phase);

			int fruitCount = builder.Random.Next(3, 6);
			for (int fruit = 0; fruit < fruitCount; fruit++)
			{
				float angle = (float)builder.Random.NextDouble() * Mathf.PI * 2f;
				float distance = radius * builder.Range(0f, .55f);
				float fruitRadius = radius * builder.Range(.26f, .36f);
				Vector3 center = new(Mathf.Cos(angle) * distance, size.y * .78f, Mathf.Sin(angle) * distance);
				AddBall(builder, center, fruitRadius, builder.Random.Next(5, 7), FruitRow(builder, rows, fillRow));
			}
		}

		private static int FruitRow(BreakableMeshBuilder builder, int[] rows, int fillRow) => rows.Length > 3 && builder.Random.NextDouble() < .4 ? Row(rows, 3) : fillRow;

		private static void AddBall(BreakableMeshBuilder builder, Vector3 center, float radius, int sides, int row)
		{
			float[] heights = { -radius * .9f, -radius * .45f, radius * .45f, radius * .9f };
			float[] radii = { radius * .3f, radius * .9f, radius * .9f, radius * .3f };
			builder.AddLathe(center, heights, radii, sides, new[] { row, row, row }, row, row, (float)builder.Random.NextDouble());
		}

		// 책 더미. 권수, 크기, 비틀림, 표지 색
		private static void BuildBookStack(BreakableMeshBuilder builder, Vector3 size, int[] rows)
		{
			int pageRow = Row(rows, 0);
			int count = builder.Random.Next(3, 7);
			float y = 0f;
			float twist = builder.Range(0f, 360f);
			for (int book = 0; book < count; book++)
			{
				float width = size.x * builder.Range(.72f, 1f);
				float depth = size.z * builder.Range(.68f, .95f);
				float height = size.y / count * builder.Range(.8f, 1.2f);
				float cover = Mathf.Min(height * .18f, .03f);
				Quaternion rotation = Quaternion.Euler(0f, twist + builder.Range(-22f, 22f), 0f);
				Vector3 offset = new(builder.Range(-.06f, .06f) * size.x, 0f, builder.Range(-.06f, .06f) * size.z);
				int coverRow = PickRow(builder, rows, 1);
				Vector3 baseCenter = offset + Vector3.up * y;
				builder.AddBox(baseCenter + Vector3.up * height * .5f + rotation * new Vector3(cover * .5f, 0f, 0f), new Vector3(width - cover, height - cover * 2f, depth - cover * 2f), rotation, pageRow);
				builder.AddBox(baseCenter + Vector3.up * cover * .5f, new Vector3(width, cover, depth), rotation, coverRow);
				builder.AddBox(baseCenter + Vector3.up * (height - cover * .5f), new Vector3(width, cover, depth), rotation, coverRow);
				builder.AddBox(baseCenter + Vector3.up * height * .5f + rotation * new Vector3(-width * .5f + cover * .5f, 0f, 0f), new Vector3(cover, height - cover * .2f, depth * .99f), rotation, coverRow);
				y += height;
			}
		}

		// 뚜껑 없는 낮은 상자에 꽂힌 약병들
		private static void BuildPotionCrate(BreakableMeshBuilder builder, Vector3 size, int[] rows)
		{
			Vector3 crateSize = new(size.x, size.y * builder.Range(.42f, .52f), size.z);
			BuildCrate(builder, crateSize, rows, rows[1], false);
			int corkRow = Row(rows, 2);
			int columns = builder.Random.Next(2, 4);
			int lines = 2;
			float cellX = size.x * .8f / columns;
			float cellZ = size.z * .8f / lines;
			for (int column = 0; column < columns; column++)
			{
				for (int line = 0; line < lines; line++)
				{
					if (builder.Random.NextDouble() < .15)
						continue;
					float radius = Mathf.Min(cellX, cellZ) * builder.Range(.3f, .4f);
					float height = size.y * builder.Range(.55f, .8f);
					Vector3 origin = new(-size.x * .4f + cellX * (column + .5f), size.y * .12f, -size.z * .4f + cellZ * (line + .5f));
					int glassRow = PickRow(builder, rows, 3);
					int sides = builder.Random.Next(6, 9);
					float[] heights = { 0f, height * .55f, height * .7f, height * .85f };
					float[] radii = { radius, radius, radius * .4f, radius * .4f };
					builder.AddLathe(origin, heights, radii, sides, new[] { glassRow, glassRow, glassRow }, -1, glassRow, 0f);
					float corkHeight = height * .12f;
					builder.AddLathe(origin + Vector3.up * heights[3], new[] { 0f, corkHeight }, new[] { radius * .45f, radius * .42f }, sides, new[] { corkRow }, -1, corkRow, 0f);
				}
			}
		}
	}
}

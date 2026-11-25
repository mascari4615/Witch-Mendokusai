using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	public class GoopFireSpotsTest
	{
		private const float CELL = .5f;
		private const float MAX_WIDTH = 4.5f;

		private static List<GoopFireSpot> Build(List<Vector2Int> cells, int max = 40, Vector2 focus = default)
		{
			List<GoopFireSpot> spots = new();
			new GoopFireSpots().Build(cells, new GoopFireRules(CELL, 1.7f, .5f, 1f, MAX_WIDTH, max), focus, spots);
			return spots;
		}

		private static List<Vector2Int> Disc(float inner, float outer)
		{
			List<Vector2Int> cells = new();
			int reach = Mathf.CeilToInt(outer / CELL);
			for (int x = -reach; x <= reach; x++)
			{
				for (int y = -reach; y <= reach; y++)
				{
					float distance = new Vector2(x + .5f, y + .5f).magnitude * CELL;
					if (distance >= inner && distance <= outer)
						cells.Add(new Vector2Int(x, y));
				}
			}
			return cells;
		}

		// 불마다 폭의 절반 안에 있는 타는 칸이 그 원의 몫만큼은 있어야 함 (빈 곳에 크게 서지 않게)
		private static void AssertStandOnFire(List<Vector2Int> cells, List<GoopFireSpot> spots)
		{
			foreach (GoopFireSpot spot in spots)
			{
				int inside = 0;
				foreach (Vector2Int cell in cells)
					if (((new Vector2(cell.x + .5f, cell.y + .5f) * CELL) - spot.Center).magnitude <= spot.Width * .5f)
						inside++;
				float share = inside * CELL * CELL / (Mathf.PI * spot.Width * spot.Width * .25f);
				Assert.That(share, Is.GreaterThan(.25f), $"{spot.Center} {spot.Width}");
				Assert.That(spot.Width, Is.LessThanOrEqualTo(MAX_WIDTH));
			}
		}

		private static List<Vector2Int> Rect(int x, int y, int width, int height)
		{
			List<Vector2Int> cells = new();
			for (int i = 0; i < width; i++)
				for (int j = 0; j < height; j++)
					cells.Add(new Vector2Int(x + i, y + j));
			return cells;
		}

		[Test]
		public void Empty_has_no_spots() => Assert.That(Build(new List<Vector2Int>()), Is.Empty);

		[Test]
		public void Single_cell_is_one_cell_wide()
		{
			List<GoopFireSpot> spots = Build(new List<Vector2Int> { new(0, 0) });
			Assert.That(spots, Has.Count.EqualTo(1));
			Assert.That(spots[0].Width, Is.EqualTo(CELL).Within(.001f));
			Assert.That(spots[0].Center, Is.EqualTo(new Vector2(.25f, .25f)));
		}

		[Test]
		public void Round_clump_is_one_fire_at_its_middle()
		{
			List<GoopFireSpot> spots = Build(Rect(-3, -3, 6, 6));
			Assert.That(spots, Has.Count.EqualTo(1));
			Assert.That(spots[0].Center.magnitude, Is.LessThan(.001f));
			Assert.That(spots[0].Width, Is.InRange(2.9f, 4.4f));
		}

		[Test]
		public void Long_clump_is_split_along_its_length()
		{
			List<GoopFireSpot> spots = Build(Rect(0, 0, 18, 4));
			Assert.That(spots.Count, Is.InRange(4, 5));
			foreach (GoopFireSpot spot in spots)
				Assert.That(spot.Center.y, Is.EqualTo(1f).Within(.3f));
		}

		[Test]
		public void Bent_hollow_and_wide_clumps_stand_on_fire()
		{
			List<Vector2Int> bent = Rect(0, 0, 14, 3);
			bent.AddRange(Rect(11, 3, 3, 11));
			List<Vector2Int> cup = Rect(0, 0, 12, 2);
			cup.AddRange(Rect(0, 2, 2, 10));
			cup.AddRange(Rect(10, 2, 2, 10));
			List<Vector2Int> cross = Rect(6, 0, 3, 15);
			cross.AddRange(Rect(0, 6, 6, 3));
			cross.AddRange(Rect(9, 6, 6, 3));
			foreach (List<Vector2Int> cells in new[] { bent, cup, cross, Disc(2f, 3.2f), Disc(0f, 5f) })
			{
				List<GoopFireSpot> spots = Build(cells);
				Assert.That(spots.Count, Is.GreaterThan(1));
				AssertStandOnFire(cells, spots);
			}
		}

		[Test]
		public void Barrel_spill_is_one_fire()
		{
			List<Vector2Int> cells = Disc(0f, 1.75f);
			List<GoopFireSpot> spots = Build(cells);
			Assert.That(spots, Has.Count.EqualTo(1));
			Assert.That(spots[0].Width, Is.InRange(3f, 4f));
		}

		[Test]
		public void Diagonal_cells_join_and_far_cells_do_not()
		{
			Assert.That(Build(new List<Vector2Int> { new(0, 0), new(1, 1) }), Has.Count.EqualTo(1));
			List<Vector2Int> apart = Rect(0, 0, 4, 4);
			apart.AddRange(Rect(10, 0, 4, 4));
			Assert.That(Build(apart), Has.Count.EqualTo(2));
		}

		[Test]
		public void Over_the_limit_keeps_the_nearest()
		{
			List<Vector2Int> cells = Rect(0, 0, 6, 6);
			cells.AddRange(Rect(20, 0, 2, 2));
			cells.AddRange(Rect(40, 0, 4, 4));
			List<GoopFireSpot> spots = Build(cells, 2, new Vector2(20f, 0f));
			Assert.That(spots, Has.Count.EqualTo(2));
			Assert.That(spots[0].Center.x, Is.GreaterThan(19f));
			Assert.That(spots[1].Center.x, Is.InRange(9f, 12f));
		}

		[Test]
		public void Same_cell_twice_counts_once()
		{
			List<GoopFireSpot> spots = Build(new List<Vector2Int> { new(0, 0), new(0, 0) });
			Assert.That(spots, Has.Count.EqualTo(1));
			Assert.That(spots[0].Width, Is.EqualTo(CELL).Within(.001f));
		}
	}
}

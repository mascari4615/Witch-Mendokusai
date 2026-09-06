using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.Numerics;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 건물이 깔고 앉는 칸이 <b>엔진 없이도</b> 같게 나온다 (TASK-WM-215).
	/// 이 규칙이 갈리면 서버는 「빈 자리」, 화면은 「겹친다」고 말한다 — 같이 짓기에서 곧바로 사고.
	/// </summary>
	public sealed class BuildingFootprintTests
	{
		[Test]
		public void 한_칸짜리는_기준_칸만_차지한다()
		{
			List<Vector3Int> cells = BuildingFootprint.Cells(new Vector3Int(3, 0, 5), new Vector2Int(1, 1));

			Assert.AreEqual(1, cells.Count);
			Assert.AreEqual(new Vector3Int(3, 0, 5), cells[0]);
		}

		[Test]
		public void 여러_칸은_기준에서_왼쪽과_앞쪽으로_퍼진다()
		{
			List<Vector3Int> cells = BuildingFootprint.Cells(new Vector3Int(0, 0, 0), new Vector2Int(2, 3));

			Assert.AreEqual(6, cells.Count, "2 x 3 = 6 칸");
			Assert.Contains(new Vector3Int(0, 0, 0), cells);
			Assert.Contains(new Vector3Int(-1, 0, 0), cells, "X 는 음수 쪽으로");
			Assert.Contains(new Vector3Int(0, 0, 2), cells, "Z 는 양수 쪽으로");
			Assert.Contains(new Vector3Int(-1, 0, 2), cells);
		}

		[Test]
		public void 높이는_기준_칸을_따라간다()
		{
			List<Vector3Int> cells = BuildingFootprint.Cells(new Vector3Int(0, 7, 0), new Vector2Int(2, 2));

			foreach (Vector3Int cell in cells)
				Assert.AreEqual(7, cell.y, "적층해도 한 층만 차지한다");
		}

		[Test]
		public void 한_칸이라도_겹치면_못_짓는다()
		{
			HashSet<Vector3Int> occupied = new HashSet<Vector3Int> { new Vector3Int(-1, 0, 1) };

			bool blocked = BuildingFootprint.IsBlocked(new Vector3Int(0, 0, 0), new Vector2Int(2, 2), occupied);

			Assert.IsTrue(blocked);
		}

		[Test]
		public void 빈_자리면_지을_수_있다()
		{
			HashSet<Vector3Int> occupied = new HashSet<Vector3Int> { new Vector3Int(9, 0, 9) };

			bool blocked = BuildingFootprint.IsBlocked(new Vector3Int(0, 0, 0), new Vector2Int(2, 2), occupied);

			Assert.IsFalse(blocked);
		}

		[Test]
		public void 아무도_안_지은_땅에서는_언제나_지을_수_있다()
		{
			Assert.IsFalse(BuildingFootprint.IsBlocked(new Vector3Int(0, 0, 0), new Vector2Int(3, 3), null));
			Assert.IsFalse(BuildingFootprint.IsBlocked(new Vector3Int(0, 0, 0), new Vector2Int(3, 3), new HashSet<Vector3Int>()));
		}
	}
}

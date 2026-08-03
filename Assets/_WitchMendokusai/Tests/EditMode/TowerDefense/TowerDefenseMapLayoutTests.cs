using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 판 배치도 회귀 — 무한 맵의 핵심(창 안인가 · 가장자리까지 몇 칸인가 · 창이 자랄 때)인데
	/// 시험이 하나도 없었다 (TASK-WM-194).
	///
	/// ★ 여기가 틀리면 *무음으로* 어긋난다: 창 확장에서 좌표가 밀리면 세워둔 것이 통째로 옮겨지고,
	///   「창 안인가」가 어긋나면 화면이 거짓 초록불을 켠다(실제로 겪었다).
	/// </summary>
	public class TowerDefenseMapLayoutTests
	{
		private static TowerDefenseMapLayout Layout(int seed = 5, int width = 40, int length = 40)
		{
			TowerDefenseMapParameters parameters = TowerDefenseMapParameters.Default;
			parameters.Seed = seed;
			parameters.Width = width;
			parameters.Length = length;
			return TowerDefenseMapGenerator.Generate(parameters);
		}

		[Test]
		public void 칸과_월드_좌표는_왕복해도_같다()
		{
			// 배치 스냅이 전부 이 왕복 위에 서 있다.
			TowerDefenseMapLayout layout = Layout();

			foreach (Vector2Int cell in new[] { new Vector2Int(0, 0), new Vector2Int(7, 13), new Vector2Int(39, 39) })
				Assert.AreEqual(cell, layout.WorldToCell(layout.CellToWorld(cell)), $"{cell} 왕복이 깨진다.");
		}

		[Test]
		public void 창_안팎이_가장자리에서_갈린다()
		{
			// 미리보기 초록불이 이 판정에 걸린다 — 어긋나면 화면이 「여기 된다」고 거짓말한다.
			TowerDefenseMapLayout layout = Layout();

			Assert.IsTrue(layout.IsInsideWindow(layout.CorePosition), "코어가 창 밖일 리 없다.");
			Assert.IsFalse(layout.IsInsideWindow(layout.CellToWorld(new Vector2Int(-5, -5))), "창 밖인데 안이라고 한다.");
			Assert.IsFalse(layout.IsInsideWindow(layout.CellToWorld(new Vector2Int(200, 200))), "창 밖인데 안이라고 한다.");
		}

		[Test]
		public void 가장자리에_가까울수록_남은_칸이_적다()
		{
			// 창 확장 트리거가 이 값 하나로 돈다 — 뒤집히면 판이 영영 안 자라거나 계속 자란다.
			TowerDefenseMapLayout layout = Layout();

			int atCore = layout.CellsToWindowEdge(layout.CorePosition);
			int nearEdge = layout.CellsToWindowEdge(layout.CellToWorld(new Vector2Int(1, 1)));

			Assert.Greater(atCore, nearEdge, "가운데가 가장자리보다 여유가 적다면 값이 뒤집힌 것이다.");
			Assert.GreaterOrEqual(nearEdge, 0);
		}

		[Test]
		public void 창을_키워도_기존_칸은_그대로다()
		{
			// ★ 「원점 이동 금지」가 무한 맵 설계의 전제다. 좌표가 밀리면 세워둔 것이 통째로 옮겨지는데
			//   그건 화면에 아무 경고 없이 일어난다.
			TowerDefenseMapLayout before = Layout();
			Vector2Int probe = new Vector2Int(9, 12);
			bool blockedBefore = before.IsBlocked(probe);
			Vector2Int coreBefore = before.CoreCell;

			TowerDefenseMapLayout after = TowerDefenseMapLayout.Grown(before, 60, 60, cell => false);

			Assert.AreEqual(coreBefore, after.CoreCell, "창을 키웠더니 코어 칸이 옮겨졌다.");
			Assert.AreEqual(blockedBefore, after.IsBlocked(probe), "창을 키웠더니 기존 칸의 암반 여부가 바뀌었다.");
			Assert.AreEqual(60, after.Width);
			Assert.AreEqual(60, after.Length);
		}

		[Test]
		public void 창을_키우면_새_띠만_새로_채운다()
		{
			// 전부 다시 만들면 판이 갑자기 달라진다 — 새로 열린 띠만 물어봐야 한다.
			TowerDefenseMapLayout before = Layout();
			int obstaclesBefore = before.ObstacleCells.Count;

			// 새 띠는 전부 암반이라고 답한다 — 그럼 늘어난 수만큼만 늘어야 한다.
			TowerDefenseMapLayout after = TowerDefenseMapLayout.Grown(before, 50, 50, cell => true);

			int newBandCells = 50 * 50 - 40 * 40;
			Assert.AreEqual(obstaclesBefore + newBandCells, after.ObstacleCells.Count,
				"기존 칸까지 다시 물어봤거나, 새 띠를 빠뜨렸다.");
		}

		[Test]
		public void 창_밖_칸은_막힌_것으로_본다()
		{
			// 열리지 않은 곳은 「빈 땅」이 아니다 — 빈 땅으로 보면 길찾기가 판 밖으로 새어 나간다.
			TowerDefenseMapLayout layout = Layout();

			Assert.IsTrue(layout.IsBlocked(new Vector2Int(-1, 0)));
			Assert.IsTrue(layout.IsBlocked(new Vector2Int(0, 999)));
		}
	}
}

using NUnit.Framework;
using UnityEngine;
// ★ 좌표는 판정 쪽 (TASK-WM-214).
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2 = WitchMendokusai.Numerics.Vector2;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using Vector3Int = WitchMendokusai.Numerics.Vector3Int;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 커서를 따라다니는 설명 상자가 화면 밖으로 새지 않는가 — 커서가 없는 확인 도구 대신
	/// 이 계산이 증명한다(TASK-WM-194). 사람이 마우스를 끝까지 끌어보지 않아도 된다.
	/// </summary>
	public class TowerDefenseTooltipPlacementTests
	{
		private static readonly Vector2 Screen1080 = new(1920f, 1080f);
		private static readonly Vector2 Box = new(240f, 120f);
		private const float OFFSET = 18f;

		[Test]
		public void 가운데서는_커서_오른쪽_아래에_붙는다()
		{
			Vector2 placed = TowerDefenseTooltipPlacement.Resolve(new Vector2(960f, 540f), Screen1080, Box, OFFSET);

			Assert.AreEqual(978f, placed.x, 0.01f);
			Assert.AreEqual(558f, placed.y, 0.01f); // 1080-540+18
		}

		[Test]
		public void 오른쪽_끝에서는_왼쪽으로_뒤집힌다()
		{
			// 안 뒤집으면 상자가 화면 밖으로 잘려 나간다 — 정작 읽어야 할 때 못 읽는다.
			Vector2 placed = TowerDefenseTooltipPlacement.Resolve(new Vector2(1900f, 540f), Screen1080, Box, OFFSET);

			Assert.LessOrEqual(placed.x + Box.x, Screen1080.x, "오른쪽으로 넘쳤다.");
			Assert.Less(placed.x, 1900f, "커서 왼쪽으로 뒤집혀야 한다.");
		}

		[Test]
		public void 아래쪽_끝에서는_위로_뒤집힌다()
		{
			// 화면 좌표 y=20 = 바닥 근처 = UI 좌표로는 아래쪽.
			Vector2 placed = TowerDefenseTooltipPlacement.Resolve(new Vector2(960f, 20f), Screen1080, Box, OFFSET);

			Assert.LessOrEqual(placed.y + Box.y, Screen1080.y, "아래로 넘쳤다.");
		}

		[Test]
		public void 어느_모서리에서도_화면_안에_있다()
		{
			foreach (Vector2 corner in new[]
			{
				new Vector2(0f, 0f), new Vector2(1920f, 0f),
				new Vector2(0f, 1080f), new Vector2(1920f, 1080f),
			})
			{
				Vector2 placed = TowerDefenseTooltipPlacement.Resolve(corner, Screen1080, Box, OFFSET);

				Assert.GreaterOrEqual(placed.x, 0f, $"{corner} 에서 왼쪽으로 샜다.");
				Assert.GreaterOrEqual(placed.y, 0f, $"{corner} 에서 위로 샜다.");
				Assert.LessOrEqual(placed.x + Box.x, Screen1080.x, $"{corner} 에서 오른쪽으로 샜다.");
				Assert.LessOrEqual(placed.y + Box.y, Screen1080.y, $"{corner} 에서 아래로 샜다.");
			}
		}
	}
}

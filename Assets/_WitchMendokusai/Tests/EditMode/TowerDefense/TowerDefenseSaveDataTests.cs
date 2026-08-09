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
	/// 판 저장 회귀 — 저장의 목적은 「그 판을 그대로 돌려주는 것」이고, 그게 안 되면
	/// *되살리지 않는 편이 낫다*(깨진 판을 이어하는 것이 처음부터 하는 것보다 나쁘다). TASK-WM-194.
	/// </summary>
	public class TowerDefenseSaveDataTests
	{
		private static TowerDefenseSaveData Valid()
		{
			return new TowerDefenseSaveData
			{
				StageId = "TDS_0",
				MapSeed = 1234,
				MapWidth = 200,
				MapLength = 200,
				ElapsedSeconds = 200f,
				Lives = 8,
			};
		}

		[Test]
		public void 형식이_맞고_스테이지가_있으면_호환()
		{
			Assert.IsTrue(Valid().IsCompatible);
		}

		[Test]
		public void 형식_번호가_다르면_버린다()
		{
			// 옛 저장을 억지로 읽으면 「이어했는데 판이 이상하다」가 된다 — 그건 처음부터 하는 것보다 나쁘다.
			TowerDefenseSaveData save = Valid();
			save.Version = TowerDefenseSaveData.CURRENT_VERSION + 1;

			Assert.IsFalse(save.IsCompatible);
			Assert.IsFalse(save.IsResumable);
		}

		[Test]
		public void 스테이지가_없으면_버린다()
		{
			TowerDefenseSaveData save = Valid();
			save.StageId = string.Empty;

			Assert.IsFalse(save.IsCompatible);
		}

		[Test]
		public void 목숨이_0이면_이어할_수_없다()
		{
			// 끝난 판을 되살리면 「이어하기」가 거짓말이 된다.
			TowerDefenseSaveData save = Valid();
			save.Lives = 0;

			Assert.IsFalse(save.IsResumable);
		}

		[Test]
		public void 설명은_시간과_규모를_말한다()
		{
			TowerDefenseSaveData save = Valid();
			save.Buildings.Add(new TowerDefenseBuildingSave { Kind = 0, Position = Vector3.zero, Level = 1 });

			string text = save.Describe();

			StringAssert.Contains("3분", text);
			StringAssert.Contains("건물 1채", text);
			StringAssert.Contains("목숨 8", text);
		}

		[Test]
		public void 새_저장은_빈_목록을_갖는다()
		{
			// null 목록은 불러오는 쪽에서 조용히 터진다 — 빈 목록이 기본이어야 한다.
			TowerDefenseSaveData save = new();

			Assert.IsNotNull(save.Buildings);
			Assert.IsNotNull(save.TakenBoons);
			Assert.IsNotNull(save.DestroyedNestPositions);
		}
	}
}

using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 개척 최고 기록 규칙 회귀 — 무한 모드의 유일한 점수라 「기록이 줄어드는」 사고가 나면 게임이 무너진다.
	/// 저장 매체와 분리된 순수 규칙(TowerDefenseRecord). TASK-WM-194.
	/// </summary>
	public class TowerDefenseRecordTests
	{
		private const int STAGE_ID = 7;

		[Test]
		public void Best_기록없으면_0()
		{
			Assert.AreEqual(0, TowerDefenseRecord.Best(new Dictionary<int, int>(), STAGE_ID));
		}

		[Test]
		public void Submit_첫기록은_항상갱신()
		{
			Dictionary<int, int> records = new();

			bool updated = TowerDefenseRecord.Submit(records, STAGE_ID, 3, out int best);

			Assert.IsTrue(updated);
			Assert.AreEqual(3, best);
			Assert.AreEqual(3, records[STAGE_ID]);
		}

		[Test]
		public void Submit_더나쁜판은_기록을_덮지않는다()
		{
			Dictionary<int, int> records = new() { { STAGE_ID, 9 } };

			bool updated = TowerDefenseRecord.Submit(records, STAGE_ID, 4, out int best);

			Assert.IsFalse(updated);
			Assert.AreEqual(9, best, "갱신 실패해도 기존 최고를 그대로 알려줘야 배너가 거짓말을 안 한다.");
			Assert.AreEqual(9, records[STAGE_ID]);
		}

		[Test]
		public void Submit_동률은_갱신아님()
		{
			Dictionary<int, int> records = new() { { STAGE_ID, 5 } };

			bool updated = TowerDefenseRecord.Submit(records, STAGE_ID, 5, out int best);

			Assert.IsFalse(updated);
			Assert.AreEqual(5, best);
		}

		[Test]
		public void Submit_스테이지별로_따로_쌓인다()
		{
			Dictionary<int, int> records = new();

			TowerDefenseRecord.Submit(records, STAGE_ID, 6, out _);
			TowerDefenseRecord.Submit(records, STAGE_ID + 1, 2, out _);

			Assert.AreEqual(6, TowerDefenseRecord.Best(records, STAGE_ID));
			Assert.AreEqual(2, TowerDefenseRecord.Best(records, STAGE_ID + 1));
		}
	}
}

using NUnit.Framework;
using UnityEngine;
using WitchMendokusai.DomainSDK.Workshop;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-170 — 듀얼루프의 <b>다리</b>: 밤에 번 골드를 낮 채집 효율로 바꾸는 부분.
	///
	/// ★ 왜 이것만 따로 보나: 낮밤 교대와 밤 장사는 세계 시계가 있어야 돌지만, 이 다리는
	///   원장과 계수만 있으면 판정된다. 씬 없이 볼 수 있는 유일한 조각이고,
	///   <b>두 루프가 서로에게 의미가 있게 만드는 자리</b>라 값어치가 크다.
	///
	/// 값을 절대값으로 박지 않는다 — 기본 계수는 인스펙터에서 언제든 바뀐다.
	/// 대신 <b>관계</b>를 지킨다: 투자하면 오른다 / 더 투자해도 무한히는 안 오른다.
	/// </summary>
	public class WorkshopDirectorBridgeTests
	{
		private GameObject host;
		private WorkshopDirector director;

		[SetUp]
		public void SetUp()
		{
			host = new GameObject("WorkshopDirectorTestHost");
			director = host.AddComponent<WorkshopDirector>();
		}

		[TearDown]
		public void TearDown()
		{
			Object.DestroyImmediate(host);
		}

		private void EarnGold(int amount)
		{
			// 판매를 거쳐 골드를 만든다 — 원장에 직접 넣는 문은 없다(그게 맞다).
			director.Ledger.SellProduct(new WorkshopProduct(1, new MaterialCost[0], amount), 1);
		}

		[Test]
		public void 아무것도_안_했으면_아직_아무것도_안_돌았다()
		{
			Assert.AreEqual(0, director.LastNightProduced);
			Assert.AreEqual(0, director.Ledger.Gold);
			Assert.AreEqual(0, director.Ledger.GoldInvestedInDayEfficiency);
			Assert.AreEqual(DayNightPhase.Day, director.Phase); // 시계가 없으면 낮에 멈춰 있다.
			Assert.AreEqual(0, director.DayIndex);
		}

		[Test]
		public void 밤에_번_돈을_낮_효율에_투자하면_효율이_오른다()
		{
			float before = director.DayCollectionEfficiency;

			EarnGold(1000);
			Assert.IsTrue(director.Ledger.InvestInDayEfficiency(1000));

			Assert.Greater(director.DayCollectionEfficiency, before);
		}

		[Test]
		public void 돈이_없으면_투자가_안_되고_효율도_그대로다()
		{
			float before = director.DayCollectionEfficiency;

			Assert.IsFalse(director.Ledger.InvestInDayEfficiency(50)); // 번 게 없다.

			Assert.AreEqual(before, director.DayCollectionEfficiency);
			Assert.AreEqual(0, director.Ledger.GoldInvestedInDayEfficiency);
		}

		[Test]
		public void 아무리_부어도_효율이_무한히_오르지는_않는다()
		{
			EarnGold(1000000);
			director.Ledger.InvestInDayEfficiency(1000000);
			float huge = director.DayCollectionEfficiency;

			EarnGold(1000000);
			director.Ledger.InvestInDayEfficiency(1000000);

			// 상한이 없으면 밤 수익이 무한히 굴러 밸런스가 무너진다 — 더 부어도 안 오른다.
			Assert.AreEqual(huge, director.DayCollectionEfficiency);
		}

		[Test]
		public void 정련_단계가_없으면_값은_기본가_그대로다()
		{
			// 감독이 들고 있는 정련 계수로 재도, 단계가 없으면 값이 안 움직인다.
			int price = WorkshopRefinedPrice.Evaluate(100, null, director.RefiningCoefficients, 1f);
			Assert.AreEqual(100, price);
		}
	}
}

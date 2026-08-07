using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Workshop;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-170 — 「밤 한 번」이 실제로 재료를 줄이고 골드를 늘리는지.
	/// 씬도 시계도 안 띄운다 — 밤 장사는 순수 계산으로 뽑아 뒀다.
	/// </summary>
	public class WorkshopNightShiftTests
	{
		private static readonly MaterialId HERB = new MaterialId(1);
		private static readonly MaterialId GLASS = new MaterialId(2);

		private static WorkshopProduct Potion(int price)
		{
			return new WorkshopProduct(
				productId: 100,
				materials: new List<MaterialCost> { new MaterialCost(HERB, 2), new MaterialCost(GLASS, 1) },
				salePrice: price);
		}

		[Test]
		public void 재료가_되는_만큼_만들어_팔고_재고가_준다()
		{
			WorkshopLedger ledger = new WorkshopLedger();
			ledger.CollectMaterial(HERB, 5);   // 물약 2개분(4) + 남는 1
			ledger.CollectMaterial(GLASS, 3);

			int produced = WorkshopNightShift.Run(ledger, new List<WorkshopProduct> { Potion(30) });

			Assert.AreEqual(2, produced);
			Assert.AreEqual(60, ledger.Gold);
			Assert.AreEqual(1, ledger.GetStock(HERB));
			Assert.AreEqual(1, ledger.GetStock(GLASS));
		}

		[Test]
		public void 재료가_모자라면_아무것도_안_만들고_재고도_그대로다()
		{
			WorkshopLedger ledger = new WorkshopLedger();
			ledger.CollectMaterial(HERB, 1);   // 2개 필요한데 1개뿐
			ledger.CollectMaterial(GLASS, 9);

			int produced = WorkshopNightShift.Run(ledger, new List<WorkshopProduct> { Potion(30) });

			Assert.AreEqual(0, produced);
			Assert.AreEqual(0, ledger.Gold);
			Assert.AreEqual(1, ledger.GetStock(HERB));
			Assert.AreEqual(9, ledger.GetStock(GLASS));
		}

		[Test]
		public void 팔_상품이_하나도_없으면_조용히_아무_일도_안_일어난다()
		{
			WorkshopLedger ledger = new WorkshopLedger();
			ledger.CollectMaterial(HERB, 10);

			int produced = WorkshopNightShift.Run(ledger, new List<WorkshopProduct>());

			Assert.AreEqual(0, produced);
			Assert.AreEqual(0, ledger.Gold);
			Assert.AreEqual(10, ledger.GetStock(HERB));
		}

		[Test]
		public void 재료를_안_먹는_상품은_건너뛴다_안_그러면_밤이_안_끝난다()
		{
			// 레시피가 비면 「만들 수 있음」이 영원히 참이라 무한 골드 + 무한 루프가 된다.
			WorkshopProduct freeMoney = new WorkshopProduct(999, new List<MaterialCost>(), 1000);

			WorkshopLedger ledger = new WorkshopLedger();
			int produced = WorkshopNightShift.Run(ledger, new List<WorkshopProduct> { freeMoney });

			Assert.AreEqual(0, produced);
			Assert.AreEqual(0, ledger.Gold);
		}

		[Test]
		public void 원장이나_목록이_없으면_터지지_않고_0_이다()
		{
			Assert.AreEqual(0, WorkshopNightShift.Run(null, new List<WorkshopProduct>()));
			Assert.AreEqual(0, WorkshopNightShift.Run(new WorkshopLedger(), null));
		}
	}
}

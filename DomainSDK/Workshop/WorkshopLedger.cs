using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Workshop
{
	/// <summary>
	/// TASK-WM-170 Phase 0 — 듀얼루프(낮 마계 / 밤 공방) 자원 수지 원장. 순수 POCO (DomainSDK).
	/// 낮 산출(채집) → 재료 재고 → 밤 제조(재료 차감) → 판매(골드 +) → 다음 낮 효율 투자(골드 -) 1사이클 닫힘.
	///
	/// City CityEconomy 와 같은 "누계 재고 원장" 패턴(데이터주도 키, 음수 방지는 호출자/메서드 자체 책임).
	/// 차이: 공방은 단일 가게 미시 운영이라 골드 + 효율 누계까지 한 곳에서 묶는다.
	///
	/// 본격 슬라이스에선 ISavable 직렬화 + EventBus 이벤트 발행이 위에 얹힘 — Phase 0 은 순수 산술만.
	/// </summary>
	public sealed class WorkshopLedger
	{
		private readonly Dictionary<MaterialId, int> stock = new();

		public IReadOnlyDictionary<MaterialId, int> Stock => stock;

		/// <summary>판매로 누적된 골드. 효율 투자로 차감.</summary>
		public int Gold { get; private set; }

		/// <summary>지금까지 다음 낮 채집 효율에 투자한 누계 골드. <see cref="DayEfficiencyModel"/> 입력.</summary>
		public int GoldInvestedInDayEfficiency { get; private set; }

		public int GetStock(MaterialId material)
		{
			return stock.TryGetValue(material, out int amount) ? amount : 0;
		}

		/// <summary>낮 루프(마계 채집) 결과 — 재료 재고에 추가. 0/음수는 무효.</summary>
		public void CollectMaterial(MaterialId material, int amount)
		{
			if (amount <= 0)
			{
				return;
			}

			stock[material] = GetStock(material) + amount;
		}

		/// <summary>밤 루프(공방 제조) — 재료 충분하면 차감하고 true. 부족하면 재고 불변 + false.</summary>
		public bool TryManufacture(WorkshopProduct product)
		{
			if (ProductSale.CanProduce(stock, product) == false)
			{
				return false;
			}

			foreach (MaterialCost cost in product.Materials)
			{
				stock[cost.Material] = stock[cost.Material] - cost.Amount;
			}

			return true;
		}

		/// <summary>밤 루프 — 판매 매출을 골드에 누적. 음수 수량은 무효(외부 호출자 보호).</summary>
		public void SellProduct(WorkshopProduct product, int unitsSold)
		{
			Gold = Gold + ProductSale.Revenue(product, unitsSold);
		}

		/// <summary>다음 낮 채집 효율 강화 — 골드 부족 시 false (트랜잭션 보존).</summary>
		public bool InvestInDayEfficiency(int goldAmount)
		{
			if (goldAmount <= 0)
			{
				return false;
			}

			if (Gold < goldAmount)
			{
				return false;
			}

			Gold = Gold - goldAmount;
			GoldInvestedInDayEfficiency = GoldInvestedInDayEfficiency + goldAmount;
			return true;
		}

		/// <summary>현재 누적 투자에 따른 채집 효율 (호출자가 ScaleCollection 으로 base 채집량에 적용).</summary>
		public float CurrentDayEfficiency(DayEfficiencyCoefficients coefficients)
		{
			return DayEfficiencyModel.Evaluate(GoldInvestedInDayEfficiency, coefficients);
		}
	}
}

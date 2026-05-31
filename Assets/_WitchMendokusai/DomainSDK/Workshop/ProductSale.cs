using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Workshop
{
	/// <summary>
	/// TASK-WM-170 Phase 0 — 공방 상품 제조/판매 순수 함수. 상태 0, MonoBehaviour 무관, EditMode 직접.
	/// 재고 충분성 판정 + 매출 환산을 분리해 두면 <see cref="WorkshopLedger"/> 외 다른 사용처(시뮬레이션,
	/// 가격 미리보기 UI 등)에서도 같은 식을 빌려 쓸 수 있다.
	/// </summary>
	public static class ProductSale
	{
		/// <summary>주어진 재고로 상품 1개를 제조할 수 있는가 — 모든 재료가 요구량 이상이면 true.</summary>
		public static bool CanProduce(IReadOnlyDictionary<MaterialId, int> stock, WorkshopProduct product)
		{
			foreach (MaterialCost cost in product.Materials)
			{
				if (stock.TryGetValue(cost.Material, out int available) == false)
				{
					return false;
				}

				if (available < cost.Amount)
				{
					return false;
				}
			}

			return true;
		}

		/// <summary>주어진 판매 수량의 총 매출(골드). 음수 수량은 0 으로 환산(외부 호출자 보호).</summary>
		public static int Revenue(WorkshopProduct product, int unitsSold)
		{
			if (unitsSold <= 0)
			{
				return 0;
			}

			return product.SalePrice * unitsSold;
		}
	}
}

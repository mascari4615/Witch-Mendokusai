using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Workshop
{
	/// <summary>
	/// TASK-WM-170 Phase 0 — 공방 상품 정의(레시피 + 판매가). 순수 POCO (DomainSDK).
	/// 본격 슬라이스에선 WorkshopProductSO(Domain) 가 공급원, 모드가 새 상품 주입.
	///
	/// 가격은 정수 골드 — 정수 산술 결정성. 부동소수 가격이 필요하면 별도 모델로 분리(EditMode 잠금 깨짐 회피).
	/// </summary>
	public sealed class WorkshopProduct
	{
		public int ProductId { get; }
		public IReadOnlyList<MaterialCost> Materials { get; }
		public int SalePrice { get; }

		public WorkshopProduct(int productId, IReadOnlyList<MaterialCost> materials, int salePrice)
		{
			ProductId = productId;
			Materials = materials;
			SalePrice = salePrice;
		}
	}
}

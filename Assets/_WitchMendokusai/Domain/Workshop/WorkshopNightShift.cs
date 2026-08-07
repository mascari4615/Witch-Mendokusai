using System.Collections.Generic;
using WitchMendokusai.DomainSDK.Workshop;

namespace WitchMendokusai
{
	/// <summary>
	/// TASK-WM-170 — 밤 한 번의 장사: <b>만들 수 있는 만큼 만들어 파는</b> 계산.
	///
	/// ★ 왜 따로 있나: 「밤이 되면 무슨 일이 일어나는가」를 씬 부품 안에 묻어 두면 시험이 씬을 띄워야 한다.
	///   여긴 순수 계산이라 원장과 상품 목록만 주면 판정된다.
	///
	/// ★ 여기서 정하지 <b>않는</b> 것: 무엇을 팔지·얼마에 팔지·몇 개나 팔릴지는 <b>데이터</b>다
	///   (상품 에셋이 공급). 이 함수는 「재료가 되는 데까지」만 돌린다 — 손님 수요·인기 같은 개념은
	///   사용자가 가게 정체성을 정한 뒤에 얹을 자리다.
	/// </summary>
	public static class WorkshopNightShift
	{
		/// <summary>
		/// 상품 목록을 앞에서부터 훑으며, 재료가 되는 동안 계속 만들고 곧바로 판다.
		/// 만들어진 개수의 총합을 돌려준다(0 = 재료가 없어 아무것도 못 만든 밤).
		/// </summary>
		public static int Run(WorkshopLedger ledger, IReadOnlyList<WorkshopProduct> products)
		{
			if (ledger == null || products == null)
			{
				return 0;
			}

			int producedTotal = 0;

			for (int index = 0; index < products.Count; index++)
			{
				WorkshopProduct product = products[index];
				if (product == null)
				{
					continue;
				}

				// 재료가 안 줄어드는 상품(레시피가 비었거나 전부 0개)은 무한히 만들 수 있어 밤이 안 끝난다.
				// 그런 상품은 「공짜로 돈이 나오는 구멍」이라 아예 건너뛴다.
				if (ConsumesMaterials(product) == false)
				{
					continue;
				}

				while (ledger.TryManufacture(product) == true)
				{
					ledger.SellProduct(product, 1);
					producedTotal = producedTotal + 1;
				}
			}

			return producedTotal;
		}

		private static bool ConsumesMaterials(WorkshopProduct product)
		{
			if (product.Materials == null)
			{
				return false;
			}

			for (int index = 0; index < product.Materials.Count; index++)
			{
				if (product.Materials[index].Amount > 0)
				{
					return true;
				}
			}

			return false;
		}
	}
}

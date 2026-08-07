using System;
using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Workshop;

namespace WitchMendokusai
{
	/// <summary>
	/// TASK-WM-170 — 공방이 파는 상품 하나(레시피 + 판매가)를 <b>에셋으로</b> 정의한다.
	///
	/// ★ 왜 코드가 아니라 에셋인가: 「무엇을 파는 가게인가」는 <b>사용자(디자인) 영역</b>이다.
	///   여기에 상품을 하나라도 박아 넣으면 그게 곧 답을 정해 버린다. 그래서 <b>틀만 만들고 비워 둔다</b> —
	///   품목이 정해지면 에셋을 만들어 채우면 되고, 모드·UGC 도 같은 자리로 새 상품을 넣는다.
	/// </summary>
	[CreateAssetMenu(fileName = "WorkshopProduct", menuName = "WitchMendokusai/Workshop/상품")]
	public class WorkshopProductSO : ScriptableObject
	{
		/// <summary>인스펙터에서 「재료 무엇을 몇 개」를 적기 위한 한 줄. 순수 값 struct 는 인스펙터에 안 뜬다.</summary>
		[Serializable]
		public class MaterialLine
		{
			[Tooltip("재료 식별자 — 마계 전리품·재배·구매 어디서 온 것이든 같은 정수 키를 쓴다.")]
			public int MaterialId;

			[Tooltip("상품 1개를 만드는 데 필요한 개수.")]
			public int Amount = 1;
		}

		[Tooltip("상품 식별자. 저장·통계가 이 번호로 붙는다.")]
		[SerializeField] private int productId;

		[Tooltip("이 상품 1개를 만드는 데 드는 재료들.")]
		[SerializeField] private List<MaterialLine> materials = new List<MaterialLine>();

		[Tooltip("팔았을 때 들어오는 골드. 정수 — 소수 가격은 결정성이 깨져 일부러 안 쓴다.")]
		[SerializeField] private int salePrice;

		/// <summary>에셋의 내용을 순수 값으로 옮긴다 — 계산 층은 유니티를 모른다.</summary>
		public WorkshopProduct ToProduct()
		{
			List<MaterialCost> costs = new List<MaterialCost>(materials.Count);
			for (int index = 0; index < materials.Count; index++)
			{
				MaterialLine line = materials[index];
				if (line == null)
				{
					continue;
				}

				costs.Add(new MaterialCost(new MaterialId(line.MaterialId), line.Amount));
			}

			return new WorkshopProduct(productId, costs, salePrice);
		}
	}
}

using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Workshop;

namespace WitchMendokusai
{
	/// <summary>
	/// TASK-WM-170 — <b>시연용 더미</b>. 낮마다 약초가 들어오고 밤마다 물약을 팔아, 듀얼루프가 실제로
	/// 도는 걸 콘솔에서 볼 수 있게 한다.
	///
	/// ★ 로어 아니다. 「공방이 무엇을 파는 가게인가」는 사용자가 정할 문제라 코드가 안 정한다.
	///   여기 약초·물약은 <b>메커니즘이 돈다는 걸 보여주기 위한 자리표</b>다 — 자율 삶 층이 더미 주민으로
	///   같은 일을 한다. 진짜 상품 에셋이 생기면 이 부품째 지우면 된다(부트스트랩에서 토글).
	///
	/// ★ 낮 루프 대역: 진짜 낮(마계 채집)이 붙기 전까지 재료를 대신 넣어 준다.
	///   붙고 나면 이 자리는 사라지고 채집 결과가 그대로 원장으로 들어간다.
	/// </summary>
	public class WorkshopDemoTrickle : MonoBehaviour
	{
		[Tooltip("낮이 올 때마다 들어오는 재료 개수.")]
		[SerializeField] private int materialPerDay = 7;

		[Tooltip("재료 식별자(더미).")]
		[SerializeField] private int materialId = 900002;

		[Tooltip("상품 1개에 드는 재료 개수.")]
		[SerializeField] private int materialPerProduct = 2;

		[Tooltip("상품 하나 팔았을 때 들어오는 골드.")]
		[SerializeField] private int productPrice = 30;

		[Tooltip("상품 식별자(더미).")]
		[SerializeField] private int productId = 900001;

		[Tooltip("낮밤이 바뀔 때마다 콘솔에 남길지. 이게 꺼져 있으면 돌아도 눈에 안 보인다.")]
		[SerializeField] private bool logEachPhase = true;

		private WorkshopDirector director;

		/// <summary>코드로 얹을 때 더미 수치를 한 번에 넣는다(인스펙터로 놓았으면 안 불러도 된다).</summary>
		public void Configure(int demoProductId, int demoMaterialId, int demoMaterialPerDay, int demoMaterialPerProduct, int demoPrice)
		{
			productId = demoProductId;
			materialId = demoMaterialId;
			materialPerDay = demoMaterialPerDay;
			materialPerProduct = demoMaterialPerProduct;
			productPrice = demoPrice;
		}

		private void Start()
		{
			director = GetComponent<WorkshopDirector>();
			if (director == null)
			{
				return;
			}

			director.AddProduct(new WorkshopProduct(
				productId,
				new List<MaterialCost> { new MaterialCost(new MaterialId(materialId), materialPerProduct) },
				productPrice));

			director.OnPhaseChanged += OnPhaseChanged;

			// 첫 낮 몫은 지금 넣어 둔다 — 안 그러면 첫 밤이 빈손으로 지나간다.
			director.Ledger.CollectMaterial(new MaterialId(materialId), materialPerDay);
		}

		private void OnDestroy()
		{
			if (director != null)
			{
				director.OnPhaseChanged -= OnPhaseChanged;
			}
		}

		private void OnPhaseChanged(DayNightPhase phase)
		{
			if (phase == DayNightPhase.Day)
			{
				director.Ledger.CollectMaterial(new MaterialId(materialId), materialPerDay);

				if (logEachPhase == true)
				{
					Debug.Log($"[공방] 낮 {director.DayIndex}일차 — 재료 {materialPerDay} 들어옴"
						+ $" (재고 {director.Ledger.GetStock(new MaterialId(materialId))})");
				}

				return;
			}

			if (logEachPhase == true)
			{
				Debug.Log($"[공방] 밤 — {director.LastNightProduced}개 만들어 팔았다,"
					+ $" 골드 {director.Ledger.Gold}"
					+ $" (남은 재료 {director.Ledger.GetStock(new MaterialId(materialId))})");
			}
		}
	}
}

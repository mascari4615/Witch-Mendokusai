using UnityEngine;
using WitchMendokusai.DomainSDK.Act;

namespace WitchMendokusai
{
	// 가방을 원장의 창고로 쓴다 (TASK-WM-410) — 「씨앗이 없으면 못 심는다」를 원장 한 곳에서 판정하게.
	//
	// ★ 왜 어댑터인가: 원장은 「무엇이 얼마나 있나」만 물으면 되고, 그게 가방인지 상자인지는 몰라야 한다.
	//   반대로 가방은 원장을 몰라도 된다. 둘을 잇는 얇은 통역이 여기다.
	//
	// ★ ResourceId = 아이템 ID 그대로. 아이템 ID 대역(수천만)은 도시 시뮬(0·1)·삶 자원(100 대역,
	//   KnownResources)과 안 겹친다 — 같은 원장에서 키가 부딪히지 않는다.
	public sealed class InventoryActPool : IActResourcePool
	{
		private readonly Inventory inventory;

		public InventoryActPool(Inventory inventory)
		{
			this.inventory = inventory;
		}

		public int AmountOf(ResourceId resource)
		{
			return inventory == null ? 0 : inventory.CountByID(resource.Value);
		}

		public void Add(ResourceId resource, int amount)
		{
			if (inventory == null || amount == 0)
			{
				return;
			}

			if (amount < 0)
			{
				inventory.Consume(resource.Value, -amount);
				return;
			}

			ItemData item = SOHelper.Get<ItemData>(resource.Value);
			if (item == null)
			{
				Debug.LogError($"[InventoryActPool] 알 수 없는 아이템 ID: {resource.Value} (카탈로그에 없음).");
				return;
			}

			inventory.Add(item, amount);
		}
	}
}

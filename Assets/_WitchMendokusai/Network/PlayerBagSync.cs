using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 세계가 아는 내 가방을 <b>화면에 맞춘다</b> (TASK-WM-218).
	///
	/// 세계가 주인이다 — 다시 들어왔을 때 화면이 비어 있으면 「내 것」이 사라진 것처럼 보인다.
	/// 반대 방향(주웠다·썼다)은 <see cref="WorldBagRelay"/> 가 이미 나른다.
	///
	/// ⚠ 되먹임 고리 주의: 맞추는 동안 인벤토리가 세계에 되알리면 둘이 무한히 오간다.
	/// 그 억제는 <c>Inventory.ApplyWorldCounts</c> 안에 있다.
	/// </summary>
	public sealed class PlayerBagSync : IWorldBagReceiver
	{
		public void ApplyWorldBag(int[] itemIds, int[] amounts)
		{
			SOManager soManager = SOManager.Instance;
			if (soManager == null || soManager.ItemInventory == null)
				return;

			soManager.ItemInventory.ApplyWorldCounts(itemIds, amounts, FindItemData);
		}

		/// <summary>번호로 게임의 아이템 정의를 찾는다 — 세계는 번호만 안다.</summary>
		private static IItemData FindItemData(int itemId)
		{
			SOManager soManager = SOManager.Instance;
			if (soManager == null)
				return null;

			System.Collections.Generic.Dictionary<int, DataSO> byId = soManager[typeof(ItemData)];
			if (byId == null)
				return null;

			return byId.TryGetValue(itemId, out DataSO data) ? data as IItemData : null;
		}
	}
}

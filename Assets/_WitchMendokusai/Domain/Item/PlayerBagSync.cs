using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 세계가 아는 내 가방을 <b>화면에 맞춘다</b> (TASK-WM-218).
	///
	/// ★ 왜 Domain 인가: 이걸 통신 층(WM.Network)에 뒀더니 컴파일이 깨졌다 — 어셈블리 방향이
	///   Network → Domain 이 아니라 그 반대라, 통신 층에서는 SOManager·ItemData 를 볼 수 없다.
	///   (로비 때 겪은 것과 같은 종류다.) 그래서 <b>보이는 쪽</b>이 구멍을 채우고, 통신 층은 부르기만 한다.
	///
	/// 세계가 주인이다 — 다시 들어왔을 때 화면이 비어 있으면 「내 것」이 사라진 것처럼 보인다.
	/// 반대 방향(주웠다·썼다)은 통신 층이 나른다.
	/// </summary>
	public sealed class PlayerBagSync : IWorldBagReceiver
	{
		// 꽂는 자리는 RootLifetimeScope (SOManager 를 등록하는 곳). 전에는 부팅 훅으로 스스로 꽂고 static 으로 SOManager 를 찾던 것
		private readonly SOManager soManager;

		public PlayerBagSync(SOManager soManager)
		{
			this.soManager = soManager;
		}

		public void ApplyWorldBag(int[] itemIds, int[] amounts)
		{
			if (soManager.ItemInventory == null)
				return;

			soManager.ItemInventory.ApplyWorldCounts(itemIds, amounts, FindItemData);
		}

		/// <summary>번호로 게임의 아이템 정의를 찾는다 — 세계는 번호만 안다.</summary>
		private IItemData FindItemData(int itemId)
		{
			Dictionary<int, DataSO> byId = soManager[typeof(ItemData)];
			if (byId == null)
				return null;

			return byId.TryGetValue(itemId, out DataSO data) ? data as IItemData : null;
		}
	}
}

using System;
using UnityEngine;

namespace WitchMendokusai
{
	// 보관 상자 1개의 런타임 보관 동작. 상자 Building 의 model prefab 에 부착.
	// 내용물(ChestInventory)을 부모 BuildingObject.RuntimeData(JSON)에 영속 → GridData 세이브 편승.
	// BuildingObject(범용 풀링 객체)에 상자 로직을 박지 않는다 — Feedback 컴포넌트 패턴 정합.
	[DisallowMultipleComponent]
	public class ChestStorage : MonoBehaviour, IInteractable
	{
		private ChestInventory inventory = new();
		private BuildingObject owner;

		public ChestInventory Inventory => inventory;

		// 내용물 변경 시 발행 — 상자 UI(P1c)가 구독해 갱신.
		public event Action OnContentsChanged = delegate { };

		// 플레이어 상호작용(클릭) 시 발행 — 상자 UI(P1c)가 구독해 보관창 오픈.
		public event Action<ChestStorage> OnOpenRequested = delegate { };

		private void OnEnable()
		{
			// Model 은 BuildingObject 의 자식으로 spawn → 부모 존재 보장. // init-order-ok
			owner = GetComponentInParent<BuildingObject>();
			inventory = ChestInventory.FromJson(owner.SaveData.RuntimeData);
			OnContentsChanged();
		}

		public void Add(int itemID, int count = 1)
		{
			inventory.Add(itemID, count);
			Persist();
		}

		public bool Remove(int itemID, int count = 1)
		{
			bool removed = inventory.Remove(itemID, count);
			if (removed)
				Persist();
			return removed;
		}

		public void OnInteract() => OnOpenRequested(this);

		private void Persist()
		{
			owner.UpdateRuntimeData(inventory.ToJson());
			OnContentsChanged();
		}
	}
}

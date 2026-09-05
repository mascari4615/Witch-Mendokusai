using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace WitchMendokusai
{
	// 보관 상자 1개의 런타임 보관 동작. 상자 Building 의 model prefab 에 부착.
	// per-instance ChestStorageInventory(게임 Inventory 재사용) 보유 → 보관 UI 가 그대로 바인딩.
	// 영속: Inventory.Save()(List<InventorySlotSaveData>) → JSON → 부모 BuildingObject.RuntimeData → GridData 세이브.
	// BuildingObject(범용 풀링)에 상자 로직 안 박음 (Feedback 컴포넌트 패턴, TASK-WM-169).
	[DisallowMultipleComponent]
	public class ChestStorage : MonoBehaviour, IInteractable
	{
		private ChestStorageInventory inventory;
		private BuildingObject owner;

		// 보관 UI(P1c) 가 바인딩. ChestStorageInventory 는 Inventory 라 기존 위젯(ItemGrid/UIItemGrid) 그대로 사용.
		public Inventory Inventory => inventory;

		// 상자 클릭 시 발행 (전역) — 단일 공유 보관 UI(ChestStorageView)가 구독해 그 상자를 오픈.
		public static event Action<ChestStorage> AnyOpenRequested = delegate { };

		private void OnEnable()
		{
			// Model 은 BuildingObject 의 자식으로 spawn → 부모 존재 보장. // init-order-ok
			owner = GetComponentInParent<BuildingObject>();
			inventory = ScriptableObject.CreateInstance<ChestStorageInventory>();
			LoadFromRuntimeData(owner.SaveData.RuntimeData);
		}

		private void OnDisable()
		{
			if (inventory != null)
			{
				Destroy(inventory);
				inventory = null;
			}
		}

		// 보관 내용 변경 후 호출 — 현재 인벤토리를 RuntimeData 에 직렬화해 세이브에 반영.
		public void Persist()
		{
			owner.UpdateRuntimeData(JsonConvert.SerializeObject(inventory.Save()));
		}

		public void OnInteract() => AnyOpenRequested(this);

		private void LoadFromRuntimeData(string json)
		{
			List<InventorySlotSaveData> slots = new();

			if (string.IsNullOrEmpty(json) == false)
			{
				try
				{
					slots = JsonConvert.DeserializeObject<List<InventorySlotSaveData>>(json) ?? new List<InventorySlotSaveData>();
				}
				catch (JsonException)
				{
					// 손상 세이브 — 상자 1개 깨짐이 GridData 전체 로드를 안 죽이게 빈 인벤토리 폴백.
					Debug.LogWarning($"[ChestStorage] RuntimeData 파싱 실패 → 빈 상자: {json}");
					slots = new List<InventorySlotSaveData>();
				}
			}

			inventory.Load(slots);
		}
	}
}

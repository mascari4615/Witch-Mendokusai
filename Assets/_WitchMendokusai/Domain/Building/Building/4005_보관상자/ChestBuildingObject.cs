using System;
using UnityEngine;

namespace WitchMendokusai
{
	// TASK-WM-169 Phase 1 — 상자 prefab 의 Model 에 붙는 컴포넌트. ChestInventory POCO 를 보유하고
	// BuildingObject.SaveData.RuntimeData(string JSON) 슬롯에 직렬화/복원. FarmFieldObject 가 작물
	// 상태로 동일 RuntimeData bridge 를 쓰는 검증된 패턴(prior art).
	//
	// IInteractable.OnInteract — Phase 1 INC-A 는 정적 이벤트만 발화하고 실 UI 패널은 INC-B
	// 후속(에디터 prefab + UIManager 배선 = 사용자 영역). 게임 코드는 OnChestOpened 구독으로
	// 자유롭게 UI 를 연결할 수 있다. EditMode 테스트도 이 이벤트로 OnInteract 호출을 관측 가능.
	public class ChestBuildingObject : MonoBehaviour, IInteractable
	{
		public static event Action<ChestBuildingObject> OnChestOpened = delegate { };

		[Header("_" + nameof(ChestBuildingObject))]
		[SerializeField] private int defaultCapacity = 16;

		public ChestInventory Chest { get; private set; }

		private BuildingObject buildingObject;
		private bool persistOnChange = false;

		private void OnEnable()
		{
			buildingObject = GetComponentInParent<BuildingObject>();
			LoadChestFromRuntimeData();
		}

		private void OnDisable()
		{
			if (Chest != null)
				Chest.OnDataChanged -= PersistToRuntimeData;
			Chest = null;
			persistOnChange = false;
		}

		public void OnInteract()
		{
			OnChestOpened.Invoke(this);
		}

		private void LoadChestFromRuntimeData()
		{
			string json = buildingObject != null ? buildingObject.SaveData.RuntimeData : string.Empty;
			ChestSaveData saveData = ChestSaveData.FromJson(json);
			int capacity = saveData.Capacity > 0 ? saveData.Capacity : defaultCapacity;

			persistOnChange = false;
			Chest = new ChestInventory(capacity);
			saveData.ApplyTo(Chest);
			persistOnChange = true;
			Chest.OnDataChanged += PersistToRuntimeData;
		}

		private void PersistToRuntimeData()
		{
			if (persistOnChange == false || buildingObject == null)
				return;
			buildingObject.UpdateRuntimeData(ChestSaveData.FromChest(Chest).ToJson());
		}
	}
}

namespace WitchMendokusai
{
	// 보관 상자 1개의 per-instance 인벤토리. 플레이어 Inventory 를 그대로 재사용
	// (슬롯/스택/용량/Save·Load/UI 위젯 바인딩) 하되, 'OnItemAdded' 전역 side-effect
	// (마지막 장착 아이템 갱신)는 상자에 무의미 → no-op override (TASK-WM-169 P1c).
	// 런타임 CreateInstance per chest (에셋 아님). 영속 = Save() → JSON → BuildingInstanceData.RuntimeData.
	public class ChestStorageInventory : Inventory
	{
		protected override void OnItemAdded(ItemData itemData)
		{
			// 상자 입고는 '마지막 장착' 과 무관 — 의도적 no-op.
		}
	}
}

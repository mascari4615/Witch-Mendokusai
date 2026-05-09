using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// Doll.Equipment 리스트를 Inventory 인터페이스로 노출하는 어댑터.
	/// HoldingManager / ItemGrid / ItemSlot 가 일반 인벤토리처럼 다루도록 한다.
	/// 데이터 동일성 — Data 가 doll.Equipment 와 같은 List 인스턴스를 가리키므로 슬롯 변경이 즉시 인형에 반영된다.
	/// </summary>
	public class DollEquipmentInventory : Inventory
	{
		protected override int DefaultCapacity => Doll.EQUIPMENT_SLOT_COUNT;

		public Doll BoundDoll { get; private set; }

		public void BindDoll(Doll doll)
		{
			BoundDoll = doll;
			Capacity = Doll.EQUIPMENT_SLOT_COUNT;
			Data = doll != null ? doll.Equipment : new List<Item>();
			UpdateUI();
		}
	}
}

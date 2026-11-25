using System.Linq;

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
			if (HoldingManager.TryGetExistingInstance(out HoldingManager holding))
				holding.ReturnHeldFrom(this);
			BoundDoll = doll;
			Capacity = Doll.EQUIPMENT_SLOT_COUNT;
			// 인형이 없어도 목록 길이는 칸 수와 같게. 짧으면 칸을 그리는 쪽이 범위를 벗어나 읽음
			Data = doll != null ? doll.Equipment : Enumerable.Repeat<Item>(null, Capacity).ToList();
			UpdateUI();
		}

		// 장비만, 같은 장비는 한 칸만
		public override bool CanAccept(int slotIndex, Item item)
		{
			if (item.Data is EquipmentData == false)
				return false;

			for (int i = 0; i < Data.Count; i++)
			{
				if (i != slotIndex && Data[i] != null && Data[i].Data.ID == item.Data.ID)
					return false;
			}
			return true;
		}
	}
}

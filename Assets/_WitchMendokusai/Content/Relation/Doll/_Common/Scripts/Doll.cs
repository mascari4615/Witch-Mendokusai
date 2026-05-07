using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = nameof(Doll), menuName = "WM/Variable/" + nameof(Unit) +"/"+ nameof(Doll))]
	public class Doll : Unit, ISavable<DollSaveData>
	{
		public const int DUMMY_ID = 4444;

		[field: Header("_" + nameof(Doll))]
		// 고유 장비 : 인형이 고정적으로 장착하고 있는 장비
		[field: SerializeField] public EquipmentData SignatureEquipment { get; private set; }
		// 기본 장비 : 인형을 얻을 때 기본적으로 장착되어 있는 장비들
		[field: SerializeField] public List<EquipmentData> DefaultEquipments { get; private set; }

		public const int EQUIPMENT_SLOT_COUNT = 3;

		// 인형의 레벨과 경험치 (던전 내 일시적 레벨과 경험치와는 별개)
		[field: NonSerialized] public int Level { get; private set; } = 1;
		[field: NonSerialized] public int Exp { get; private set; } = 0;
		// 현재 장착 장비 (인형이 직접 소유). 시그니처 장비는 별개 (SignatureEquipment 필드).
		[field: NonSerialized] public List<Item> Equipment { get; private set; } = new() { null, null, null };

		public void Load(DollSaveData dollData)
		{
			Level = dollData.Level;
			Exp = dollData.Exp;

			Equipment = new List<Item> { null, null, null };
			if (dollData.Equipment != null)
			{
				foreach (DollEquipmentSlotSaveData slot in dollData.Equipment)
				{
					if (slot.SlotIndex < 0 || slot.SlotIndex >= EQUIPMENT_SLOT_COUNT)
					{
						Debug.LogWarning($"Doll {ID} 장비 슬롯 인덱스 {slot.SlotIndex} 범위 초과");
						continue;
					}
					ItemData itemData = SOHelper.GetItemData(slot.ItemID);
					if (itemData == null)
						continue;
					Equipment[slot.SlotIndex] = new Item(slot.Guid, itemData, 1);
				}
			}
		}

		public DollSaveData Save()
		{
			List<DollEquipmentSlotSaveData> equipmentSave = new();
			for (int i = 0; i < Equipment.Count; i++)
			{
				if (Equipment[i] == null)
					continue;
				equipmentSave.Add(new DollEquipmentSlotSaveData(i, Equipment[i]));
			}
			return new DollSaveData
			{
				DollID = ID,
				Level = Level,
				Exp = Exp,
				Equipment = equipmentSave
			};
		}
	}
}
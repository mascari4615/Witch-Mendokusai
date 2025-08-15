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

		// 인형의 레벨과 경험치 (던전 내 일시적 레벨과 경험치와는 별개)
		[field: NonSerialized] public int Level { get; private set; } = 1;
		[field: NonSerialized] public int Exp { get; private set; } = 0;
		// 현재 장착하고 있는 장비들의 Guid
		[field: NonSerialized] public List<Guid?> EquipmentGuids { get; private set; } = new() { null, null, null };

		public void Load(DollSaveData dollData)
		{
			Level = dollData.Level;
			Exp = dollData.Exp;
			EquipmentGuids = dollData.EquipmentGuids.ToList();

			if (EquipmentGuids.Count < 3)
				EquipmentGuids.AddRange(new Guid?[3 - EquipmentGuids.Count]);
			else if (EquipmentGuids.Count > 3)
				Debug.LogWarning("인형의 장비 개수가 3개를 초과했습니다.");
		}

		public DollSaveData Save()
		{
			return new DollSaveData
			{
				DollID = ID,
				Level = Level,
				Exp = Exp,
				EquipmentGuids = EquipmentGuids.ToList()
			};
		}
	}
}
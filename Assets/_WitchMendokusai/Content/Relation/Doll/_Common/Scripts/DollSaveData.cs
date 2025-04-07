using System;
using System.Collections.Generic;
using System.Linq;

namespace WitchMendokusai
{
	[Serializable]
	public struct DollSaveData
	{
		public int DollID;
		public int Level;
		public int Exp;
		public List<Guid?> EquipmentGuids;

		public DollSaveData(int dollID, int dollLevel, int dollExp, List<Guid?> equipmentGuids)
		{
			DollID = dollID;
			Level = dollLevel;
			Exp = dollExp;
			EquipmentGuids = equipmentGuids.ToList();
		}
	}
}
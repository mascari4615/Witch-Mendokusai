using System.Collections.Generic;

namespace WitchMendokusai
{
	public class ExpeditionManager
	{
		private RuntimeExpedition active;

		public RuntimeExpedition Active => active;
		public bool HasActive => active != null;

		public void Init(List<ExpeditionSaveData> saveData)
		{
			active = null;
			if (saveData != null && saveData.Count > 0)
				active = new RuntimeExpedition(saveData[0]);
		}

		public void StartExpedition(ExpeditionSO data)
		{
			active = new RuntimeExpedition(data);
			DataManager.Instance.SaveManager.SaveData();
		}

		public bool TryComplete(out List<DataSOWithPercentage> loot)
		{
			loot = null;
			if (active == null || !active.IsComplete)
				return false;

			loot = active.Data.Loot;
			active = null;
			DataManager.Instance.SaveManager.SaveData();
			return true;
		}

		public List<ExpeditionSaveData> Save()
		{
			List<ExpeditionSaveData> result = new();
			if (active != null)
				result.Add(active.Save());
			return result;
		}
	}
}

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	public class UIBuildingBar : UIPanel
	{
		[field: Header("_" + nameof(UIBuildingBar))]
		private UIBuildingSlot[] buildingSlots;

		private void Start()
		{
			TimeManager.Instance.RegisterCallback(UpdateUI);
		}

		protected override void OnInit()
		{
			buildingSlots = GetComponentsInChildren<UIBuildingSlot>(true);

			foreach (UIBuildingSlot buildingSlot in buildingSlots)
			{
				buildingSlot.Init();
				buildingSlot.SetClickAction((slot) =>
				{
					Building building = slot.DataSO as Building;
					BuildManager.Instance.SelectBuilding(building);
				});
			}
		}

		public override void UpdateUI()
		{
			List<DataSO> buildings = SOManager.Instance.DataSOs[typeof(Building)].Values.ToList();

			for (int i = 0; i < buildings.Count; i++)
			{
				Building building = buildings[i] as Building;
				buildingSlots[i].SetSlot(building);
			}

			for (int i = 0; i < buildingSlots.Length; i++)
				buildingSlots[i].gameObject.SetActive(i < buildings.Count);
		}
	}
}
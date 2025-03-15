using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	public class UIBuildingBar : MonoBehaviour, IUI
	{
		[field: Header("_" + nameof(UIBuildingBar))]
		private UIBuildingSlot[] buildingSlots;

		private void Start()
		{
			TimeManager.Instance.RegisterCallback(UpdateUI);
		}

		public void Init(BuildManager buildManager)
		{
			buildingSlots = GetComponentsInChildren<UIBuildingSlot>(true);

			foreach (UIBuildingSlot buildingSlot in buildingSlots)
			{
				buildingSlot.Init();
				buildingSlot.SetSelectAction((slot) =>
				{
					BuildingData building = slot.DataSO as BuildingData;
					buildManager.SelectBuilding(building);
				});
			}
		}

		public void UpdateUI()
		{
			var buildings = SOManager.Instance.DataSOs[typeof(BuildingData)].Values.ToList();

			for (int i = 0; i < buildings.Count; i++)
			{
				BuildingData building = buildings[i] as BuildingData;
				buildingSlots[i].SetSlot(building);
			}

			for (int i = 0; i < buildingSlots.Length; i++)
				buildingSlots[i].gameObject.SetActive(i < buildings.Count);
		}
	}
}
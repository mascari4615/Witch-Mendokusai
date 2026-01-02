using UnityEngine;

namespace WitchMendokusai
{
	public class UIUpgradeSlot : UISlot
	{
		[SerializeField] private Transform checkBoxesParent;
		private GameObject[] checkBoxes = new GameObject[0];
		private GameObject[] checks = new GameObject[0];

		public override void Init()
		{
			base.Init();

			checkBoxes = new GameObject[checkBoxesParent.childCount];
			checks = new GameObject[checkBoxesParent.childCount];

			for (int i = 0; i < checkBoxesParent.childCount; i++)
			{
				checkBoxes[i] = checkBoxesParent.GetChild(i).gameObject;
				checks[i] = checkBoxes[i].transform.GetChild(1).gameObject; // 0: Background, 1: Check
			}
		}

		public override void UpdateUI()
		{
			base.UpdateUI();

			if (DataSO is UpgradeData upgradeData)
			{
				for (int i = 0; i < checkBoxes.Length; i++)
				{
					if (i < upgradeData.MaxLevel)
					{
						checkBoxes[i].SetActive(true);
						checks[i].SetActive(i < upgradeData.CurLevel);
					}
					else
					{
						checkBoxes[i].SetActive(false);
					}
				}
			}
		}
	}
}
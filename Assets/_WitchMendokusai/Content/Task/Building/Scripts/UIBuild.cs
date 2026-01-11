using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	public enum UIBuildingType
	{
		None = -1,
		BuildingBar = 0,
	}

	public class UIBuild : UIContentBase<UIBuildingType>
	{
		[field: Header("_" + nameof(UIBuild))]
		[SerializeField] private UIBuildingBar buildingBar;
		private Coroutine loop;

		public override bool CanBeClosedByCancelInput => true;
		public override UIBuildingType DefaultPanel => UIBuildingType.None;

		public override void Init()
		{
			Panels[UIBuildingType.BuildingBar] = buildingBar;
		}

		public void UpdateUI()
		{
			buildingBar.UpdateUI();
		}

		public void StartLoop()
		{
			if (loop != null)
				StopCoroutine(loop);
			loop = StartCoroutine(Loop());
		}

		public void StopLoop()
		{
			if (loop != null)
				StopCoroutine(loop);
		}

		private IEnumerator Loop()
		{
			WaitForSeconds wait = new(.05f);

			while (true)
			{
				UpdateUI();
				yield return wait;
			}
		}
	}
}
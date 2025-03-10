using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	public class UIBuild : UIPanel
	{
		[field: Header("_" + nameof(UIBuild))]
		[SerializeField] private UIBuildingBar buildingBar;
		private Coroutine loop;

		public override void Init()
		{
			buildingBar.Init();
		}

		public override void UpdateUI()
		{
			buildingBar.UpdateUI();
		}

		public override void OnOpen()
		{
			if (loop != null)
				StopCoroutine(loop);
			loop = StartCoroutine(Loop());
		}

		public override void OnClose()
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
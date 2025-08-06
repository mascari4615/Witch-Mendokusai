using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	public class UIAdventurerBoard : UIPanel
	{
		[field: Header("_" + nameof(UIAdventurerBoard))]
		[SerializeField] private UISlot reputationSlot;
		private Coroutine loop;

		protected override void OnInit()
		{
			reputationSlot.Init();
			reputationSlot.SetSlot(GetGameStatData(GameStatType.VILLAGE_REPUTATION_LEVEL));
		}

		public override void UpdateUI()
		{
		}

		protected override void OnOpen()
		{
			if (loop != null)
				StopCoroutine(loop);
			loop = StartCoroutine(Loop());
		}

		protected override void OnClose()
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
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	public class UIAdventurerGuild : UIPanel
	{
		[field: Header("_" + nameof(UIAdventurerGuild))]
		private Coroutine loop;

		public override void Init()
		{
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
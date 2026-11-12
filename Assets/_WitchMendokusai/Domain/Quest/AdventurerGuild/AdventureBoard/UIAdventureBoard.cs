using System.Collections;
using UnityEngine;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	public class UIAdventurerBoard : UIPanel
	{
		[field: Header("_" + nameof(UIAdventurerBoard))]
		[SerializeField] private UISlot reputationSlot;
		private Coroutine loop;

		public override bool IsFullscreen => true;

		protected override void OnInit()
		{
			reputationSlot.Init();
			GameStatData reputationData = GetGameStatData(GameStatType.VILLAGE_REPUTATION_LEVEL);
			reputationSlot.SetSlot(reputationData.Sprite, StatDisplayName.Get(GameStatType.VILLAGE_REPUTATION_LEVEL), reputationData.Description);
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
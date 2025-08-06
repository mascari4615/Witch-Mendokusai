using UnityEngine;

namespace WitchMendokusai
{
	[RequireComponent(typeof(UICraft))]
	public class UICraftPanel : UIPanel
	{
		private UICraft craft;

		protected override void OnInit()
		{
			craft = GetComponent<UICraft>();
			craft.Init();
		}

		public override void UpdateUI()
		{
			craft.UpdateUI();
		}

		protected override void OnOpen()
		{
			base.OnOpen();
			craft.SetActive(true);
		}

		protected override void OnClose()
		{
			base.OnClose();
			craft.SetActive(false);
		}
	}
}
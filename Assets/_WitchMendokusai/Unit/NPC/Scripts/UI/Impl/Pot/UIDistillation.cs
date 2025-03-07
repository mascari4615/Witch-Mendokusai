using UnityEngine;

namespace WitchMendokusai
{
	[RequireComponent(typeof(UICraft))]
	public class UIDistillation : UIPanel
	{
		private UICraft craft;

		public override void Init()
		{
			craft = GetComponent<UICraft>();
			craft.Init();
		}

		public override void UpdateUI()
		{
			craft.UpdateUI();
		}
	}
}
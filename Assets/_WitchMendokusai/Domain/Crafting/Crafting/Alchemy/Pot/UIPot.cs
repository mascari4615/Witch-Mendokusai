using System;

namespace WitchMendokusai
{
	public class UIPot : UIPanel
	{
		public override bool IsFullscreen => true;

		protected override void OnInit()
		{
		}

		public override void SetNPC(NPCObject npc)
		{
		}

		public override void UpdateUI()
		{
			throw new NotImplementedException();
		}
	}
}
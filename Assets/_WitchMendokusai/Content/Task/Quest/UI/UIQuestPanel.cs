namespace WitchMendokusai
{
	public class UIQuestPanel : UIPanel
	{
		private UIQuestGrid questGrid;

		public override bool IsFullscreen => true;

		protected override void OnInit()
		{
			questGrid = GetComponentInChildren<UIQuestGrid>(true);
			questGrid.Init();
		}

		public override void UpdateUI()
		{
			questGrid.UpdateUI();
		}
	}
}

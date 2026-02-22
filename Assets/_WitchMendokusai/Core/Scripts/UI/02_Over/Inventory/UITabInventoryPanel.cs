namespace WitchMendokusai
{
	public class UITabInventoryPanel : UIPanel
	{
		private UIItemGrid itemInventoryUI;

		public override bool IsFullscreen => true;

		protected override void OnInit()
		{
			itemInventoryUI = GetComponentInChildren<UIItemGrid>(true);
			itemInventoryUI.Init();
		}

		public override void UpdateUI()
		{
			itemInventoryUI.UpdateUI();
		}
	}
}

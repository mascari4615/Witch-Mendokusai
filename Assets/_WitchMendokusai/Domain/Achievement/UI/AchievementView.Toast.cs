namespace WitchMendokusai
{
	// 업적 달성 띠. 화면 아래 가운데, 하나씩 차례로 (UIRoot.Toasts). 조작을 막지 않음
	public partial class AchievementView
	{
		private AchievementToast toast;

		// 띠는 업적 화면 밖 (OverlayLayer) 이라 USS 를 따로 붙임
		private void BuildToast()
		{
			toast = new AchievementToast();
			toast.AddToClassList(USS_PALETTE);
			toast.styleSheets.Add(Settings.Menu.Style);
			toast.styleSheets.Add(Settings.Style);
			uiRoot.OverlayLayer.Add(toast);
		}

		private void DestroyToast() => toast.RemoveFromHierarchy();

		private void OnUnlocked(AchievementUnlockedEvent evt) =>
			uiRoot.Toasts.Enqueue(toast, () => toast.Show(SOHelper.Get<AchievementSO>(evt.AchievementId), evt.Tier), toast.Hide);
	}
}

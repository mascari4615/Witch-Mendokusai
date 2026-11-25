namespace WitchMendokusai
{
	// 판 안에서 새 기록이 적힌 순간의 띠 (UIRoot.Toasts). 첫 기록과 완성만 띄움 (중간 칸은 도감의 NEW 로)
	public partial class DiscoveryScreen
	{
		private const string USS_PALETTE = "wm-fullmenu-palette";
		private const int FIRST_STAGE = 1;

		private DiscoveryToast toast;

		// 띠는 도감 화면 밖 (OverlayLayer) 이라 USS 를 따로 붙임
		private void BuildToast()
		{
			toast = new DiscoveryToast();
			AchievementElements.AddClasses(toast, USS_PALETTE + " " + USS_CARD_TOKENS);
			AddStyles(toast);
			uiRoot.OverlayLayer.Add(toast);
		}

		private void DestroyToast() => toast.RemoveFromHierarchy();

		private void OnStageRaised(MonsterLogStageEvent evt)
		{
			// 도감을 열 길이 없는 빌드 (데모) 에서는 기록만 쌓고 알리지 않음
			if (KeybindRegistry.IsEnabledInThisBuild(InputEventType.DiscoveryToggle) == false)
			{
				return;
			}
			if (evt.Stage == FIRST_STAGE || evt.Stage >= evt.MaxStage)
			{
				uiRoot.Toasts.Enqueue(toast, () => toast.Show(evt), toast.Hide);
			}
		}
	}
}

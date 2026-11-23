using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 세이브를 앞 판으로 복구했거나, 못 읽어 새 게임으로 시작했거나, 다른 창이 세이브를 쥐고 있을 때 한 번 알리는 확인창.
	/// 로비가 데이터를 불러온 뒤 SaveManager.LastLoadOutcome 을 넘겨 부름. 첫 실행과 정상 불러오기는 알리지 않음
	/// 모양은 공용 확인창 (UIConfirmDialog, ConfirmDialog.uss)
	/// </summary>
	public class SaveNoticeView : MonoBehaviour
	{
		private UIRoot uiRoot;
		private VisualElement root;
		private UIConfirmDialog dialog;

		public bool IsOpen => dialog != null && dialog.IsOpen;

		private void Awake() => uiRoot = GetComponent<UIRoot>();

		private void OnDestroy() => root?.RemoveFromHierarchy();

		public void ShowIfNeeded(SaveLoadOutcome outcome)
		{
			switch (outcome)
			{
				case SaveLoadOutcome.RestoredFromBackup:
					Show("세이브를 앞 판으로 복구했어요", "저장 파일을 읽지 못해 그 전 저장으로 불러왔습니다. 최근 진행이 조금 빠졌을 수 있어요.");
					break;
				case SaveLoadOutcome.ResetUnreadable:
					Show("세이브를 읽지 못해 새 게임으로 시작해요", "읽지 못한 파일은 지우지 않고 세이브 폴더에 따로 옮겼습니다.");
					break;
				case SaveLoadOutcome.ReadOnlyOtherOwner:
					Show("이번 실행은 저장되지 않아요", "다른 창에서 같은 세이브로 게임이 켜져 있습니다. 진행을 남기려면 이 창을 끄고 그 창에서 이어 주세요.");
					break;
			}
		}

		/// <summary> 취소 키. 선택지가 확인 하나라 고른 것과 같음 </summary>
		public void Cancel() => dialog?.Cancel();

		private void Show(string title, string text)
		{
			EnsureBuilt();
			root.style.display = DisplayStyle.Flex;
			UiSfx.Play(UiSfxEvent.WindowOpen);
			dialog.Open(title, text, new UIConfirmDialog.Choice("확인", Hide, true));
		}

		private void Hide()
		{
			UiSfx.Play(UiSfxEvent.Confirm);
			// 숨은 버튼에 초점이 남으면 확인 키가 로비 버튼까지 안 감 (SettingView.Close 와 같음)
			if (root.focusController?.focusedElement is VisualElement focused && root.Contains(focused))
				focused.Blur();
			root.style.display = DisplayStyle.None;
		}

		private void EnsureBuilt()
		{
			if (root != null)
				return;
			FullScreenFrame frame = uiRoot.CreateFullScreenFrame(uiRoot.ScreenLayer, nameof(SaveNoticeView));
			root = frame.Root;
			// 확인창의 어두운 막이 화면 끝까지 덮게 Root 에 둠. 상자는 가운데라 안전 영역 안
			dialog = new UIConfirmDialog();
			root.Add(dialog);
			root.style.display = DisplayStyle.None;
		}
	}
}

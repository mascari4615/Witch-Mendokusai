using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 전체 메뉴의 게임 종료, 끼임 탈출, 던전 포기, 로비로가 묻는 확인 팝업. 메뉴 위에 따로 뜸
	/// 전에는 설정창의 확인 자리를 빌려 설정창이 통째로 같이 열렸음 (2026-11-23 릴리스 실측, 사용자 "완전 별개의 팝업이어야")
	/// 취소하면 팝업만 닫혀 메뉴가 그대로, 수락하면 부른 쪽의 동작
	/// 모양은 공용 확인창 (UIConfirmDialog, ConfirmDialog.uss). 처음 초점은 취소
	/// </summary>
	public class MenuConfirmView : MonoBehaviour
	{
		private UIRoot uiRoot;
		private VisualElement root;
		private UIConfirmDialog dialog;
		// 취소하면 초점을 부른 메뉴 칸으로 돌려줌
		private VisualElement focusOnCancel;

		public bool IsOpen => dialog != null && dialog.IsOpen;

		private void Awake() => uiRoot = GetComponent<UIRoot>();

		private void OnDestroy() => root?.RemoveFromHierarchy();

		public void Ask(string title, string text, string acceptLabel, Action onAccept)
		{
			EnsureBuilt();
			focusOnCancel = root.focusController?.focusedElement as VisualElement;
			root.style.display = DisplayStyle.Flex;
			// 처음 만들 때 화면 맨 위에 붙지만, 그 뒤 만든 전체 화면 창보다 위에 오게 매번 올림
			root.BringToFront();
			UiSfx.Play(UiSfxEvent.Confirm);
			dialog.Open(title, text,
				new UIConfirmDialog.Choice(acceptLabel, () => Accept(onAccept)),
				new UIConfirmDialog.Choice("취소", CancelChosen, true));
		}

		/// <summary> 취소 키. 마지막 선택지 (취소) 와 같음 </summary>
		public void Cancel() => dialog?.Cancel();

		/// <summary>
		/// 고르지 않고 닫기, 콜백 없음. 아래 메뉴가 다른 길 (창 단축키 등) 로 닫힐 때 메뉴가 부름
		/// 안 닫으면 팝업이 게임 위에 남고, 수락이 메뉴 없이 종료나 포기를 함
		/// </summary>
		public void Dismiss()
		{
			if (IsOpen == false)
				return;
			dialog.Dismiss();
			Hide();
			focusOnCancel = null;
		}

		private void Accept(Action onAccept)
		{
			UiSfx.Play(UiSfxEvent.Confirm);
			Hide();
			focusOnCancel = null;
			onAccept();
		}

		private void CancelChosen()
		{
			UiSfx.Play(UiSfxEvent.Cancel);
			Hide();
			VisualElement target = focusOnCancel;
			focusOnCancel = null;
			if (target != null)
				target.schedule.Execute(() => target.Focus());
		}

		private void Hide()
		{
			// 숨은 버튼에 초점이 남으면 확인 키가 아래 메뉴까지 안 감 (SaveNoticeView.Hide 와 같음)
			if (root.focusController?.focusedElement is VisualElement focused && root.Contains(focused))
				focused.Blur();
			root.style.display = DisplayStyle.None;
		}

		private void EnsureBuilt()
		{
			if (root != null)
				return;
			FullScreenFrame frame = uiRoot.CreateFullScreenFrame(uiRoot.ScreenLayer, nameof(MenuConfirmView));
			root = frame.Root;
			// 어두운 막이 화면 끝까지 덮게 Root 에 둠. 상자는 가운데라 안전 영역 안
			dialog = new UIConfirmDialog();
			root.Add(dialog);
			root.style.display = DisplayStyle.None;
		}
	}
}

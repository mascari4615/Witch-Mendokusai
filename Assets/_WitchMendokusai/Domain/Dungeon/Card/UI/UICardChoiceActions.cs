using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	// 레벨업 화면의 리롤, 스킵, 차단 버튼과 잔여 횟수. 덱 화면은 스킵만 사용
	public class UICardChoiceActions : MonoBehaviour
	{
		[SerializeField] private SurvivalUITheme theme;
		[SerializeField] private Button rerollButton;
		[SerializeField] private Button skipButton;
		[SerializeField] private Button banButton;
		[SerializeField] private TextMeshProUGUI rerollLabel;
		[SerializeField] private TextMeshProUGUI skipLabel;
		[SerializeField] private TextMeshProUGUI banLabel;
		[SerializeField] private TextMeshProUGUI title;
		[SerializeField] private string rerollFormat = "리롤 {0}";
		[SerializeField] private string skipFormat = "스킵 {0}";
		[SerializeField] private string banFormat = "차단 {0}";
		[SerializeField] private string banCancelText = "차단 취소";
		[SerializeField] private string banTitle = "차단할 카드를 고르세요";
		[SerializeField] private Color enabledText = Color.white;
		[SerializeField] private Color disabledText = Color.gray;

		private CardManager cardManager;
		private Selectable upTarget;
		private string normalTitle;

		public bool IsBanMode { get; private set; }

		public void Init(CardManager manager)
		{
			cardManager = manager;
			skipButton.onClick.AddListener(OnSkip);
			UITermIcon.Attach(skipLabel, theme, SurvivalTerm.Skip);
			if (rerollButton != null)
			{
				rerollButton.onClick.AddListener(OnReroll);
				banButton.onClick.AddListener(OnBanToggle);
				normalTitle = title.text;
				UITermIcon.Attach(rerollLabel, theme, SurvivalTerm.Reroll);
				UITermIcon.Attach(banLabel, theme, SurvivalTerm.Ban);
			}
		}

		public Selectable FirstEnabled
		{
			get
			{
				List<Selectable> buttons = EnabledButtons();
				return buttons.Count > 0 ? buttons[0] : null;
			}
		}

		public void SetUpTarget(Selectable target)
		{
			upTarget = target;
			ApplyNavigation();
		}

		public void Refresh()
		{
			Apply(skipButton, skipLabel, string.Format(skipFormat, cardManager.SkipsLeft), cardManager.CanSkip);
			if (rerollButton != null)
			{
				Apply(rerollButton, rerollLabel, string.Format(rerollFormat, cardManager.RerollsLeft), cardManager.CanReroll);
				Apply(banButton, banLabel, IsBanMode ? banCancelText : string.Format(banFormat, cardManager.BansLeft), cardManager.BansLeft > 0);
			}
			ApplyNavigation();
		}

		// 전체 화면 패널에서는 PopText가 지워져서 제목 자리에 표시
		public void ShowBanDenied(string text)
		{
			if (string.IsNullOrEmpty(text) == false)
				title.text = text;
		}

		public void ExitBanMode()
		{
			if (IsBanMode == false)
				return;
			IsBanMode = false;
			title.text = normalTitle;
		}

		private void OnDisable() => ExitBanMode();

		private void OnSkip()
		{
			if (cardManager.SkipLevelUp())
				UiSfx.Play(UiSfxEvent.Cancel);
		}

		private void OnReroll()
		{
			ExitBanMode();
			if (cardManager.RerollCards())
				UiSfx.Play(UiSfxEvent.Reroll);
			if (cardManager.CanReroll)
				rerollButton.Select();
		}

		private void OnBanToggle()
		{
			IsBanMode = IsBanMode == false;
			UiSfx.Play(IsBanMode ? UiSfxEvent.Confirm : UiSfxEvent.Cancel);
			title.text = IsBanMode ? banTitle : normalTitle;
			Refresh();
			if (IsBanMode && upTarget != null)
				upTarget.Select();
		}

		private static void Apply(Button button, TextMeshProUGUI label, string text, bool isEnabled)
		{
			button.interactable = isEnabled;
			label.text = text;
		}

		private void LateUpdate()
		{
			skipLabel.color = skipButton.IsInteractable() ? enabledText : disabledText;
			if (rerollButton != null)
			{
				rerollLabel.color = rerollButton.IsInteractable() ? enabledText : disabledText;
				banLabel.color = banButton.IsInteractable() ? enabledText : disabledText;
			}
		}

		private List<Selectable> EnabledButtons()
		{
			List<Selectable> buttons = new();
			if (rerollButton != null && rerollButton.interactable)
				buttons.Add(rerollButton);
			if (skipButton.interactable)
				buttons.Add(skipButton);
			if (banButton != null && banButton.interactable)
				buttons.Add(banButton);
			return buttons;
		}

		private void ApplyNavigation()
		{
			List<Selectable> buttons = EnabledButtons();
			for (int i = 0; i < buttons.Count; i++)
			{
				buttons[i].navigation = new Navigation
				{
					mode = Navigation.Mode.Explicit,
					selectOnUp = upTarget,
					selectOnLeft = i > 0 ? buttons[i - 1] : null,
					selectOnRight = i < buttons.Count - 1 ? buttons[i + 1] : null
				};
			}
		}
	}
}

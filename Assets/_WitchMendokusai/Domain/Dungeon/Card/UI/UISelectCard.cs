using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WitchMendokusai
{
	public class UISelectCard : UIPanel
	{
		[SerializeField] private Transform deckPanelContent;
		[SerializeField] private List<UICardSlot> cardSelectButtons;
		// 패널이 튀어나오는 시간.
		[SerializeField] private float openAnimDuration = 0.5f;
		[SerializeField] private UIDeckTabs deckTabs;
		[SerializeField] private UICardChoiceActions actions;
		[SerializeField] private UIEvolutionStrip evolutionStrip;
		[SerializeField] private UIChestReward chestReward;

		private UICardFocusSound focusSound;
		private UICardRevealDirector reveal;
		private readonly List<CardRevealEntry> revealEntries = new();

		public override bool IsFullscreen => true;
		public bool IsChestRevealing => chestReward.IsRevealing;

		public override void UpdateUI()
		{
			focusSound.Silence();
			CardManager cardManager = PanelGroup as CardManager;
			evolutionStrip.Set(cardManager.GetEvolutionProgress());
			actions.ExitBanMode();
			ApplyNavigation(cardManager.OfferedCardCount);

			// 연출을 끈 설정이면 예전 튀어나오기
			if (UIEffects.Enabled == false)
			{
				deckPanelContent.localScale = Vector3.zero;
				deckPanelContent.DOScale(Vector3.one, openAnimDuration).SetEase(Ease.OutBack).SetUpdate(true);
			}
			else
				PlayReveal();
			// 첫 카드 포커스. 공개가 먼저 시작돼야 선택 정보가 공개 전에 뜨지 않음
			cardSelectButtons[0].Select();
		}

		private void PlayReveal()
		{
			revealEntries.Clear();
			foreach (UICardSlot slot in cardSelectButtons)
				if (slot.gameObject.activeSelf && slot.DataSO is CardData card)
					revealEntries.Add(new CardRevealEntry((RectTransform)slot.transform, UICardRevealDirector.TierOf(card)));
			reveal.Play(cardSelectButtons[0].ChoiceView.ChoiceCard.Theme.Reveal.SelectCard, revealEntries);
		}

		private void ShowSelectedInfo()
		{
			GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
			foreach (UICardSlot slot in cardSelectButtons)
				if (slot.gameObject == selected && slot.DataSO is CardData card)
					deckTabs.ShowInfo(card, slot.ChoiceDetail);
		}

		protected override void OnInit()
		{
			CardManager cardManager = PanelGroup as CardManager;
			focusSound = UICardFocusSound.AttachTo(gameObject, selected => chestReward.IsOpen || deckTabs.IsTabButton(selected));
			reveal = UICardRevealDirector.AttachTo(gameObject, (RectTransform)transform, deckPanelContent);
			reveal.Finished += ShowSelectedInfo;
			actions.Init(cardManager);
			actions.SetInputGate(reveal.ConsumeBlockedChoice);
			chestReward.Bind(cardManager);
			for (int i = 0; i < cardSelectButtons.Count; i++)
			{
				// cardSelectAction: (slot) => { SelectCard(cardSlots[i].Artifact as Card); }
				// 원래 위 코드를 썼는데, 클로저 문제로 인해 아래처럼 바꿈
				cardSelectButtons[i].Init();
				cardSelectButtons[i].SetClickAction((slot) =>
				{
					if (reveal.ConsumeBlockedChoice())
						return;
					if (actions.IsBanMode)
					{
						CardData target = slot.DataSO as CardData;
						if (cardManager.BanCard(target))
						{
							UiSfx.Play(UiSfxEvent.Blocked);
						}
						else
						{
							UiSfx.Play(UiSfxEvent.PurchaseFail);
							actions.ShowBanDenied(cardManager.BanDeniedText(target));
						}
					}
					else
						cardManager.SelectCard(slot.DataSO as CardData);
				});
				cardSelectButtons[i].SetSelectAction((slot) =>
				{
					// 공개 중 정보 표시는 결과 노출, 끝난 뒤로 미룸
					if (reveal.IsRevealing == false && slot.DataSO is CardData card)
						deckTabs.ShowInfo(card, (slot as UICardSlot).ChoiceDetail);
				});
			}
		}
		
		private void OnEnable() => UIHideWhileChatting.Request(this, true);

		private void OnDisable() => UIHideWhileChatting.Request(this, false);

		private void ApplyNavigation(int count)
		{
			actions.Refresh();
			actions.SetUpTarget(cardSelectButtons[0].Selectable);
			for (int i = 0; i < count && i < cardSelectButtons.Count; i++)
			{
				cardSelectButtons[i].SetNavigation(new Navigation
				{
					mode = Navigation.Mode.Explicit,
					selectOnLeft = i > 0 ? cardSelectButtons[i - 1].Selectable : null,
					selectOnRight = i < count - 1 ? cardSelectButtons[i + 1].Selectable : deckTabs.CurrentTabButton,
					selectOnDown = actions.FirstEnabled,
				});
			}
			deckTabs.SetCardNeighbor(cardSelectButtons[Mathf.Min(count, cardSelectButtons.Count) - 1].Selectable);
		}

		public void SetSkillTree(SkillTree tree, EquipmentData equipment, IReadOnlyList<CardData> selectedCards, IReadOnlyList<CardData> offeredCards) => deckTabs.SetSkillTree(tree, equipment, selectedCards, offeredCards);

		public void SetCardSelectButtons(List<CardData> cardDataList, EquipmentData equipment, IReadOnlyList<EquipmentData> equipments, IReadOnlyList<CardData> selectedCards, IReadOnlyList<CardData> deckCards, SkillTreeCatalog skillTrees)
		{
			reveal.Cancel();
			deckTabs.SetOverview(equipments, selectedCards, skillTrees);
			ApplyNavigation(cardDataList.Count);
			for (int i = 0; i < cardSelectButtons.Count; i++)
			{
				cardSelectButtons[i].SetActive(i < cardDataList.Count);
				if (i < cardDataList.Count)
				{
					cardSelectButtons[i].SetChoice(cardDataList[i], equipment, selectedCards, deckCards);
					cardSelectButtons[i].UpdateUI();
				}
				else
				{
					cardSelectButtons[i].SetChoice(null, equipment, selectedCards, deckCards);
				}
			}
		}
	}
}

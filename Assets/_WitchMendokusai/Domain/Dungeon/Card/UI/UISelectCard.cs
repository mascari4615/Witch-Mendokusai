using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
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

		public override bool IsFullscreen => true;

		public override void UpdateUI()
		{
			CardManager cardManager = PanelGroup as CardManager;
			evolutionStrip.Set(cardManager.GetEvolutionProgress());
			actions.ExitBanMode();
			ApplyNavigation(cardManager.OfferedCardCount);
			deckPanelContent.localScale = Vector3.zero;
			deckPanelContent.DOScale(Vector3.one, openAnimDuration).SetEase(Ease.OutBack).SetUpdate(true);

			// DeckPanel이 켜지면 첫 번째 카드 버튼 포커스
			cardSelectButtons[0].Select();
			for (int i = 0; i < cardSelectButtons.Count; i++)
				if (cardSelectButtons[i].gameObject.activeSelf)
					cardSelectButtons[i].ChoiceView.ChoiceCard.PlayAppear(i);
		}

		protected override void OnInit()
		{
			CardManager cardManager = PanelGroup as CardManager;
			actions.Init(cardManager);
			chestReward.Bind(cardManager);
			for (int i = 0; i < cardSelectButtons.Count; i++)
			{
				// cardSelectAction: (slot) => { SelectCard(cardSlots[i].Artifact as Card); }
				// 원래 위 코드를 썼는데, 클로저 문제로 인해 아래처럼 바꿈
				cardSelectButtons[i].Init();
				cardSelectButtons[i].SetClickAction((slot) =>
				{
					if (actions.IsBanMode)
					{
						CardData target = slot.DataSO as CardData;
						if (cardManager.BanCard(target) == false)
							actions.ShowBanDenied(cardManager.BanDeniedText(target));
					}
					else
						cardManager.SelectCard(slot.DataSO as CardData);
				});
				cardSelectButtons[i].SetSelectAction((slot) =>
				{
					if (slot.DataSO is CardData card)
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

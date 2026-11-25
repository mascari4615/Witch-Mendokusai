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
		private Sequence receipt;
		private UICardSlot receivingSlot;
		private Vector3 receiptHome;
		private Vector3 receiptScale;
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

			// 연출을 끈 설정이나 공개 스킨 없음이면 예전 튀어나오기
			if (UIEffects.Enabled == false || PlayReveal() == false)
			{
				deckPanelContent.localScale = Vector3.zero;
				deckPanelContent.DOScale(Vector3.one, openAnimDuration).SetEase(Ease.OutBack).SetUpdate(true);
			}
			// 첫 카드 포커스. 공개가 먼저 시작돼야 선택 정보가 공개 전에 뜨지 않음
			cardSelectButtons[0].Select();
		}

		// 형태 카드를 덮은 빛은 공개 연출이 끝난 뒤에 걷음 (연출이 없거나 다시 뽑았을 때는 바로)
		private void LateUpdate()
		{
			if (reveal == null || reveal.IsRevealing)
				return;
			foreach (UICardSlot slot in cardSelectButtons)
				if (slot.ChoiceView.FormFace != null)
					slot.ChoiceView.FormFace.Uncover();
		}

		private bool PlayReveal()
		{
			if (devPending != null)
			{
				DevRevealRequest request = devPending;
				devPending = null;
				DevReplay(request);
				return true;
			}
			revealEntries.Clear();
			CardManager cardManager = PanelGroup as CardManager;
			foreach (UICardSlot slot in cardSelectButtons)
				if (slot.gameObject.activeSelf && slot.DataSO is CardData card)
					revealEntries.Add(new CardRevealEntry((RectTransform)slot.transform, UICardRevealDirector.TierOf(card), cardManager.FormLookOf(card)?.Main));
			return reveal.Play(Theme.Reveal.SelectCard, revealEntries, chained: cardManager.IsChainedChoice);
		}

		public SurvivalUITheme Theme => cardSelectButtons[0].ChoiceView.ChoiceCard.Theme;
		public bool IsShowing => gameObject.activeInHierarchy;

		// 개발 창 reveal. 다음 공개를 이 스킨과 등급으로
		public sealed class DevRevealRequest
		{
			public CardRevealSkin Skin;
			public bool Chained;
			// null 이면 실제 카드 등급
			public CardRevealTier? Best;
		}

		private static DevRevealRequest devPending;

		public static void DevQueue(DevRevealRequest request) => devPending = request;

		// 지금 열린 카드로 다시 공개. 등급을 정하면 가운데 칸이 그 등급, 영웅이면 왼쪽 희귀
		public void DevReplay(DevRevealRequest request)
		{
			revealEntries.Clear();
			int index = 0;
			foreach (UICardSlot slot in cardSelectButtons)
			{
				CardData card = slot.DataSO as CardData;
				if (slot.gameObject.activeSelf == false || card == null)
					continue;
				CardRevealTier tier = UICardRevealDirector.TierOf(card);
				if (request.Best.HasValue)
					tier = index == 1 ? request.Best.Value : index == 0 && request.Best.Value >= CardRevealTier.Epic ? CardRevealTier.Rare : CardRevealTier.Common;
				slot.ChoiceView.ShowTier(tier);
				revealEntries.Add(new CardRevealEntry((RectTransform)slot.transform, tier));
				index++;
			}
			reveal.PlayExact(request.Skin, revealEntries, chained: request.Chained);
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
			reveal.ExtraBlock = IsFormCardCovered;
			actions.Init(cardManager);
			actions.SetInputGate(ConsumeInput);
			chestReward.Bind(cardManager);
			for (int i = 0; i < cardSelectButtons.Count; i++)
			{
				// cardSelectAction: (slot) => { SelectCard(cardSlots[i].Artifact as Card); }
				// 원래 위 코드를 썼는데, 클로저 문제로 인해 아래처럼 바꿈
				cardSelectButtons[i].Init();
				cardSelectButtons[i].SetClickAction((slot) =>
				{
					if (ConsumeInput())
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
						ReceiveCard((UICardSlot)slot, cardManager);
				});
				cardSelectButtons[i].SetSelectAction((slot) =>
				{
					// 공개 중 정보 표시는 결과 노출, 끝난 뒤로 미룸
					if (receipt == null && reveal.IsRevealing == false && slot.DataSO is CardData card)
						deckTabs.ShowInfo(card, (slot as UICardSlot).ChoiceDetail);
				});
			}
		}
		
		private void OnEnable() => UIHideWhileChatting.Request(this, true);

		// 형태 카드가 빛에 덮여 글을 못 읽는 동안도 막음 (reveal.ExtraBlock. 되돌릴 수 없는 변신을 보지도 않고 고르지 않게)
		private bool ConsumeInput() => receipt != null || reveal.ConsumeBlockedChoice();

		private bool IsFormCardCovered()
		{
			foreach (UICardSlot slot in cardSelectButtons)
			{
				if (slot.gameObject.activeSelf && slot.ChoiceView.FormFace != null && slot.ChoiceView.FormFace.IsCovered)
					return true;
			}
			return false;
		}

		private void ReceiveCard(UICardSlot slot, CardManager manager)
		{
			CardData card = slot.DataSO as CardData;
			if (card == null)
				return;
			if (UIEffects.Enabled == false)
			{
				manager.SelectCard(card);
				return;
			}

			Canvas canvas = GetComponentInParent<Canvas>().rootCanvas;
			int before = manager.HeldCount(card);
			RectTransform rect = (RectTransform)slot.transform;
			RectTransform target = deckTabs.ReceiptTarget(card);
			receivingSlot = slot;
			receiptHome = rect.localPosition;
			receiptScale = rect.localScale;
			receipt = DOTween.Sequence().SetUpdate(true);
			slot.ChoiceView.ChoiceCard.PlayConfirm();
			if (slot.ChoiceView.FormFace != null)
				slot.ChoiceView.FormFace.PlayPicked(canvas);
			foreach (UICardSlot other in cardSelectButtons)
			{
				if (other == slot || other.gameObject.activeSelf == false)
					continue;
				if (other.TryGetComponent(out CanvasGroup group) == false)
					group = other.gameObject.AddComponent<CanvasGroup>();
				receipt.Insert(0f, group.DOFade(0.26f, 0.16f));
			}

			receipt.Insert(0f, rect.DOScale(receiptScale * 1.1f, 0.17f).SetEase(Ease.OutQuad));
			receipt.Insert(0f, rect.DOLocalMoveY(receiptHome.y + 16f, 0.17f));
			receipt.Insert(0.36f, rect.DOMove(target.TransformPoint(target.rect.center), 0.38f).SetEase(Ease.InOutCubic));
			receipt.Insert(0.36f, rect.DOScale(receiptScale * 0.14f, 0.38f).SetEase(Ease.InQuad));
			receipt.OnComplete(() =>
			{
				receipt = null;
				RestoreChoice();
				manager.SelectCard(card);
				int count = manager.HeldCount(card);
				if (count > before && canvas != null)
					slot.ChoiceView.ShowReceipt(card, count, canvas);
			});
		}

		private void RestoreChoice()
		{
			if (receivingSlot == null)
				return;
			receipt?.Kill();
			receipt = null;
			receivingSlot.transform.localPosition = receiptHome;
			receivingSlot.transform.localScale = receiptScale;
			receivingSlot = null;
			foreach (UICardSlot slot in cardSelectButtons)
				if (slot.TryGetComponent(out CanvasGroup group))
					group.alpha = 1f;
		}

		private void OnDisable()
		{
			RestoreChoice();
			UIHideWhileChatting.Request(this, false);
		}

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
			RestoreChoice();
			reveal.Cancel();
			deckTabs.SetOverview(equipments, selectedCards, skillTrees);
			ApplyNavigation(cardDataList.Count);
			for (int i = 0; i < cardSelectButtons.Count; i++)
			{
				cardSelectButtons[i].SetActive(i < cardDataList.Count);
				if (i < cardDataList.Count)
				{
					cardSelectButtons[i].SetChoice(cardDataList[i], equipment, selectedCards, deckCards, ((CardManager)PanelGroup).FormLookOf(cardDataList[i]));
					cardSelectButtons[i].UpdateUI();
				}
				else
				{
					cardSelectButtons[i].SetChoice(null, equipment, selectedCards, deckCards, null);
				}
			}
		}
	}
}

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	// 상자 보상 패널. 보상은 이벤트 발행 전 지급 완료, 이 화면은 표시만 담당
	public class UIChestReward : MonoBehaviour
	{
		[SerializeField] private SurvivalUITheme theme;
		[SerializeField] private GameObject content;
		[SerializeField] private Transform cellsParent;
		[SerializeField] private GameObject cellTemplate;
		[SerializeField] private Button closeButton;
		[SerializeField] private Image closeTimer;
		[SerializeField] private UIBurst burst;
		[SerializeField] private Sprite nyangSprite;
		[SerializeField] private string nyangName = "냥";
		[SerializeField] private TextMeshProUGUI title;
		[SerializeField] private string evolutionTag = "진화 2";
		[SerializeField] private SurvivalUIKind nyangKind = SurvivalUIKind.Speed;

		private readonly List<GameObject> cells = new();
		private CardManager cardManager;
		private float autoCloseSeconds;
		private float openedAt;
		private RectTransform rewardLayout;
		private UICardRevealSequence reveal;
		private UIChestOpenEffect openEffect;
		private CanvasGroup cellsGroup;
		private readonly List<CardRevealEntry> revealEntries = new();
		private readonly List<CardRevealSymbol> revealSymbols = new();

		public bool IsOpen => content.activeSelf;
		// 개봉 예고와 슬롯 회전 중. 자동 닫기 대기 기준
		public bool IsRevealing => openEffect.IsPlaying || reveal.IsRevealing;

		private void Awake()
		{
			BuildRewardLayout();
			reveal = UICardRevealSequence.AttachTo(gameObject);
			RectTransform cellsRect = (RectTransform)cellsParent;
			openEffect = UIChestOpenEffect.Create((RectTransform)cellsRect.parent, cellsRect.localPosition + (Vector3)cellsRect.rect.center, (RectTransform)content.transform, theme.TermIcon(SurvivalTerm.ChestReward));
			if (cellsParent.TryGetComponent(out cellsGroup) == false)
				cellsGroup = cellsParent.gameObject.AddComponent<CanvasGroup>();
			closeButton.onClick.AddListener(Close);
			UITermIcon.Attach(title, theme, SurvivalTerm.ChestReward);
			content.SetActive(false);
			cellTemplate.SetActive(false);
		}

		private void BuildRewardLayout()
		{
			RectTransform viewport = (RectTransform)content.transform;
			List<RectTransform> children = new();
			foreach (Transform child in viewport)
				if (child is RectTransform rect)
					children.Add(rect);
			GameObject layout = new("Reward layout", typeof(RectTransform));
			rewardLayout = (RectTransform)layout.transform;
			rewardLayout.SetParent(viewport, false);
			rewardLayout.anchorMin = rewardLayout.anchorMax = new Vector2(.5f, .5f);
			rewardLayout.sizeDelta = new Vector2(1920f, 1080f);
			foreach (RectTransform child in children)
			{
				Vector2 position = child.anchoredPosition;
				child.SetParent(rewardLayout, false);
				child.anchoredPosition = position;
			}
			FitRewardLayout();
		}

		private void OnRectTransformDimensionsChange() => FitRewardLayout();

		private void FitRewardLayout()
		{
			if (rewardLayout == null)
				return;
			Rect viewport = ((RectTransform)content.transform).rect;
			if (viewport.width <= 0f || viewport.height <= 0f)
				return;
			float scale = Mathf.Min(1f, viewport.width / 1920f, viewport.height / 1080f);
			rewardLayout.localScale = Vector3.one * scale;
		}

		private void OnEnable()
		{
			EventBusBridge.Subscribe<ChestOpenedEvent>(OnChestOpened);
			EventBusBridge.Subscribe<ChestClosedEvent>(OnChestClosed);
		}

		private void OnDisable()
		{
			EventBusBridge.Unsubscribe<ChestOpenedEvent>(OnChestOpened);
			EventBusBridge.Unsubscribe<ChestClosedEvent>(OnChestClosed);
			UIHideWhileChatting.Request(this, false);
		}

		private void Update()
		{
			if (content.activeSelf == false || autoCloseSeconds <= 0f)
				return;
			// 자동 닫기 시간은 연출이 끝난 뒤부터
			if (IsRevealing)
				openedAt = Time.unscaledTime;
			closeTimer.fillAmount = Mathf.Clamp01(1f - (Time.unscaledTime - openedAt) / autoCloseSeconds);
		}

		public void Bind(CardManager manager) => cardManager = manager;

		private void Close()
		{
			if (openEffect.IsPlaying || reveal.BlocksChoice)
			{
				openEffect.Skip();
				reveal.SkipAll();
				return;
			}
			UiSfx.Play(UiSfxEvent.Confirm);
			cardManager.CloseChest();
		}

		private void OnChestClosed(ChestClosedEvent closedEvent)
		{
			openEffect.Cancel();
			reveal.Cancel();
			content.SetActive(false);
			UIHideWhileChatting.Request(this, false);
		}

		private void OnChestOpened(ChestOpenedEvent openedEvent)
		{
			openEffect.Cancel();
			reveal.Cancel();
			foreach (GameObject cell in cells)
			{
				cell.SetActive(false);
				Destroy(cell);
			}
			cells.Clear();

			content.SetActive(true);
			FitRewardLayout();
			UIHideWhileChatting.Request(this, true);
			autoCloseSeconds = openedEvent.AutoCloseSeconds;
			openedAt = Time.unscaledTime;
			closeTimer.gameObject.SetActive(autoCloseSeconds > 0f);
			closeTimer.fillAmount = 1f;

			bool hasEvolution = false;
			revealEntries.Clear();
			revealSymbols.Clear();
			revealSymbols.Add(new CardRevealSymbol(nyangSprite, nyangName));
			HashSet<int> symbolIds = new();
			foreach (CardData owned in SOManagerBridge.SelectedCardBuffer.Data)
				UICardRevealSequence.AddSymbol(revealSymbols, symbolIds, owned);
			for (int i = 0; i < openedEvent.Rewards.Count; i++)
			{
				ChestReward reward = openedEvent.Rewards[i];
				hasEvolution |= reward.IsEvolution;
				GameObject cell = Instantiate(cellTemplate, cellsParent);
				cell.SetActive(true);
				cells.Add(cell);
				FillCell(cell.transform, reward);
				UICardRevealSequence.AddSymbol(revealSymbols, symbolIds, reward.Card);
				revealEntries.Add(new CardRevealEntry(
					cell.transform.Find("[Image] Icon").GetComponent<Image>(),
					cell.transform.Find("[Text] Name").GetComponent<TextMeshProUGUI>(),
					cell.transform, null, RevealTier(reward),
					new List<GameObject> { cell.transform.Find("[Block] Tag").gameObject }));
			}

			CardRevealTier bestTier = CardRevealTier.Common;
			foreach (CardRevealEntry entry in revealEntries)
				if (entry.Tier > bestTier)
					bestTier = entry.Tier;
			cellsGroup.alpha = 0f;
			closeButton.Select();
			openEffect.Play(theme.CardReveal, theme.CardReveal.TierColor(bestTier), skipped => ShowRewards(hasEvolution, skipped));
		}

		// 상자 열림 순간. 칸 등장, 튀는 조각, 슬롯 회전
		private void ShowRewards(bool hasEvolution, bool skipped)
		{
			cellsGroup.alpha = 1f;
			for (int i = 0; i < cells.Count; i++)
				UIEffects.PopIn(cells[i].transform, theme.CardAppearDuration, i * theme.CardAppearInterval);
			reveal.Play(revealEntries, revealSymbols, theme.CardReveal);
			if (skipped)
				reveal.SkipAll();
			// 슬롯 연출 중이면 진화음은 멈춤 때 재생
			if (hasEvolution && UIEffects.Enabled == false)
				UiSfx.Play(UiSfxEvent.Evolve);
			Color burstColor = hasEvolution ? theme.Style(SurvivalUIKind.Advanced).Main : theme.MaxColor;
			if (UIEffects.Enabled)
				burst.Play(burstColor, theme.CardReveal.ChestBurstCount);
			else
				burst.Play(burstColor);
		}

		private static CardRevealTier RevealTier(ChestReward reward)
		{
			return reward.Card != null ? UICardRevealSequence.TierOf(reward.Card, reward.IsEvolution) : CardRevealTier.Common;
		}

		private void FillCell(Transform cell, ChestReward reward)
		{
			bool isCard = reward.Card != null;
			SurvivalUITheme.KindStyle style = theme.Style(isCard ? reward.Card.UIKind : nyangKind);
			cell.Find("[Image] Diamond").GetComponent<Image>().color = style.Dark;
			cell.Find("[Image] Diamond/[Image] Line").GetComponent<Image>().color = style.Main;
			cell.Find("[Image] Icon").GetComponent<Image>().sprite = isCard ? reward.Card.Sprite : nyangSprite;
			cell.Find("[Text] Name").GetComponent<TextMeshProUGUI>().text = isCard ? reward.Card.Name : $"{reward.Nyang} {nyangName}";
			GameObject tag = cell.Find("[Block] Tag").gameObject;
			tag.SetActive(reward.IsEvolution);
			TextMeshProUGUI tagText = tag.GetComponentInChildren<TextMeshProUGUI>(true);
			tagText.text = evolutionTag;
			UITermIcon.Attach(tagText, theme, SurvivalTerm.Evolution);
		}
	}
}

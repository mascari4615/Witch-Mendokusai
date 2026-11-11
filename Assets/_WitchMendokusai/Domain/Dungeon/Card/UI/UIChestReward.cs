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

		public bool IsOpen => content.activeSelf;

		private void Awake()
		{
			BuildRewardLayout();
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
			closeTimer.fillAmount = Mathf.Clamp01(1f - (Time.unscaledTime - openedAt) / autoCloseSeconds);
		}

		public void Bind(CardManager manager) => cardManager = manager;

		private void Close()
		{
			UiSfx.Play(UiSfxEvent.Confirm);
			cardManager.CloseChest();
		}

		private void OnChestClosed(ChestClosedEvent closedEvent)
		{
			content.SetActive(false);
			UIHideWhileChatting.Request(this, false);
		}

		private void OnChestOpened(ChestOpenedEvent openedEvent)
		{
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
			for (int i = 0; i < openedEvent.Rewards.Count; i++)
			{
				ChestReward reward = openedEvent.Rewards[i];
				hasEvolution |= reward.IsEvolution;
				GameObject cell = Instantiate(cellTemplate, cellsParent);
				cell.SetActive(true);
				cells.Add(cell);
				FillCell(cell.transform, reward);
				UIEffects.PopIn(cell.transform, theme.CardAppearDuration, i * theme.CardAppearInterval);
			}

			if (hasEvolution)
				UiSfx.Play(UiSfxEvent.Evolve);
			burst.Play(hasEvolution ? theme.Style(SurvivalUIKind.Advanced).Main : theme.MaxColor);
			closeButton.Select();
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

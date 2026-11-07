using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WitchMendokusai
{
	// 카드 선택 오른쪽 패널. 덱 전체 탭과 선택 장비의 스킬 트리 탭
	public class UIDeckTabs : MonoBehaviour
	{
		[SerializeField] private Button deckTabButton;
		[SerializeField] private Button treeTabButton;
		[SerializeField] private Image deckTabLine;
		[SerializeField] private Image treeTabLine;
		[SerializeField] private GameObject deckRoot;
		[SerializeField] private GameObject treeRoot;
		[SerializeField] private Transform rowsParent;
		[SerializeField] private GameObject rowTemplate;
		[SerializeField] private TextMeshProUGUI infoName;
		[SerializeField] private TextMeshProUGUI infoText;
		[SerializeField] private GameObject treeEmptyNotice;
		[SerializeField] private UISkillTreeTab skillTreeTab;
		[SerializeField] private int compactRowThreshold = 3;
		[SerializeField] private float normalRowHeight = 96f;
		[SerializeField] private float compactRowHeight = 76f;
		[SerializeField] private float normalCell = 62f;
		[SerializeField] private float compactCell = 54f;
		[SerializeField] private Color activeLine = Color.white;
		[SerializeField] private Color idleLine = Color.gray;

		private readonly List<GameObject> rows = new();
		private bool initialized;
		private bool isDeckTab = true;
		private Selectable cardNeighbor;

		private void Init()
		{
			if (initialized)
				return;
			initialized = true;
			deckTabButton.onClick.AddListener(() => SetTab(true));
			treeTabButton.onClick.AddListener(() => SetTab(false));
			rowTemplate.SetActive(false);
			SetTab(true);
		}

		public void SetOverview(IReadOnlyList<EquipmentData> equipments, IReadOnlyList<CardData> selectedCards, SkillTreeCatalog skillTrees)
		{
			Init();
			foreach (GameObject row in rows)
				Destroy(row);
			rows.Clear();
			bool isCompact = equipments.Count(item => item != null) > compactRowThreshold;
			foreach (EquipmentData equipment in equipments.Where(item => item != null))
			{
				GameObject row = Instantiate(rowTemplate, rowsParent);
				row.SetActive(true);
				rows.Add(row);
				RectTransform rowRect = (RectTransform)row.transform;
				rowRect.sizeDelta = new Vector2(rowRect.sizeDelta.x, isCompact ? compactRowHeight : normalRowHeight);
				row.transform.Find("[Image] EquipmentIcon").GetComponent<Image>().sprite = equipment.Sprite;
				row.transform.Find("[Text] EquipmentName").GetComponent<TextMeshProUGUI>().text = equipment.Name;
				Transform list = row.transform.Find("[Block] List");
				float cell = isCompact ? compactCell : normalCell;
				list.GetComponent<GridLayoutGroup>().cellSize = new Vector2(cell, cell);
				UIMiniCard template = list.GetChild(0).GetComponent<UIMiniCard>();
				foreach (CardData card in skillTrees.CardsFor(equipment).Where(item => item != null).GroupBy(item => item.ID).Select(group => group.First()))
				{
					UIMiniCard mini = Instantiate(template, list);
					mini.gameObject.SetActive(true);
					mini.SetCard(card, selectedCards.Count(selected => selected.ID == card.ID));
				}
				template.gameObject.SetActive(false);
			}
		}

		public Selectable CurrentTabButton => isDeckTab ? deckTabButton : treeTabButton;

		public void SetCardNeighbor(Selectable neighbor)
		{
			cardNeighbor = neighbor;
			ApplyNavigation();
		}

		private void Update()
		{
			GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
			if (selected == deckTabButton.gameObject && isDeckTab == false)
				SetTab(true);
			else if (selected == treeTabButton.gameObject && isDeckTab)
				SetTab(false);
		}

		private void ApplyNavigation()
		{
			Selectable treeFocus = isDeckTab == false && skillTreeTab.IsInteractable() ? skillTreeTab : null;
			deckTabButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = cardNeighbor, selectOnRight = treeTabButton };
			treeTabButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = deckTabButton, selectOnDown = treeFocus };
			skillTreeTab.SetNeighbors(treeTabButton, cardNeighbor);
		}

		public void SetSkillTree(SkillTree tree, EquipmentData equipment, IReadOnlyList<CardData> selectedCards, IReadOnlyList<CardData> offeredCards)
		{
			Init();
			treeEmptyNotice.SetActive(tree == null);
			skillTreeTab.Show(tree, equipment, selectedCards, offeredCards);
			ApplyNavigation();
		}

		public void ShowInfo(CardData card, string description)
		{
			infoName.text = card.Name;
			infoText.text = description;
			skillTreeTab.Preview(card);
		}

		private void SetTab(bool showDeck)
		{
			isDeckTab = showDeck;
			deckRoot.SetActive(showDeck);
			treeRoot.SetActive(showDeck == false);
			deckTabLine.color = showDeck ? activeLine : idleLine;
			treeTabLine.color = showDeck ? idleLine : activeLine;
			ApplyNavigation();
		}
	}
}

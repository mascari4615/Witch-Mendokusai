using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	// 카드 선택 오른쪽 패널. 덱 전체 탭과 선택 장비의 스킬 트리 탭 (트리는 기존 UIDeck)
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
		[SerializeField] private Color activeLine = Color.white;
		[SerializeField] private Color idleLine = Color.gray;

		private readonly List<GameObject> rows = new();
		private bool initialized;

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

		public void SetOverview(IReadOnlyList<EquipmentData> equipments, IReadOnlyList<CardData> selectedCards)
		{
			Init();
			foreach (GameObject row in rows)
				Destroy(row);
			rows.Clear();
			foreach (EquipmentData equipment in equipments.Where(item => item != null))
			{
				GameObject row = Instantiate(rowTemplate, rowsParent);
				row.SetActive(true);
				rows.Add(row);
				row.transform.Find("[Image] EquipmentIcon").GetComponent<Image>().sprite = equipment.Sprite;
				row.transform.Find("[Text] EquipmentName").GetComponent<TextMeshProUGUI>().text = equipment.Name;
				Transform list = row.transform.Find("[Block] List");
				UIMiniCard template = list.GetChild(0).GetComponent<UIMiniCard>();
				foreach (CardData card in equipment.EffectCards.Where(item => item != null).GroupBy(item => item.ID).Select(group => group.First()))
				{
					UIMiniCard mini = Instantiate(template, list);
					mini.gameObject.SetActive(true);
					mini.SetCard(card, selectedCards.Count(selected => selected.ID == card.ID));
				}
				template.gameObject.SetActive(false);
			}
		}

		public void ShowInfo(CardData card, string description)
		{
			infoName.text = card.Name;
			infoText.text = description;
		}

		private void SetTab(bool showDeck)
		{
			deckRoot.SetActive(showDeck);
			treeRoot.SetActive(showDeck == false);
			deckTabLine.color = showDeck ? activeLine : idleLine;
			treeTabLine.color = showDeck ? idleLine : activeLine;
		}
	}
}

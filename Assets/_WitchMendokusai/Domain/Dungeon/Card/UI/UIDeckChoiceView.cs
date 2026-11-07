using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	// 레벨업 장비 칸: 장비 색, 이번 판 강화 수, 카드 목록
	public class UIDeckChoiceView : MonoBehaviour
	{
		[SerializeField] private SurvivalUITheme theme;
		[SerializeField] private UIChoiceCard choiceCard;
		[SerializeField] private Image diamond;
		[SerializeField] private Image diamondLine;
		[SerializeField] private Image tile;
		[SerializeField] private Image panelTile;
		[SerializeField] private GridLayoutGroup miniGrid;
		[SerializeField] private int compactThreshold = 6;
		[SerializeField] private float normalCell = 62f;
		[SerializeField] private float compactCell = 56f;
		[SerializeField] private TextMeshProUGUI totalText;
		[SerializeField] private Transform miniParent;
		[SerializeField] private UIMiniCard miniTemplate;

		private readonly List<UIMiniCard> minis = new();

		public void Set(EquipmentData equipment, IReadOnlyList<CardData> equipmentCards, IReadOnlyList<CardData> selectedCards)
		{
			SurvivalUITheme.KindStyle style = theme.Style(equipment.UIKind);
			choiceCard.SetKind(equipment.UIKind);
			diamond.color = style.Dark;
			diamondLine.color = style.Main;
			tile.sprite = style.Tile;
			panelTile.sprite = style.Tile;

			List<CardData> cards = equipmentCards.Where(card => card != null).GroupBy(card => card.ID).Select(group => group.First()).ToList();
			float cell = cards.Count > compactThreshold ? compactCell : normalCell;
			miniGrid.cellSize = new Vector2(cell, cell);
			int total = 0;
			for (int i = 0; i < cards.Count; i++)
			{
				if (i >= minis.Count)
					minis.Add(Instantiate(miniTemplate, miniParent));
				int count = selectedCards.Count(selected => selected.ID == cards[i].ID);
				total += count;
				minis[i].gameObject.SetActive(true);
				minis[i].SetCard(cards[i], count);
			}
			for (int i = cards.Count; i < minis.Count; i++)
				minis[i].gameObject.SetActive(false);
			miniTemplate.gameObject.SetActive(false);
			totalText.text = $"{total}회";
		}
	}
}

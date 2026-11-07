using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	// 덱 탭 아래 진화 진행 표시. 장착 장비로 가능한 진화만 표시
	public class UIEvolutionStrip : MonoBehaviour
	{
		[SerializeField] private SurvivalUITheme theme;
		[SerializeField] private GameObject root;
		[SerializeField] private TextMeshProUGUI label;
		[SerializeField] private Transform chipsParent;
		[SerializeField] private GameObject chipTemplate;
		[SerializeField] private string readyText = "준비";
		[SerializeField] private string ownedText = "완료";
		[SerializeField] private string blockedText = "막힘";
		[SerializeField] private string progressFormat = "{0}/{1}";
		[SerializeField] private Color blockedColor = new(0.886f, 0.345f, 0.298f, 1f);
		[SerializeField] private Color progressColor = Color.white;

		private readonly List<GameObject> chips = new();

		public void Set(IReadOnlyList<CardEvolutionProgress> progressList)
		{
			foreach (GameObject chip in chips)
				Destroy(chip);
			chips.Clear();
			chipTemplate.SetActive(false);
			root.SetActive(progressList.Count > 0);
			UITermIcon.Attach(label, theme, SurvivalTerm.Evolution);

			foreach (CardEvolutionProgress progress in progressList)
			{
				GameObject chip = Instantiate(chipTemplate, chipsParent);
				chip.SetActive(true);
				chips.Add(chip);
				chip.transform.Find("[Image] Icon").GetComponent<Image>().sprite = progress.Card.Sprite;
				TextMeshProUGUI state = chip.transform.Find("[Text] State").GetComponent<TextMeshProUGUI>();
				state.text = Describe(progress);
				state.color = progress.IsBlocked ? blockedColor : progress.IsReady ? theme.MaxColor : progressColor;
			}
		}

		private string Describe(CardEvolutionProgress progress)
		{
			if (progress.IsOwned)
				return ownedText;
			if (progress.IsReady)
				return readyText;
			if (progress.IsBlocked)
				return blockedText;
			return string.Format(progressFormat, progress.Current, progress.Required);
		}
	}
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	public class UIExpeditionSlot : MonoBehaviour
	{
		[SerializeField] private TMP_Text nameText;
		[SerializeField] private TMP_Text durationText;
		[SerializeField] private Button startButton;

		private ExpeditionSO data;

		public void SetData(ExpeditionSO expeditionSO, System.Action onStart)
		{
			data = expeditionSO;
			nameText.text = expeditionSO.Name;
			int minutes = Mathf.CeilToInt(expeditionSO.DurationSeconds / 60f);
			durationText.text = $"{minutes}분";
			startButton.onClick.RemoveAllListeners();
			startButton.onClick.AddListener(() => onStart?.Invoke());
		}
	}
}

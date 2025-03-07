using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace WitchMendokusai
{
	public class ToolTip : MonoBehaviour
	{
		[SerializeField] private Image image;
		[SerializeField] private TextMeshProUGUI nameText;
		[SerializeField] private TextMeshProUGUI descriptionText;
		[SerializeField] private TextMeshProUGUI gradeText;

		[SerializeField] private ContentFitterRefresh contentFitterRefresh;

		public void SetToolTipContent(SlotData slotData)
		{
			image.sprite = slotData.Sprite;
			image.color = slotData.Sprite == null ? Color.clear : Color.white;
			nameText.text = slotData.Name;
			descriptionText.text = slotData.Description;
			gradeText.text = "";

			if (contentFitterRefresh != null)
				contentFitterRefresh.RefreshContentFitters();
		}

		public void Clear()
		{
			image.sprite = null;
			image.color = Color.clear;
			nameText.text = "";
			descriptionText.text = "";
			gradeText.text = "";

			if (contentFitterRefresh != null)
				contentFitterRefresh.RefreshContentFitters();
		}
	}
}
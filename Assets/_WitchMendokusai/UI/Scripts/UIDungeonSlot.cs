using TMPro;

namespace WitchMendokusai
{
	public class UIDungeonSlot : UISlot
	{
		protected TextMeshProUGUI playTimeText;
		protected TextMeshProUGUI typeText;

		public override bool Init()
		{
			if (base.Init() == false)
				return false;

			playTimeText = transform.Find("[Text] PlayTime").GetComponent<TextMeshProUGUI>();
			typeText = transform.Find("[Text] Type").GetComponent<TextMeshProUGUI>();

			return true;
		}

		public override void UpdateUI()
		{
			base.UpdateUI();

			if (DataSO)
			{
				Dungeon dungeon = DataSO as Dungeon;
				playTimeText.text = $"{dungeon.TimeBySecond / 60}분";
				typeText.text = dungeon.Type.ToString();
			}
			else
			{
				playTimeText.text = string.Empty;
				typeText.text = string.Empty;

			}
		}
	}
}
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static WitchMendokusai.MDataSOUtil;
using static WitchMendokusai.DataSODefine;

namespace WitchMendokusai
{
	public class MDataSOSlot
	{
		public DataSO DataSO { get; private set; }
		public VisualElement VisualElement { get; private set; }

		private readonly Button button;
		private readonly Label nameLabel;
		private readonly Label idLabel;

		public MDataSOSlot(Action<MDataSOSlot> clickAction)
		{
			VisualTreeAsset treeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{EDITOR_DIR}MDataSO/MDataSOSlot/MDataSOSlot.uxml");
			VisualElement = treeAsset.Instantiate();

			button = VisualElement.Q<Button>();
			nameLabel = VisualElement.Q<Label>(name: "Name");
			idLabel = VisualElement.Q<Label>(name: "ID");

			button.RegisterCallback<ClickEvent>((_) =>
			{
				if (DataSO != null)
					clickAction?.Invoke(this);
			});
			UpdateUI();
		}

		public void SetDataSO(DataSO dataSO)
		{
			DataSO = dataSO;
			UpdateUI();
		}

		public void UpdateUI()
		{
			if (DataSO == null)
			{
				nameLabel.text = string.Empty;
				idLabel.text = string.Empty;
				button.style.backgroundImage = null;

				button.style.borderTopColor = Color.black;
				button.style.borderBottomColor = Color.black;
				button.style.borderLeftColor = Color.black;
				button.style.borderRightColor = Color.black;
			}
			else
			{
				nameLabel.text = DataSO.Name;
				idLabel.text = DataSO.ID.ToString();
				if (DataSO.Sprite != null)
					button.style.backgroundImage = new(DataSO.Sprite);

				// new Color(226 / 255f, 137 / 255f, 45 / 255f)
				Color borderColor = MDataSO.Instance.CurSlot == this ? Color.white : Color.black;
				button.style.borderTopColor = borderColor;
				button.style.borderBottomColor = borderColor;
				button.style.borderLeftColor = borderColor;
				button.style.borderRightColor = borderColor;
			}
		}
	}
}
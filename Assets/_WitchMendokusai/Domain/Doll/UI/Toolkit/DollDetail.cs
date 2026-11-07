using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	public class DollDetail : VisualElement
	{
		public const string USS_CLASS = "wm-doll-detail";
		public const string USS_NAME = "wm-doll-detail__name";
		public const string USS_LEVEL = "wm-doll-detail__level";
		public const string USS_SECTION = "wm-doll-detail__section";
		public const string USS_SECTION_LABEL = "wm-doll-detail__section-label";
		public const string USS_SIGNATURE_SLOT = "wm-doll-detail__signature";
		public const string USS_SIGNATURE_ROW = "wm-doll-detail__signature-row";
		public const string USS_SIGNATURE_NAME = "wm-doll-detail__signature-name";
		public const string USS_SELECT_BUTTON = "wm-doll-detail__select";

		private readonly Label nameLabel;
		private readonly Label levelLabel;
		private readonly VisualElement signatureSlot;
		private readonly VisualElement signatureIcon;
		private readonly Label signatureName;
		private readonly ItemGrid equipmentGrid;
		private readonly Button selectButton;

		private Doll doll;
		private readonly DollEquipmentInventory equipmentInventory;

		// VisualElement 는 MonoBehaviour 가 아니라 [Inject] 사용 불가 — caller (DollView, MonoBehaviour)
		// 가 inject 받은 인스턴스를 생성자로 전달한다.
		private readonly DataManager dataManager;
		private readonly PlayerProvider playerProvider;

		public DollDetail(DataManager dataManager, PlayerProvider playerProvider)
		{
			this.dataManager = dataManager;
			this.playerProvider = playerProvider;

			AddToClassList(USS_CLASS);

			nameLabel = new Label();
			nameLabel.AddToClassList(USS_NAME);
			Add(nameLabel);

			levelLabel = new Label();
			levelLabel.AddToClassList(USS_LEVEL);
			Add(levelLabel);

			Add(BuildSignatureSection(out signatureSlot, out signatureIcon, out signatureName));

			VisualElement equipmentSection = BuildSection("장비");
			equipmentGrid = new ItemGrid();
			equipmentSection.Add(equipmentGrid);
			Add(equipmentSection);

			selectButton = new Button(OnSelectClicked) { text = "이 인형으로 전환" };
			selectButton.AddToClassList(USS_SELECT_BUTTON);
			selectButton.AddToClassList(WMWindow.NAV_CLASS);
			Add(selectButton);

			equipmentInventory = ScriptableObject.CreateInstance<DollEquipmentInventory>();
			equipmentInventory.name = "DollEquipmentInventory(runtime)";
			equipmentGrid.Bind(equipmentInventory);

			style.display = DisplayStyle.None;
		}

		private VisualElement BuildSection(string label)
		{
			VisualElement section = new();
			section.AddToClassList(USS_SECTION);

			Label sectionLabel = new(label);
			sectionLabel.AddToClassList(USS_SECTION_LABEL);
			section.Add(sectionLabel);

			return section;
		}

		private VisualElement BuildSignatureSection(out VisualElement slotElement, out VisualElement iconElement, out Label nameElement)
		{
			VisualElement section = BuildSection("고유 장비");

			VisualElement slot = new();
			slot.AddToClassList(Slot.USS_CLASS);
			slot.AddToClassList(USS_SIGNATURE_SLOT);

			VisualElement icon = new();
			icon.AddToClassList(Slot.ICON_CLASS);
			icon.pickingMode = PickingMode.Ignore;
			slot.Add(icon);

			VisualElement row = new();
			row.AddToClassList(USS_SIGNATURE_ROW);
			row.Add(slot);

			Label name = new();
			name.AddToClassList(USS_SIGNATURE_NAME);
			name.pickingMode = PickingMode.Ignore;
			row.Add(name);

			section.Add(row);

			slotElement = slot;
			iconElement = icon;
			nameElement = name;
			return section;
		}

		public void Bind(Doll newDoll)
		{
			doll = newDoll;

			if (doll == null)
			{
				style.display = DisplayStyle.None;
				equipmentInventory.BindDoll(null);
				return;
			}

			style.display = DisplayStyle.Flex;
			equipmentInventory.BindDoll(doll);
			equipmentGrid.Bind(equipmentInventory);
			Refresh();
		}

		public void Refresh()
		{
			if (doll == null)
				return;

			nameLabel.text = doll.Name ?? "?";
			levelLabel.text = $"Lv.{doll.Level}  Exp.{doll.Exp}";

			EquipmentData signature = doll.SignatureEquipment;
			if (signature != null)
			{
				if (signature.Sprite != null)
					signatureIcon.style.backgroundImage = new StyleBackground(signature.Sprite);
				else
					signatureIcon.style.backgroundImage = StyleKeyword.None;
				signatureName.text = signature.Name;
			}
			else
			{
				signatureIcon.style.backgroundImage = StyleKeyword.None;
				signatureName.text = "없음";
			}

			selectButton.style.display = doll.IsControllable ? DisplayStyle.Flex : DisplayStyle.None;
			selectButton.SetEnabled(dataManager.CurDollID != doll.ID && DungeonManagerBridge.IsDungeon == false);
		}

		private void OnSelectClicked()
		{
			// 던전 중 전환은 스탯과 장비를 초기화
			if (doll == null || doll.IsControllable == false || DungeonManagerBridge.IsDungeon)
				return;

			dataManager.SetCurDoll(doll.ID);
			playerProvider.CurrentObject.SetDoll(doll.ID);
			Refresh();
		}
	}
}

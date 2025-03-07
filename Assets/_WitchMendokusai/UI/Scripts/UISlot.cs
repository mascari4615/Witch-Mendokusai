using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using UnityEngine.EventSystems;

namespace WitchMendokusai
{
	public class SlotData
	{
		public DataSO DataSO { get; private set; }= null;
		public Sprite Sprite{ get; private set; } = null;
		public string Name { get; private set; }= "";
		public string Description { get; private set; }= "";

		public bool IsEmpty { get; private set; }

		public void SetData(Sprite sprite, string name, string description)
		{
			if (sprite != null || string.IsNullOrEmpty(name) == false)
			{
				DataSO = null;
				Sprite = sprite;
				Name = name;
				Description = description;

				IsEmpty = false;
			}
			else
			{
				Init();
			}
		}

		public void SetData(DataSO dataSO)
		{
			if (dataSO)
			{
				DataSO = dataSO;
				Sprite = dataSO.Sprite;
				Name = dataSO.Name;
				Description = dataSO.Description;

				IsEmpty = false;
			}
			else
			{
				Init();
			}
		}

		public void Init()
		{
			DataSO = null;
			Sprite = null;
			Name = "";
			Description = "";

			IsEmpty = true;
		}
	}

	public class UISlot : MonoBehaviour, IUI, ISelectHandler, IDeselectHandler
	{
		public int Index { get; private set; }
		public ToolTipTrigger ToolTipTrigger { get; private set; }
		public SlotData Data { get; private set; }
		public int Amount { get; private set; }
		public bool IsDisable { get; private set; } = false;

		[SerializeField] protected DataSO defaultDataSO;
		[SerializeField] private bool blockClickWhenDisable = false;
		[SerializeField] private bool showAmountOne = false;

		protected Button button;
		public Selectable Selectable => button;
		protected Image iconImage;
		protected TextMeshProUGUI nameText;
		protected TextMeshProUGUI amountText;
		protected TextMeshProUGUI descriptionText;
		protected Image disableImage;

		private Action<UISlot> selectAction;
		private Action<UISlot> deselectAction;
		private Action<UISlot> clickAction;

		private bool isInit = false;

		public DataSO DataSO => Data.DataSO;

		public virtual bool Init()
		{
			if (isInit)
				return false;
			isInit = true;

			button = GetComponent<Button>();
			if (button)
				button.onClick.AddListener(OnClick);

			iconImage = transform.Find("[Image] IconBackground").Find("[Image] Icon").GetComponent<Image>();
			disableImage = transform.Find("[Image] Disable").GetComponent<Image>();

			nameText = transform.Find("[Text] Name").GetComponent<TextMeshProUGUI>();
			amountText = transform.Find("[Text] Amount").GetComponent<TextMeshProUGUI>();
			descriptionText = transform.Find("[Text] Description").GetComponent<TextMeshProUGUI>();

			ToolTipTrigger = GetComponent<ToolTipTrigger>();

			Data = new();

			if (defaultDataSO != null)
				SetSlot(defaultDataSO);

			return true;
		}

		public void SetSlotIndex(int index) => Index = index;

		public void SetSlot(DataSO dataSO, int amount = 1)
			=> SetSlot_(() => Data.SetData(dataSO), amount);

		public void SetSlot(Sprite sprite, string name, string description, int amount = 1)
			=> SetSlot_(() => Data.SetData(sprite, name, description), amount);

		private void SetSlot_(Action action, int amount = 1)
		{
			Init();

			action?.Invoke();
			Amount = amount;

			if (ToolTipTrigger)
				ToolTipTrigger.SetToolTipContent(Data);

			UpdateUI();
		}

		public virtual void UpdateUI()
		{
			if (!isInit)
				Init();

			iconImage.sprite = Data.Sprite;
			iconImage.color = Data.Sprite == null ? Color.clear : Color.white;
			nameText.text = Data.Name;
			descriptionText.text = Data.Description;

			amountText.text = (Amount == 1 && showAmountOne == false) ? "" : Amount.ToString();
		}

		public void SetDisable(bool isDisable)
		{
			IsDisable = isDisable;
			disableImage.gameObject.SetActive(isDisable);

			if (blockClickWhenDisable)
			{
				if (button)
					button.interactable = !isDisable;
			}
		}

		public void SetSelectAction(Action<UISlot> action) => selectAction = action;
		public void SetDeselectAction(Action<UISlot> action) => deselectAction = action;
		public void SetClickAction(Action<UISlot> action) => clickAction = action;

		public void Select()
		{
			// Debug.Log($"{name} has button? {button != null}");

			if (button)
				button.Select();
			else
				OnSelect(null);
		}

		public void OnSelect(BaseEventData eventData)
		{
			// Debug.Log($"{name} is selected");
			selectAction?.Invoke(this);
			if (ToolTipTrigger)
				ToolTipTrigger.Trigger();
		}

		public void OnDeselect(BaseEventData eventData)
		{
			// Debug.Log($"{name} is deselected");
			deselectAction?.Invoke(this);
		}

		public void OnClick()
		{
			// Debug.Log($"{name} is clicked");
			clickAction?.Invoke(this);
			if (ToolTipTrigger)
				ToolTipTrigger.Trigger();
		}

		public void SetNavigation(Navigation navigation)
		{
			if (button)
				button.navigation = navigation;
		}
	}
}
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
		public DataSO DataSO { get; private set; } = null;
		public Sprite Sprite { get; private set; } = null;
		public string Name { get; private set; } = "";
		public string Description { get; private set; } = "";
		public int Amount { get; private set; } = 1;

		public bool IsEmpty { get; private set; }

		public void SetData(Sprite sprite, string name, string description, int amount = 1)
		{
			if (sprite != null || string.IsNullOrEmpty(name) == false)
			{
				DataSO = null;
				Sprite = sprite;
				Name = name;
				Description = description;
				Amount = amount;

				IsEmpty = false;
			}
			else
			{
				Init();
			}
		}

		public void SetData(DataSO dataSO, int amount = 1)
		{
			if (dataSO != null)
			{
				DataSO = dataSO;
				Sprite = dataSO.Sprite;
				Name = dataSO.Name;
				Description = dataSO.Description;
				Amount = amount;

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
			Amount = 1;

			IsEmpty = true;
		}
	}

	public class UISlot : UIBase, ISelectHandler, IDeselectHandler
	{
		public int Index { get; private set; } = -1;
		public ToolTipTrigger ToolTipTrigger { get; private set; } = null;
		public SlotData Data { get; private set; } = new();
		public bool IsDisable { get; private set; } = false;

		[SerializeField] protected DataSO defaultDataSO = null;
		protected Button button = null;
		protected Image iconImage = null;
		protected TextMeshProUGUI nameText = null;
		protected TextMeshProUGUI amountText = null;
		protected TextMeshProUGUI descriptionText = null;
		protected Image disableImage = null;

		[SerializeField] private bool blockClickWhenDisable = false;
		[SerializeField] private bool showAmountOne = false;
		private Action<UISlot> selectAction = delegate { };
		private Action<UISlot> deselectAction = delegate { };
		private Action<UISlot> clickAction = delegate { };

		public Button Button => button;
		public Selectable Selectable => button;
		public DataSO DataSO => Data.DataSO;
		private bool IsInit => iconImage != null;

		public override void Init()
		{
			if (TryGetComponent(out Button button))
			{
				button.onClick.AddListener(OnClick);
				this.button = button;
			}

			iconImage = transform.Find("[Image] IconBackground")?.Find("[Image] Icon")?.GetComponent<Image>();
			disableImage = transform.Find("[Image] Disable")?.GetComponent<Image>();

			nameText = transform.Find("[Text] Name")?.GetComponent<TextMeshProUGUI>();
			amountText = transform.Find("[Text] Amount")?.GetComponent<TextMeshProUGUI>();
			descriptionText = transform.Find("[Text] Description")?.GetComponent<TextMeshProUGUI>();

			ToolTipTrigger = GetComponent<ToolTipTrigger>();

			if (defaultDataSO != null)
			{
				SetSlot(defaultDataSO);
			}
		}

		public void SetSlotIndex(int index) => Index = index;

		public void SetSlot(DataSO dataSO, int amount = 1)
			=> SetSlot_(() => Data.SetData(dataSO, amount));

		public void SetSlot(Sprite sprite, string name, string description, int amount = 1)
			=> SetSlot_(() => Data.SetData(sprite, name, description, amount));

		private void SetSlot_(Action action)
		{
			// Init();

			action?.Invoke();

			if (ToolTipTrigger != null)
			{
				ToolTipTrigger.SetToolTipContent(Data);
			}

			if (IsInit == true)
			{
				UpdateUI();
			}
		}

		public override void UpdateUI()
		{
			if (iconImage != null)
			{
				iconImage.sprite = Data.Sprite;
				iconImage.color = Data.Sprite == null ? Color.clear : Color.white;
			}
			if (nameText != null)
				nameText.text = Data.Name;
			if (descriptionText != null)
				descriptionText.text = Data.Description;
			if (amountText != null)
				amountText.text = (Data.Amount == 1 && showAmountOne == false) ? "" : Data.Amount.ToString();
		}

		public void SetDisable(bool isDisable)
		{
			IsDisable = isDisable;
			if (disableImage != null)
				disableImage.gameObject.SetActive(isDisable);

			if (blockClickWhenDisable)
			{
				if (button != null)
				{
					button.interactable = !isDisable;
				}
			}
		}

		public void SetSelectAction(Action<UISlot> action) => selectAction = action;
		public void SetDeselectAction(Action<UISlot> action) => deselectAction = action;
		public void SetClickAction(Action<UISlot> action) => clickAction = action;

		public void Select()
		{
			// Debug.Log($"{name} has button? {button != null}");

			if (button != null)
			{
				button.Select();
			}
			else
			{
				OnSelect(null);
			}
		}

		public void OnSelect(BaseEventData eventData)
		{
			// Debug.Log($"{name} is selected");
			selectAction?.Invoke(this);
			if (ToolTipTrigger != null)
			{
				ToolTipTrigger.Trigger();
			}
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
			if (ToolTipTrigger != null)
			{
				ToolTipTrigger.Trigger();
			}
		}

		public void SetNavigation(Navigation navigation)
		{
			if (button != null)
				button.navigation = navigation;
		}
	}
}
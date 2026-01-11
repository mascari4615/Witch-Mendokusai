using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public interface IUIContentBase
	{
		bool IsPanelOpen { get; }
		void Init();
		void ClosePanel();
	}

	public abstract class UIContentBase<T> : MonoBehaviour, IUIContentBase where T : Enum
	{
		public T CurPanelType { get; private set; } = default;
		public abstract T DefaultPanel { get; }
		public Dictionary<T, UIPanel> Panels { get; private set; } = new();

		public event Action<T> OnPanelChanged;

		public bool IsPanelOpen => CurPanelType.Equals(DefaultPanel) == false;

		// public PanelType CurPanel => PanelStack.Count > 0 ? PanelStack.Peek() : PanelType.None;
		// public Stack<PanelType> PanelStack { get; private set; } = new();

		protected virtual void Awake()
		{
			Init();
		}

		public abstract void Init();

		protected virtual void Start()
		{
			UIManager.Instance.RegisterOverlayUI(this);

			foreach (UIPanel uiPanel in Panels.Values)
			{
				uiPanel.Init(this);
				uiPanel.SetActive(false);
			}

			SetPanel(DefaultPanel);
		}

		public void SetPanel(T newPanelType, NPCObject npcObject = null)
		{
			if (CurPanelType.Equals(newPanelType))
				return;

			if (Panels.TryGetValue(CurPanelType, out UIPanel oldPanel))
			{
				oldPanel.SetActive(false);
			}

			CurPanelType = newPanelType;

			if (Panels.TryGetValue(newPanelType, out UIPanel newPanel))
			{
				newPanel.SetNPC(npcObject);
				newPanel.SetActive(true);
				newPanel.UpdateUI();
			}

			OnPanelChanged?.Invoke(newPanelType);
		}

		public void ClosePanel()
		{
			SetPanel(DefaultPanel);
		}
	}
}
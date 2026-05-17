using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public interface IUIPanelGroup
	{
		bool TryGetCurPanel(out IUIPanel panel);
		bool CanBeClosedByCancelInput { get; }
		bool IsPanelOpen { get; }
		void Init();
		void ClosePanel();
	}

	public abstract class UIPanelGroup<T> : MonoBehaviour, IUIPanelGroup where T : Enum
	{
		public event Action<T> OnPanelChanged;

		public T CurPanelType { get; private set; } = default;
		public abstract T DefaultPanel { get; }
		public Dictionary<T, IUIPanel> Panels { get; private set; } = new();

		public abstract bool CanBeClosedByCancelInput { get; }
		public bool TryGetCurPanel(out IUIPanel panel) => Panels.TryGetValue(CurPanelType, out panel);
		public bool IsPanelOpen => CurPanelType.Equals(DefaultPanel) == false;

		private UIManager uiManager;

		// VContainer source generator 는 제네릭 클래스의 [Inject] member 를 생성 못 함 (VCON0010, #3a abstract-base 동류).
		// 구체 서브클래스 (UINPC/UIDungeon/CardManager) 가 [Inject] Construct 에 UIManager 받아 SetUIManager 호출.
		protected void SetUIManager(UIManager uiManager)
		{
			this.uiManager = uiManager;
		}

		protected virtual void Awake()
		{
			Init();
		}

		public abstract void Init();

		protected virtual void Start()
		{
			uiManager.RegisterOverlayUI(this);

			foreach (IUIPanel uiPanel in Panels.Values)
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

			if (Panels.TryGetValue(CurPanelType, out IUIPanel oldPanel))
			{
				oldPanel.SetActive(false);
			}

			CurPanelType = newPanelType;

			if (Panels.TryGetValue(newPanelType, out IUIPanel newPanel))
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
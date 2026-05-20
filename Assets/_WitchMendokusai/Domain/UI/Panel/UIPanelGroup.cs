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

		// VContainer source generator 는 *제네릭* 클래스의 injector 를 안 만듦 — 소스 인증된
		// 진짜 한계 (Emitter.cs:44-47 `if (typeMeta.IsGenerics) return false`, 진단 VCON0010
		// GenericsNotSupported). ※ abstract-base 와 동류 아님 — abstract 서브클래스는 base
		// [Inject] 가 자식 injector 에 *포함*된다 (TASK-WM-109-A, DI/VCONTAINER-MECHANISM.md
		// §1·§4①). 제네릭만 별개 한계라 UIPanelGroup<T> 의 비제네릭 구체 서브클래스
		// (UINPC/UIDungeon/CardManager) 가 [Inject] Construct 에 UIManager 받아 SetUIManager 호출.
		protected void SetUIManager(UIManager uiManager)
		{
			this.uiManager = uiManager;
		}

		// Toolkit 패널 생성용 (Init 시점 = Construct 후이므로 uiManager 보장). TASK-WM-113 S2.
		protected UIManager UIManager => uiManager;

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
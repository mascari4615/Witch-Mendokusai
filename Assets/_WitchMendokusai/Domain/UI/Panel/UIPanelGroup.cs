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

		public abstract void Init();

		/// <summary>
		/// ★ <c>Init</c> 은 예전에 <c>Awake</c> 에서 불렸다. 그런데 하위 묶음들의 <c>Init</c> 은
		///   **씬을 훑어 자기 창들을 찾아 쥔다.** 유니티는 누가 먼저 깨어나는지 보장하지 않으므로,
		///   상대가 아직 안 깨어났으면 그 손잡이는 **영영 빈 채로 굳는다** — 그리고 아무것도 안 터진다.
		///   그 창만 조용히 안 열릴 뿐이라 원인을 찾기가 아주 어렵다 (TASK-WM-212).
		///
		///   지금 자리(<c>Start</c> 맨 앞)는 **모든 깨어나기가 끝난 뒤**라 찾을 것이 반드시 있다.
		///   순서는 그대로다 — 아래 줄들이 이미 <c>Panels</c> 를 읽고 있었고, 그 전에 채워진다.
		///   (<c>Panels</c> 를 깨어날 때 읽는 코드는 없다. 전수 확인 2026-08-08.)
		/// </summary>
		protected virtual void Start()
		{
			Init();

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
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace WitchMendokusai
{
	/// <summary>
	/// UI Toolkit 기반 NPC 패널 base — 레거시 uGUI UIPanel 과 동일 IUIPanel 계약 구현.
	/// UIPanelGroup 이 uGUI/Toolkit 패널을 동형 관리 (TASK-WM-113 S2 substrate).
	/// 차이: GameObject.SetActive/Animator 대신 UIRoot.ScreenLayer 의 VisualElement
	/// display 토글 (SettingView 선례). 런타임 AddComponent + container.Inject 로 생성
	/// (UIManager.CreateToolkitPanel) — 씬 prefab 미배치, init-order 안전.
	/// (UIPanelGroup 와 동일 폴더 = 논리적 홈. 구 Domain/UI/ 경로는 Unity 신파일
	/// import 스턱으로 이전 — TASK-WM-113 S2.)
	/// </summary>
	public abstract class UIToolkitPanel : MonoBehaviour, IUIPanel
	{
		public abstract string Name { get; }
		public virtual Sprite PanelIcon => null;
		public virtual bool IsFullscreen => true;

		private UIRoot uiRoot;
		protected IUIPanelGroup PanelGroup { get; private set; }
		protected VisualElement Root { get; private set; }
		private bool isBuilt;

		[Inject]
		public void Construct(UIRoot uiRoot)
		{
			this.uiRoot = uiRoot;
		}

		public void Init(IUIPanelGroup group)
		{
			PanelGroup = group;
			EnsureBuilt();
			OnInit();
		}

		private void EnsureBuilt()
		{
			if (isBuilt)
				return;
			if (uiRoot == null || uiRoot.ScreenLayer == null)
				return;

			Root = new VisualElement { name = GetType().Name };
			Root.style.position = Position.Absolute;
			Root.style.display = DisplayStyle.None;
			uiRoot.ScreenLayer.Add(Root);

			BuildUI(Root);
			isBuilt = true;
		}

		/// <summary>VisualElement 트리 구축 (1회). 서브클래스가 패널 내용 작성.</summary>
		protected abstract void BuildUI(VisualElement root);

		/// <summary>UIPanelGroup.Init 시점 훅 (PanelGroup 설정 후).</summary>
		protected virtual void OnInit() { }

		public void SetActive(bool newActive)
		{
			EnsureBuilt();
			if (Root == null)
				return;

			Root.style.display = newActive ? DisplayStyle.Flex : DisplayStyle.None;

			if (newActive)
				OnOpen();
			else
				OnClose();
		}

		protected virtual void OnOpen() { }
		protected virtual void OnClose() { }

		public virtual void SetNPC(NPCObject npc) { }

		public abstract void UpdateUI();
	}
}

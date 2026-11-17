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

		// `[Inject]` 메서드는 base+자식 합쳐 1개만 코드생성된다 — 자식(UIDungeonEntranceToolkit)이 자기
		// Construct 를 가지므로 base 는 필드 주입을 쓴다(필드는 개수 제한 없음). private 이면 같은 dll 에서
		// set 불가로 또 폴백(VCON0007) → internal. 정본: Domain/Application/DI/VCONTAINER-MECHANISM.md §3·§6
		[Inject] internal UIRoot uiRoot;
		protected IUIPanelGroup PanelGroup { get; private set; }
		protected VisualElement Root { get; private set; }
		private bool isBuilt;
		[Tooltip("이 패널만의 평상시 효과. 비우면 UIFxSettings.idleEffect")]
		[SerializeField] private UIIdleEffect idleEffect;

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

			// ★ 화면 층 패널은 *화면을 채운다*(사용자 지시: "전체화면으로 안보이고 좁은 화면으로 필요한
			//   공간만 쓰는데 좀 고쳐주세요"). 절대 배치만 걸고 네 변을 안 잡으면 요소가 *내용 크기*로
			//   줄어든다 — 안에서 flexGrow 를 아무리 줘도 부모가 작으면 늘어날 자리가 없다.
			//   네 변을 0 으로 못 박아야 안쪽 레이아웃(좌측 목록 + 우측 상세)이 의도대로 펼쳐진다.
			Root.style.left = 0;
			Root.style.right = 0;
			Root.style.top = 0;
			Root.style.bottom = 0;

			Root.AddToClassList(UIMotion.RISE);
			UIMotion.SetInstant(Root, false);
			uiRoot.ScreenLayer.Add(Root);

			BuildUI(Root);
			if (UIFxSettings.Instance.ambientMotes)
				UISparkle.AttachMotes(Root);
			UIIdleFx.Attach(Root, () => idleEffect != null ? idleEffect : UIFxSettings.Instance.idleEffect);
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

			if (newActive)
			{
				if (Root.ClassListContains(UIMotion.IN) == false)
					UIMotion.UseStyle(Root, UIFxSettings.Instance.panelMotion);
				UIMotion.Show(Root);
				// 패널 몸통 하나 아래에 실제 칸들이 있으면 그 칸들을 순서대로 (입자와 효과 층은 세지 않음)
				VisualElement body = SingleBody(Root);
				VisualElement staggerRoot = body != null && body.childCount > 1 ? body : Root;
				UIShine.StaggerChildren(staggerRoot);
			}
			else
				UIMotion.Hide(Root);

			if (newActive)
				OnOpen();
			else
				OnClose();
		}

		// 효과 층을 뺀 자식이 하나면 그 자식
		private static VisualElement SingleBody(VisualElement root)
		{
			VisualElement body = null;
			foreach (VisualElement child in root.Children())
			{
				if (child.ClassListContains(UIIdleFx.LAYER_CLASS))
					continue;
				if (body != null)
					return null;
				body = child;
			}
			return body;
		}

		protected virtual void OnOpen() { }
		protected virtual void OnClose() { }

		public virtual void SetNPC(NPCObject npc) { }

		public abstract void UpdateUI();
	}
}

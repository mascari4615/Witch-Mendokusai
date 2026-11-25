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
		/// <summary> 화면 끝까지 덮는 틀. 배경, 배경 그림, 테마 USS 클래스는 여기 </summary>
		protected VisualElement Root { get; private set; }
		/// <summary> 안전 영역 칸. BuildUI 가 받는 곳. 버튼과 글은 여기 </summary>
		protected VisualElement Content { get; private set; }
		private bool isBuilt;
		[Tooltip("이 패널의 연출 묶음. 비우면 UIFxSettings.panelProfile")]
		[SerializeField] private UIFxProfile profile;

		protected UIFxProfile Profile => profile != null ? profile : UIFxSettings.Instance.panelProfile;

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

			// 화면 층 패널은 화면을 채운다 (사용자 지시: 좁은 화면 말고 전체 화면). 배경은 노치 쪽 끝까지, 조작은 안전 영역
			FullScreenFrame frame = uiRoot.CreateFullScreenFrame(uiRoot.ScreenLayer, GetType().Name);
			Root = frame.Root;
			Content = frame.Content;
			Root.AddToClassList(UIMotion.RISE);
			UIMotion.SetInstant(Root, false);

			BuildUI(Content);
			UIFxProfile buildProfile = Profile;
			bool hasProfile = buildProfile != null;
			if (hasProfile && buildProfile.ambience != null)
				buildProfile.ambience.Attach(Root);
			else if (UIFxSettings.Instance.ambientMotes)
				UISparkle.AttachMotes(Root);
			if (hasProfile && buildProfile.parallax != null)
				buildProfile.parallax.Attach(Root);
			UIIdleFx.Attach(Root, () => Profile != null ? Profile.idleEffect : null);
			UIFocusFx.Attach(Root, () => Profile);
			isBuilt = true;
		}

		/// <summary>VisualElement 트리 구축 (1회). content 는 안전 영역 칸. 화면 끝까지 덮을 배경은 AddBackdrop</summary>
		protected abstract void BuildUI(VisualElement content);

		/// <summary> 배경 요소를 화면 끝까지 덮는 틀에, 내용 칸보다 뒤에 둠 </summary>
		protected void AddBackdrop(VisualElement element)
		{
			Root.Insert(Root.IndexOf(Content), element);
		}

		/// <summary>UIPanelGroup.Init 시점 훅 (PanelGroup 설정 후).</summary>
		protected virtual void OnInit() { }

		public void SetActive(bool newActive)
		{
			EnsureBuilt();
			if (Root == null)
				return;

			UIFxProfile current = Profile;
			UIPanelMotion motion = current != null ? current.panelMotion : null;
			UIBackdrop backdrop = current != null ? current.backdrop : null;
			if (newActive)
			{
				// 배경이 있으면 화면을 찍은 다음 프레임에 열어 찍힌 화면에 패널이 안 들어가게
				if (backdrop != null)
					backdrop.Show(Root, () => Open(motion));
				else
					Open(motion);
			}
			else
			{
				UIFocusFx.Reset(Root);
				if (backdrop != null)
					backdrop.Hide(Root);
				if (motion != null)
					motion.Play(Root, false, null);
				else
					UIMotion.Hide(Root);
			}

			if (newActive)
				OnOpen();
			else
				OnClose();
		}

		private void Open(UIPanelMotion motion)
		{
			if (motion != null)
				motion.Play(Root, true, null);
			else
			{
				if (Root.ClassListContains(UIMotion.IN) == false)
					UIMotion.UseStyle(Root, UIFxSettings.Instance.panelMotion);
				UIMotion.Show(Root);
			}
			// 패널 몸통 하나 아래에 실제 칸들이 있으면 그 칸들을 순서대로 (입자와 효과 층은 세지 않음)
			// 칸 세기는 안전 영역 칸 기준. 화면 끝 틀에는 배경 요소가 섞임
			VisualElement body = SingleBody(Content);
			VisualElement staggerRoot = body != null && body.childCount > 1 ? body : Content;
			UIShine.StaggerChildren(staggerRoot);
			UIFxProfile current = Profile;
			if (current != null && current.textFx != null)
				current.textFx.ApplyAll(Root);
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

		// 월드보다 먼저 치우는 화면 묶음이 부름. 틀이 화면 층에 남지 않게
		public void Detach() => Root?.RemoveFromHierarchy();

		protected virtual void OnOpen() { }
		protected virtual void OnClose() { }

		public virtual void SetNPC(NPCObject npc) { }

		public abstract void UpdateUI();
	}
}

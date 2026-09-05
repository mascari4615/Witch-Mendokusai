using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 손가락으로 본편을 조작하는 화면 장치 — 왼쪽 스틱 · 오른쪽 훑기(시점) · 버튼 (TASK-WM-200).
	///
	/// ★ 왜 UI Toolkit 인가: 손가락 조작은 *여러 손가락이 동시에* 일어난다(왼손은 걷고 오른손은 본다).
	///   화면 좌표를 직접 읽어 처리하면 「지금 이 좌표가 스틱 손가락인가 시점 손가락인가」를 매 프레임
	///   다시 맞춰야 하고, 손가락이 하나 늘 때마다 어긋난다. UI Toolkit 은 요소마다 손가락을 *붙잡아
	///   두는*(capture) 구조라 그 문제가 구조적으로 사라진다 — 스틱을 잡은 손가락은 화면 어디로 가든
	///   계속 스틱 것이다.
	///
	/// ★ 왜 값이 SO 로 가는가: 조이스틱 값의 주인은 예전부터 SO 하나였고(GameManager 가 그렇게 묶어
	///   뒀다), 화면 조작은 그 자리에 값을 꽂을 뿐이다. 여기서 자기 값을 따로 들면 「스틱은 미는데
	///   캐릭터는 안 간다」가 조용히 생긴다.
	///
	/// ★ 층: 시점 훑기 판은 HUD 층의 **맨 아래**에 깔린다 — 핫바·버튼이 그 위에 있어야 버튼을 누를 때
	///   시점이 같이 돌지 않는다. 위아래를 뒤집으면 화면의 모든 버튼이 죽는다.
	/// </summary>
	public partial class MobileControlsView : MonoBehaviour
	{
		[Tooltip("화면 가장자리에서 조작 장치까지 띄우는 여백(픽셀).")]
		[SerializeField, Min(0f)] private float edgeMargin = 36f;

		[Tooltip("이만큼(픽셀) 안에서 뗐을 때만 「눌렀다」로 본다 — 서랍을 굴리려던 손가락과 구별하는 문턱.")]
		[SerializeField, Min(1f)] private float tapSlopPixels = 16f;
		private Rect lastSafeArea;
		private int lastScreenWidth;
		private int lastScreenHeight;
		private VisualElement controlsRoot;

		private bool built;

		private void OnEnable()
		{
			Build();
			LookBridge.GetDelta = ConsumeLookDelta;
		}

		private void OnDisable()
		{
			LookBridge.GetDelta = () => Vector2.zero;
			ReleaseStick();
			if (lookBackdrop != null)
				lookBackdrop.RemoveFromHierarchy();
			if (controlsRoot != null)
				controlsRoot.RemoveFromHierarchy();
			// 안내는 층에 따로 붙어 있어서 같이 안 걷으면 조작 장치가 사라진 화면에 글자만 남는다.
			if (overheadHint != null)
				overheadHint.RemoveFromHierarchy();
			if (arenaExitButton != null)
				arenaExitButton.RemoveFromHierarchy();
			built = false;
		}

		private void Update()
		{
			// ★ 화면 층은 다시 만들어질 수 있다 (UIRoot 가 켜질 때마다 층을 새로 세운다).
			//   그러면 이 조작 장치는 *옛 층에 붙은 채* 화면에서 사라지는데, 스스로는 「만들었다」고
			//   믿고 있어서 영영 안 돌아온다 — 폰에서 갑자기 못 움직이게 되는 종류의 결함이다.
			//   붙어 있는지 매 프레임 확인하고, 떨어졌으면 다시 붙는다(조용히 없어지는 것보다 낫다).
			if (built && IsAttached() == false)
				built = false;

			// ★ 화면 크기·가장자리 여백은 *한 번만* 재고 끝이었다. 폰을 180도 돌리면(이 게임은 가로
			//   두 방향을 다 허용한다) 노치가 반대편으로 가는데 층은 그대로라, 버튼이 카메라 구멍
			//   아래로 들어간다. 화면 모양이 달라지면 다시 세운다.
			Rect safeArea = Screen.safeArea;
			if (built && (safeArea != lastSafeArea || Screen.width != lastScreenWidth || Screen.height != lastScreenHeight))
				built = false;
			lastSafeArea = safeArea;
			lastScreenWidth = Screen.width;
			lastScreenHeight = Screen.height;

			if (built == false)
			{
				Build();
				return;
			}

			// 「지금 손가락인가」는 입력 관리자 한 곳이 정한다 — 화면이 따로 판단하면 둘이 어긋난다.
			bool show = InputManager.TryGetExistingInstance(out InputManager inputManager) && inputManager.IsTouchMode;

			// ★ 걸을 캐릭터가 없으면 조작 장치도 없다 (실측: 제목 화면에도 스틱이 떴다).
			//   더 나쁜 건 시점 훑기 판이 *화면 전체를 덮는다*는 것이다 — 제목 화면에서 그게 켜져 있으면
			//   폰에서는 「시작」 조차 못 누른다. 무엇을 조작할 대상이 있을 때만 조작 장치가 뜬다.
			PlayerProvider playerProvider = PlayerProvider.TryGetExistingInstance(out PlayerProvider found) ? found : null;
			show = show && playerProvider != null && playerProvider.HasPlayer;

			// ★ 게임 속 게임(개척·투기장·건설)은 자기 조작을 따로 쥔다 — 캐릭터를 걷게 하는 스틱이
			//   거기 떠 있으면 거짓말이다(그 모드엔 캐릭터가 없다).
			show = show && (GameModeManager.TryGetExistingInstance(out GameModeManager gameModeManager) == false
				|| gameModeManager.CurrentMode == GameMode.Default);
			DisplayStyle display = show ? DisplayStyle.Flex : DisplayStyle.None;
			if (lookBackdrop.style.display != display)
			{
				lookBackdrop.style.display = display;
				controlsRoot.style.display = display;
				if (show == false)
				{
					ReleaseStick();
					ReleaseAllHeld();
				}
			}

			PushStickValue();
			InputManager touch = InputManager.TryGetExistingInstance(out InputManager held) ? held : null;
			UpdateOverheadHint(show, touch);
			UpdateArenaExitButton(touch);
			UpdateInteractButton(show, playerProvider);
			UpdateLookBackdropBlocking();
			// 화면을 돌렸을 때의 처리는 위(다시 세우기) 한 곳뿐이다. 여기에 「여백만 다시 재기」를
			// 따로 두면 위에서 이미 값을 갱신한 뒤라 *영영 안 불리는 죽은 가지*가 된다.
		}

		/// <summary> 지금 화면의 *현재* HUD 층에 붙어 있나 — 층이 새로 세워졌으면 거짓이 된다. </summary>
		private bool IsAttached()
		{
			if (lookBackdrop == null || controlsRoot == null)
				return false;
			if (UIRoot.TryGetExistingInstance(out UIRoot uiRoot) == false || uiRoot.HudLayer == null)
				return false;
			return lookBackdrop.parent == uiRoot.HudLayer && controlsRoot.parent == uiRoot.HudLayer;
		}

		private void Build()
		{
			if (built)
				return;

			// 옛 층에 남아 있던 것은 떼고 다시 붙인다 — 안 그러면 유령이 겹겹이 쌓인다.
			lookBackdrop?.RemoveFromHierarchy();
			controlsRoot?.RemoveFromHierarchy();

			// ★ 잡고 있던 손가락 번호도 같이 버린다. 새 스틱은 그 손가락을 붙잡은 적이 없는데
			//   번호만 남아 있으면 「이미 누가 잡고 있다」고 오해해 *새 스틱이 죽은 채로 시작한다*
			//   (다시 붙이는 그 순간 손가락이 스틱 위에 있었으면 그대로 굳는다).
			stickPointerId = -1;
			stickValue = Vector2.zero;
			lookAccumulated = Vector2.zero;

			// ★ 「자리 옮기기」 중이던 기억도 같이 버린다. 층이 다시 세워지면 옮기기 줄과 노란 테두리는
			//   사라지는데 *모드만 남는다* — 그러면 스틱이 손가락을 안 받아 **캐릭터가 안 움직이고,
			//   화면엔 이유가 아무 데도 안 적힌다.** 무대를 옮기면 옮기기는 끝난 것으로 본다.
			layoutEditMode = false;
			movable.Clear();
			// 화면을 다시 짓기 전에 누른 채로 남은 것을 놓는다 — 옛 버튼은 사라져서 영영 못 놓는다.
			ReleaseAllHeld();
			dragTarget = null;
			dragPointerId = -1;
			if (UIRoot.TryGetExistingInstance(out UIRoot uiRoot) == false || uiRoot.HudLayer == null)
				return;

			BuildLookBackdrop(uiRoot);
			BuildControls(uiRoot);
			BuildOverheadHint(uiRoot);
			BuildArenaExitButton(uiRoot);
			built = true;
		}

		private const string OVERHEAD_HINT_SEEN_KEY = "WM.Mobile.OverheadHintSeen";

		private void BuildControls(UIRoot uiRoot)
		{
			controlsRoot = new VisualElement { name = "MobileControls" };
			controlsRoot.style.position = Position.Absolute;
			ApplySafeArea(controlsRoot);
			controlsRoot.style.display = DisplayStyle.None;
			controlsRoot.pickingMode = PickingMode.Ignore;

			BuildStick(controlsRoot);
			BuildActionButtons(controlsRoot);
			BuildWindowMenu(controlsRoot);

			uiRoot.HudLayer.Add(controlsRoot);
			RestoreMovedPositions();
		}
		private int dragPointerId = -1;
		private VisualElement dragTarget;
		private Vector2 dragStart;

		private static Vector2 ScreenSize => new Vector2(Screen.width, Screen.height);

		/// <summary>
		/// 손끝이 확실히 닿는 최소 크기(논리 픽셀)로 올려준다.
		///
		/// ★ 안드로이드·iOS 접근성 기준이 같은 말을 한다: 누를 수 있는 것은 **최소 48dp(약 9mm)**.
		///   우리 화면은 논리 픽셀이라 기기 밀도에 따라 실제 크기가 달라진다 — 조밀한 폰일수록
		///   같은 숫자가 더 작아진다. 그래서 숫자를 박지 않고 *실제 손가락 크기*로 환산해 바닥을 깐다.
		/// ★ 실측(2026-08-07, 400dpi 폰): 창 메뉴 작은 버튼이 약 5.7mm 였다 — 기준의 3분의 2.
		///   「눌렀는데 안 눌린다」의 흔한 정체가 이것이고, 사람은 그걸 버그로 신고하지 않는다.
		/// </summary>
		/// <summary>
		/// 조작 장치를 기기의 「안전 영역」 안으로 들인다.
		///
		/// ★ 노치·둥근 모서리·아래쪽 제스처 바는 *화면이지만 내 것이 아닌 자리*다. 거기 버튼을 두면
		///   눌리지 않거나, 눌렀는데 시스템이 먼저 가져간다(뒤로가기·홈으로 나감). 사용자는 이걸
		///   「가끔 안 눌림」으로만 겪고 원인을 못 짚는다.
		/// ★ 화면 표시기는 이미 같은 계산을 한다 — 조작 장치만 안 하고 있었다(2026-08-07 점검).
		/// ★ 화면을 돌리면 안전 영역이 통째로 바뀐다. 그래서 한 번이 아니라 바뀔 때마다 다시 잰다.
		/// </summary>
		private static void ApplySafeArea(VisualElement root)
		{
			Rect safeArea = Screen.safeArea;
			float scale = Screen.height > 0 ? PANEL_REFERENCE_HEIGHT / Screen.height : 1f;

			root.style.left = safeArea.xMin * scale;
			root.style.right = (Screen.width - safeArea.xMax) * scale;
			root.style.top = (Screen.height - safeArea.yMax) * scale;
			root.style.bottom = safeArea.yMin * scale;
		}

		/// <summary>패널이 기준으로 삼는 세로 크기 — 화면 픽셀을 이 화면의 좌표로 바꿀 때 쓴다.</summary>
		private const float PANEL_REFERENCE_HEIGHT = 800f;
	}
}

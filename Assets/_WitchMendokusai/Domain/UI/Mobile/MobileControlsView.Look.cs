using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// MobileControlsView 의 Look 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 MobileControlsView.cs 를 본다.
	public partial class MobileControlsView
	{
		[Tooltip("화면을 훑은 픽셀 → 시점 회전량 배율. 1 이면 마우스와 같은 감도.")]
		[SerializeField, Min(0.05f)] private float lookSensitivity = 1f;

		[Header("둘러보기 안내")]
		[Tooltip("처음 온 사람에게 보여줄 한 줄. 한 번 둘러보면 다시 안 뜬다.")]
		[SerializeField] private string lookHintText = "화면을 문지르면 둘러봅니다";
		[Tooltip("안내 글자 크기(픽셀).")]
		[SerializeField, Min(8f)] private float lookHintFontSize = 22f;
		[Tooltip("안내의 진하기. 게임 화면을 가리지 않을 만큼만.")]
		[SerializeField, Range(0f, 1f)] private float lookHintOpacity = 0.75f;
		[Tooltip("이만큼(픽셀) 문지르면 「알았다」로 보고 안내를 영영 내린다.")]
		[SerializeField, Min(1f)] private float lookHintDismissDistance = 60f;

		private VisualElement lookBackdrop;
		private Label lookHint;
		private Vector2 lookAccumulated;
		private float lookHintDragged;

		/// <summary>
		/// 창이 떠 있는 동안엔 시점 훑기 판이 손가락을 가로채지 않게 비킨다 (TASK-WM-200).
		///
		/// ★ 이 판은 화면 전체를 덮는다. 그 위에서 누른 손가락을 이 판이 먼저 붙잡으면, 그 아래 있는
		///   **대화 선택지·창 버튼이 통째로 안 눌린다** — 2026-08-07 실기에서 실제로 그랬다
		///   (버튼은 보이는데 눌러도 아무 일도 없었다).
		/// ★ 왜 「눌린 자리가 UI 위인가」만으로는 부족한가: 그 판정은 한 프레임 늦게 갱신돼서,
		///   *누르는 그 순간*엔 아직 「UI 아님」으로 보인다. 그래서 창이 열린 상태 자체로 막는다.
		/// ★ 창이 닫히면 원래대로 돌아온다 — 못 돌리면 그때부터 시점이 영영 안 돈다.
		/// </summary>
		private void UpdateLookBackdropBlocking()
		{
			if (lookBackdrop == null || layoutEditMode)
				return;

			// 제목·로비처럼 아직 아무도 등록 안 한 화면에서 그냥 물으면 *매 프레임* 널 참조로 터진다.
			bool uiHoldsInput = UIChat.IsChatting
				|| (GameConditionBridge.HasInstance
					&& GameConditionBridge.Get(GameConditionType.IsViewingUI));

			PickingMode wanted = uiHoldsInput ? PickingMode.Ignore : PickingMode.Position;
			if (lookBackdrop.pickingMode == wanted)
				return;

			lookBackdrop.pickingMode = wanted;
			if (uiHoldsInput)
				lookAccumulated = Vector2.zero; // 창이 뜨는 순간 남아 있던 훑기량으로 시점이 튀지 않게
		}

		/// <summary> 훑은 양을 넘기고 비운다 — 한 번 쓴 움직임을 다음 프레임에 또 쓰면 시점이 계속 흐른다. </summary>
		private Vector2 ConsumeLookDelta()
		{
			Vector2 delta = lookAccumulated * lookSensitivity;
			lookAccumulated = Vector2.zero;
			return delta;
		}

		private void BuildLookBackdrop(UIRoot uiRoot)
		{
			lookBackdrop = new VisualElement { name = "MobileLookArea" };
			lookBackdrop.style.position = Position.Absolute;
			lookBackdrop.style.left = 0;
			lookBackdrop.style.right = 0;
			lookBackdrop.style.top = 0;
			lookBackdrop.style.bottom = 0;
			lookBackdrop.style.display = DisplayStyle.None;
			// 훑기 판은 *잡을 수* 있어야 한다 — 다만 HUD 의 맨 아래라 버튼이 늘 이긴다.
			lookBackdrop.pickingMode = PickingMode.Position;

			lookBackdrop.RegisterCallback<PointerDownEvent>(OnLookDown);
			lookBackdrop.RegisterCallback<PointerMoveEvent>(OnLookMove);
			lookBackdrop.RegisterCallback<PointerUpEvent>(OnLookUp);

			BuildLookHint();
			uiRoot.HudLayer.Insert(0, lookBackdrop);
		}

		/// <summary>
		/// 「어디를 어떻게 해야 둘러보나」 한 줄 안내 (실기 실측 2026-08-06 — 사용자가 못 찾았다).
		///
		/// ★ 화면 전체가 이미 둘러보기 판이라 *기능은 멀쩡했다*. 없던 것은 「그렇다는 말」뿐이다.
		///   그래서 판을 바꾸지 않고 글자 한 줄만 얹는다 — 조작을 건드리면 멀쩡한 것이 깨진다.
		/// ★ 한 번 둘러보면 영영 안 뜬다. 아는 사람에게 계속 말 거는 안내는 방해일 뿐이다.
		/// ★ 손가락은 통과시킨다 — 안내가 떠 있는 동안 그 자리를 문지르면 안내 때문에 못 돌아가는,
		///   가르치려다 막는 상황이 된다.
		/// </summary>
		private void BuildLookHint()
		{
			if (LookHintSeen)
				return;

			lookHint = new Label(lookHintText) { name = "MobileLookHint" };
			lookHint.pickingMode = PickingMode.Ignore;
			lookHint.style.position = Position.Absolute;
			lookHint.style.left = 0;
			lookHint.style.right = 0;
			lookHint.style.top = Length.Percent(38f);
			lookHint.style.unityTextAlign = TextAnchor.MiddleCenter;
			lookHint.style.fontSize = lookHintFontSize;
			lookHint.style.opacity = lookHintOpacity;
			lookHint.style.color = Color.white;
			lookBackdrop.Add(lookHint);
		}

		private const string LOOK_HINT_SEEN_KEY = "WM.Mobile.LookHintSeen";

		private static bool LookHintSeen => PlayerPrefs.GetInt(LOOK_HINT_SEEN_KEY, 0) == 1;

		/// <summary>둘러본 적이 있으면 기록한다 — 기기에만 남기면 되는 것이라 저장 파일을 안 건드린다.</summary>
		private void DismissLookHint()
		{
			if (lookHint == null)
				return;
			lookHint.RemoveFromHierarchy();
			lookHint = null;
			PlayerPrefs.SetInt(LOOK_HINT_SEEN_KEY, 1);
			PlayerPrefs.Save();
		}

		private void OnLookDown(PointerDownEvent evt)
		{
			// ★ 다른 창(UGUI) 위에서 시작한 끌기는 시점이 아니다 — 창은 UI Toolkit 밖에 따로 있어서
			//   이 판이 그 아래에 깔려 있어도 손가락은 둘 다 닿는다. 창을 만지는 동안 시점이 돌면
			//   「무엇을 만지고 있는지」가 화면과 어긋난다.
			if (InputManager.TryGetExistingInstance(out InputManager inputManager) && inputManager.IsPointerOverUI())
				return;

			lookBackdrop.CapturePointer(evt.pointerId);
			evt.StopPropagation();
		}

		private void OnLookMove(PointerMoveEvent evt)
		{
			if (lookBackdrop.HasPointerCapture(evt.pointerId) == false)
				return;
			// 화면 좌표는 위가 0 이고 시점 회전은 아래가 0 이다 — 안 뒤집으면 위아래가 반대로 돈다.
			lookAccumulated += new Vector2(evt.deltaPosition.x, -evt.deltaPosition.y);

			// 살짝 스친 것으로 안내를 내리면 「읽기도 전에 사라졌다」가 된다 — 확실히 돌린 뒤에만.
			if (lookHint != null && evt.deltaPosition.magnitude > 0f)
			{
				lookHintDragged += evt.deltaPosition.magnitude;
				if (lookHintDragged >= lookHintDismissDistance)
					DismissLookHint();
			}
		}

		private void OnLookUp(PointerUpEvent evt)
		{
			if (lookBackdrop.HasPointerCapture(evt.pointerId))
				lookBackdrop.ReleasePointer(evt.pointerId);
		}
	}
}

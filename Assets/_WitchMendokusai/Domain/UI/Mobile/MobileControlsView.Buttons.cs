using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// MobileControlsView 의 Buttons 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 MobileControlsView.cs 를 본다.
	public partial class MobileControlsView
	{
		[Tooltip("동작 버튼 한 변(픽셀). 손끝이 뭉툭해서 데스크톱 버튼보다 커야 한다.")]
		[SerializeField, Min(44f)] private float actionButtonSize = 96f;
		[Tooltip("부감 시점(개척·도시)에서 뜨는 한 줄 — 두 손가락 조작은 화면에 안 보여서 말해 줘야 한다.")]
		[SerializeField] private string overheadHintText = "두 손가락으로 밀면 시점 이동 · 비틀면 회전 · 오므리면 확대";
		private Label overheadHint;
		private Label arenaExitButton;

		/// <summary>
		/// 말 걸 상대가 있을 때만 뜨는 버튼 (TASK-WM-200).
		///
		/// ★ 폰엔 Z 키가 없다 — 이게 없으면 NPC 대화가 *폰에서 통째로 없는 기능*이 된다.
		///   실기 실측(2026-08-06): 다가가도 아무 버튼이 안 떠 대화를 시작할 방법이 없었다.
		/// ★ 왜 「있을 때만」인가: 늘 떠 있으면 눌러도 아무 일 없는 버튼이 되어 거짓말을 한다.
		///   상대가 생겼을 때 나타나면 버튼 자체가 「지금 말 걸 수 있다」는 안내가 된다.
		/// ★ 판정은 세계 쪽 정본(`InteractiveObject.GetNearest`)에 그대로 묻는다 — 여기서 거리를
		///   따로 재면 「머리 위 표식은 떴는데 버튼은 없다」가 조용히 생긴다.
		/// </summary>
		private void UpdateInteractButton(bool controlsVisible, PlayerProvider playerProvider)
		{
			if (interactButton == null)
				return;

			bool hasTarget = controlsVisible
				&& playerProvider != null
				&& playerProvider.HasPlayer
				&& InteractiveObject.GetNearest(
					playerProvider.Current.transform.position,
					PlayerInteraction.InteractionDistance) != null;

			DisplayStyle display = hasTarget ? DisplayStyle.Flex : DisplayStyle.None;
			if (interactButton.style.display != display)
				interactButton.style.display = display;
		}

		/// <summary>
		/// 부감(개척·도시) 시점에서 「두 손가락」을 알려 주는 한 줄 (TASK-WM-200).
		///
		/// ★ 왜 따로 만드나: 게임 속 게임에 들어가면 조작 장치도 훑기 판도 통째로 숨는다. 그래서
		///   기존 안내는 거기서 절대 안 뜬다. 그런데 *정작 안 보이는 조작이 거기 있다* — 두 손가락으로
		///   밀면 시점이 움직이고 비틀면 돈다는 것은 화면 어디에도 안 적혀 있다(실기 2026-08-07:
		///   "영웅은 움직이는데 카메라 못 움직임").
		/// ★ 손가락은 통과시킨다 — 가르치려다 그 자리를 막으면 안 된다.
		/// ★ 한 번 그렇게 해 보면 영영 안 뜬다.
		/// </summary>
		private void BuildOverheadHint(UIRoot uiRoot)
		{
			if (OverheadHintSeen)
				return;

			overheadHint = new Label(overheadHintText) { name = "MobileOverheadHint" };
			overheadHint.pickingMode = PickingMode.Ignore;
			overheadHint.style.position = Position.Absolute;
			overheadHint.style.left = 0;
			overheadHint.style.right = 0;
			overheadHint.style.top = Length.Percent(12f);
			overheadHint.style.unityTextAlign = TextAnchor.MiddleCenter;
			overheadHint.style.fontSize = lookHintFontSize;
			overheadHint.style.opacity = lookHintOpacity;
			overheadHint.style.color = Color.white;
			overheadHint.style.display = DisplayStyle.None;
			uiRoot.HudLayer.Add(overheadHint);
		}

		/// <summary>
		/// 투기장(관전)에서 나가는 버튼 — 폰에는 이 길이 **아예 없었다** (TASK-WM-200).
		///
		/// ★ 투기장은 나가는 방법이 키 하나(X)뿐이다. 그런데 그 모드에선 손가락 조작 장치가 통째로
		///   숨고, 투기장은 자기 화면 메뉴를 안 가졌다(개척은 가졌다). 그래서 폰에서 투기장에 들어가면
		///   **앱을 죽이는 것 말고 나갈 방법이 0** 이었다.
		/// ★ 개척에는 이미 자기 메뉴가 있으니 여기서는 투기장일 때만 띄운다 — 나가는 문이 둘이면
		///   어느 쪽이 진짜인지 헷갈린다.
		/// </summary>
		private void BuildArenaExitButton(UIRoot uiRoot)
		{
			arenaExitButton = MakeTapButton("나가기", InputEventType.Cancel);
			arenaExitButton.name = "MobileArenaExit";
			arenaExitButton.style.position = Position.Absolute;
			arenaExitButton.style.left = edgeMargin;
			arenaExitButton.style.top = edgeMargin;
			arenaExitButton.style.display = DisplayStyle.None;
			uiRoot.HudLayer.Add(arenaExitButton);
		}

		private void UpdateArenaExitButton(InputManager inputManager)
		{
			if (arenaExitButton == null)
				return;

			bool inArena = GameModeManager.TryGetExistingInstance(out GameModeManager gameModeManager)
				&& gameModeManager.CurrentMode == GameMode.Arena;
			DisplayStyle display = inArena && inputManager != null && inputManager.IsTouchMode
				? DisplayStyle.Flex
				: DisplayStyle.None;
			if (arenaExitButton.style.display != display)
				arenaExitButton.style.display = display;
		}

		private static bool OverheadHintSeen => PlayerPrefs.GetInt(OVERHEAD_HINT_SEEN_KEY, 0) == 1;

		/// <summary>
		/// 부감 안내를 띄울지 정하고, 실제로 두 손가락을 써 보면 지운다.
		/// 「조작 장치가 숨는 상황」이 곧 「부감 시점」이라 그 판정을 그대로 재사용한다.
		/// </summary>
		private void UpdateOverheadHint(bool controlsShown, InputManager inputManager)
		{
			if (overheadHint == null)
				return;

			// ★ 「조작 장치가 숨어 있다」만으로는 부족하다 — 제목 화면에서도 숨는다. 거기서 두 손가락
			//   안내가 뜨면 아무 뜻도 없는 글자가 첫 화면을 덮는다. *게임 속 게임에 들어와 있을 때*로
			//   좁힌다.
			bool inSubGame = GameModeManager.TryGetExistingInstance(out GameModeManager gameModeManager)
				&& gameModeManager.CurrentMode != GameMode.Default;
			bool overhead = inSubGame && controlsShown == false && inputManager != null && inputManager.IsTouchMode;
			overheadHint.style.display = overhead ? DisplayStyle.Flex : DisplayStyle.None;
			if (overhead == false)
				return;

			bool used = inputManager.PointerTwoFingerPanDelta.sqrMagnitude > 0f
				|| Mathf.Abs(inputManager.PointerTwistDelta) > 0f;
			if (used == false)
				return;

			overheadHint.RemoveFromHierarchy();
			overheadHint = null;
			PlayerPrefs.SetInt(OVERHEAD_HINT_SEEN_KEY, 1);
			PlayerPrefs.Save();
		}

		private void BuildActionButtons(VisualElement parent)
		{
			VisualElement column = new VisualElement();
			column.style.position = Position.Absolute;
			column.style.right = edgeMargin;
			column.style.bottom = edgeMargin + bottomSafeOffset;
			column.style.alignItems = Align.FlexEnd;
			column.pickingMode = PickingMode.Ignore;

			// 위에서 아래로: 덜 쓰는 것 → 자주 쓰는 것. 엄지는 아래쪽이 가장 편하다.
			VisualElement topRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
			topRow.pickingMode = PickingMode.Ignore;
			// ★ 스킬은 손가락으로 닿는 길이 아예 없었다 (2026-08-07 도달성 점검). 키보드에만 있으면
			//   폰에서는 *그 기능이 없는 게임*이다 — 화면에 안 보이니 없는 줄도 모른다.
			topRow.Add(MakeHoldButton("스킬", InputEventType.Space, actionButtonSize * 0.8f));
			topRow.Add(MakeHoldButton("보조", InputEventType.Click1, actionButtonSize * 0.8f));
			topRow.Add(MakeHoldButton("뛰기", InputEventType.Sprint, actionButtonSize * 0.8f));
			// 조준 모드도 키 하나(Y)에만 있었다 — 폰에선 조준으로 바꿀 방법이 0 이었다.
			// 누르는 순간 「바꿔라」가 나가는 종류라 이 버튼으로 그대로 된다(떼는 것은 아무도 안 듣는다).
			topRow.Add(MakeHoldButton("조준", InputEventType.ChangeMode, actionButtonSize * 0.8f));
			column.Add(topRow);

			// 말걸기는 *가끔* 뜨는 것이라 늘 있는 버튼들과 줄을 섞지 않는다 — 섞으면 나타날 때마다
			// 점프·공격이 옆으로 밀려서, 방금 외운 손가락 위치가 어긋난다.
			VisualElement interactRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
			interactRow.pickingMode = PickingMode.Ignore;
			interactButton = MakeTapButton("말걸기", InputEventType.Submit);
			interactButton.style.display = DisplayStyle.None;
			interactRow.Add(interactButton);
			column.Add(interactRow);

			VisualElement bottomRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
			bottomRow.pickingMode = PickingMode.Ignore;
			bottomRow.Add(MakeHoldButton("점프", InputEventType.Jump, actionButtonSize));
			bottomRow.Add(MakeHoldButton("공격", InputEventType.Click0, actionButtonSize * 1.25f));
			// 앉기도 키 하나(C)에만 있었다. 「누르는 동안 앉는다」라서 누름·뗌을 그대로 넘기는
			// 이 버튼이 맞다 — 톡 버튼에 걸면 켜지자마자 꺼져 아무 일도 안 일어난다.
			bottomRow.Add(MakeHoldButton("앉기", InputEventType.Crouch, actionButtonSize * 0.8f));
			column.Add(bottomRow);

			column.name = "MobileActionButtons";
			parent.Add(column);
			RegisterMovable(column);
		}

		private VisualElement windowMenuColumn;
		private Label interactButton;

		/// <summary>
		/// 창으로 가는 길 (TASK-WM-200) — 폰엔 키보드가 없다.
		///
		/// ★ 이게 없으면 폰에서 *인벤토리·퀘스트·도감·마도서·인형·스탯이 통째로 없는 기능*이 된다.
		///   전부 키 하나에만 매달려 있었고(I/J/B/M/K/V), 화면 어디에도 문이 없었다. 조작 장치를
		///   아무리 잘 만들어도 물건을 못 꺼내면 게임이 성립하지 않는다.
		/// ★ 왜 접어 두나: 창이 여섯이라 늘 펼쳐 두면 화면 오른쪽 절반이 버튼밭이 된다.
		///   자주 쓰는 것은 아래 동작 버튼이고, 이건 「가끔 여는 것들」이다.
		/// </summary>
		private void BuildWindowMenu(VisualElement parent)
		{
			VisualElement corner = new VisualElement();
			corner.style.position = Position.Absolute;
			corner.style.right = edgeMargin;
			corner.style.top = edgeMargin;
			corner.style.alignItems = Align.FlexEnd;
			corner.pickingMode = PickingMode.Ignore;

			// ★ 목록이 화면보다 길어질 수 있다. 손끝 크기 바닥을 깔면서 칸이 커졌고, 항목도 늘었다 —
			//   계산해 보면 여덟 개에 토글까지 872 인데 화면은 800 이다. 넘치면 **아래쪽 항목이
			//   화면 밖으로 밀려 영영 못 누른다**(잘리는 게 아니라 사라진다).
			//   그래서 넘칠 때만 스스로 굴러가게 둔다 — 항목이 더 늘어도 같은 문제가 안 난다.
			ScrollView windowMenuScroll = new ScrollView(ScrollViewMode.Vertical)
			{
				name = "MobileWindowMenuScroll",
			};
			windowMenuScroll.style.display = DisplayStyle.None;
			// ★ 백분율로 주면 안 된다 — 이 목록을 담은 상자는 세로가 *내용에 따라 정해지는* 상자라,
			//   「그 72%」가 가리킬 기준이 없다. 그러면 높이가 0 으로 접혀 **메뉴가 통째로 안 보인다**.
			//   화면 기준 세로에서 직접 계산한 픽셀로 준다.
			windowMenuScroll.style.maxHeight = PANEL_REFERENCE_HEIGHT * 0.72f;
			windowMenuScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
			windowMenuScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;

			windowMenuColumn = windowMenuScroll;
			windowMenuColumn.style.alignItems = Align.FlexEnd;
			windowMenuScroll.contentContainer.style.alignItems = Align.FlexEnd;

			windowMenuColumn.Add(MakeTapButton("가방", InputEventType.Inventory));
			windowMenuColumn.Add(MakeTapButton("퀘스트", InputEventType.QuestToggle));
			windowMenuColumn.Add(MakeTapButton("도감", InputEventType.DiscoveryToggle));
			windowMenuColumn.Add(MakeTapButton("마도서", InputEventType.MagicBookToggle));
			windowMenuColumn.Add(MakeTapButton("인형", InputEventType.DollToggle));
			windowMenuColumn.Add(MakeTapButton("몸 상태", InputEventType.Status));
			// 건축도 키 하나에만 매달려 있었다 — 폰에서 집을 지을 방법이 0 이었다.
			windowMenuColumn.Add(MakeTapButton("건축", InputEventType.BuildModeToggle));
			// 시점 바꾸기(1인칭/3인칭 순환)도 키 하나에만 있었다 — 폰에선 시점을 영영 못 바꿨다.
			// 자주 쓰는 조작이 아니라 화면에 상시 띄우지 않고 이 서랍에 둔다.
			windowMenuColumn.Add(MakeTapButton("시점", InputEventType.CameraViewCycle));
			// 1인칭↔3인칭은 위의 「시점 순환」과 다른 축이다(그건 부감·자유비행을 도는 것).
			// 이것도 키 하나(F5)에만 있어서 폰에선 1인칭으로 갈 방법이 0 이었다.
			windowMenuColumn.Add(MakeTapButton("1인칭", InputEventType.CameraPerspectiveToggle));
			windowMenuColumn.Add(MakeLayoutEditButton());

			Label toggle = MakeMenuToggleButton();
			corner.Add(toggle);
			corner.Add(windowMenuColumn);
			corner.name = "MobileWindowMenu";
			parent.Add(corner);
			RegisterMovable(corner);

			BuildEditBar(parent);
		}

		private Label MakeMenuToggleButton()
		{
			Label button = new Label("창") { name = "MobileWindowMenuToggle" };
			StyleRoundButton(button, actionButtonSize * 0.7f);
			button.RegisterCallback<PointerDownEvent>(evt =>
			{
				bool open = windowMenuColumn.style.display == DisplayStyle.None;
				windowMenuColumn.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
				evt.StopPropagation();
			});
			return button;
		}

		/// <summary> 한 번 톡 = 그 키를 한 번 눌렀다 뗀 것 — 창 여닫기처럼 「누르고 있기」가 뜻 없는 것들. </summary>
		private Label MakeTapButton(string label, InputEventType inputEventType)
		{
			Label button = new Label(label) { name = "MobileWindowButton_" + inputEventType };
			StyleRoundButton(button, actionButtonSize * 0.7f);
			button.style.width = actionButtonSize * 1.15f;
			// ★ 예전엔 *닿는 순간* 곧바로 눌렀다. 그런데 이 버튼들은 위아래로 굴러가는 서랍 안에 있다 —
			//   서랍을 굴리려고 버튼 위에서 손가락을 끌면, 끌기가 시작되기도 전에 창이 열려 버린다.
			//   그래서 「닿았다 → 거의 안 움직이고 → 그 자리에서 뗐다」일 때만 누른 것으로 친다.
			// ★ 손가락을 붙잡지(capture) 않는다 — 붙잡으면 서랍이 그 끌기를 못 받아 아예 안 굴러간다.
			Vector2 pressedAt = Vector2.zero;
			int pressedPointerId = -1;

			button.RegisterCallback<PointerDownEvent>(evt =>
			{
				pressedAt = evt.position;
				pressedPointerId = evt.pointerId;
			});

			button.RegisterCallback<PointerUpEvent>(evt =>
			{
				if (evt.pointerId != pressedPointerId)
					return;
				pressedPointerId = -1;

				if (Vector2.Distance(evt.position, pressedAt) > tapSlopPixels)
					return; // 굴린 것이지 누른 것이 아니다

				// 자리를 옮기는 중이면 버튼은 손잡이일 뿐이다 — 살짝 옮기고 뗐다고 창이 열리면 안 된다.
				if (layoutEditMode)
					return;

				if (InputManager.TryGetExistingInstance(out InputManager inputManager))
				{
					inputManager.PressFromScreenButton(inputEventType);
					inputManager.ReleaseFromScreenButton(inputEventType);
				}
				evt.StopPropagation();
			});
			return button;
		}

		private static float TouchFriendly(float logicalSize)
		{
			// 계산은 순수 함수에 있다(시험으로 못 박은 자리) — 여기서는 기기 값만 건네준다.
			return Mathf.Max(
				logicalSize,
				MobileLayoutStore.MinimumTouchSize(Screen.dpi, Screen.height, PANEL_REFERENCE_HEIGHT));
		}

		private void StyleRoundButton(Label button, float rawSize)
		{
			float size = TouchFriendly(rawSize);
			button.style.width = size;
			button.style.height = size;
			button.style.marginTop = 8;
			button.style.borderTopLeftRadius = size * 0.35f;
			button.style.borderTopRightRadius = size * 0.35f;
			button.style.borderBottomLeftRadius = size * 0.35f;
			button.style.borderBottomRightRadius = size * 0.35f;
			button.style.backgroundColor = new Color(0.1f, 0.12f, 0.17f, 0.62f);
			SetBorder(button, new Color(0.75f, 0.8f, 0.9f, 0.4f), 2f);
			button.style.color = new Color(0.93f, 0.95f, 0.99f, 1f);
			button.style.unityTextAlign = TextAnchor.MiddleCenter;
			button.style.fontSize = Mathf.Max(13f, size * 0.28f);
			button.pickingMode = PickingMode.Position;
		}

		/// <summary>
		/// 누르고 있는 동안 눌린 것으로 치는 버튼 — 「한 번 눌림」으로만 만들면 계속 휘두르는 공격이 죽는다.
		/// </summary>
		private VisualElement MakeHoldButton(string label, InputEventType inputEventType, float rawSize)
		{
			float size = TouchFriendly(rawSize);
			Label button = new Label(label) { name = "MobileButton_" + inputEventType };
			button.style.width = size;
			button.style.height = size;
			button.style.marginLeft = 10;
			button.style.marginTop = 10;
			button.style.borderTopLeftRadius = size;
			button.style.borderTopRightRadius = size;
			button.style.borderBottomLeftRadius = size;
			button.style.borderBottomRightRadius = size;
			button.style.backgroundColor = new Color(0.1f, 0.12f, 0.17f, 0.55f);
			SetBorder(button, new Color(0.75f, 0.8f, 0.9f, 0.4f), 2f);
			button.style.color = new Color(0.93f, 0.95f, 0.99f, 1f);
			button.style.unityTextAlign = TextAnchor.MiddleCenter;
			button.style.fontSize = Mathf.Max(14f, size * 0.22f);
			button.pickingMode = PickingMode.Position;

			button.RegisterCallback<PointerDownEvent>(evt =>
			{
				button.CapturePointer(evt.pointerId);
				button.style.backgroundColor = new Color(0.24f, 0.42f, 0.72f, 0.8f);
				if (InputManager.TryGetExistingInstance(out InputManager inputManager))
					inputManager.PressFromScreenButton(inputEventType);
				// 「지금 눌린 채로 있는 것」을 적어 둔다 — 뗄 기회를 잃어도 누군가는 놓아 줘야 한다.
				if (heldButtons.Contains(inputEventType) == false)
					heldButtons.Add(inputEventType);
				evt.StopPropagation();
			});
			button.RegisterCallback<PointerUpEvent>(evt =>
			{
				if (button.HasPointerCapture(evt.pointerId))
					button.ReleasePointer(evt.pointerId);
				ReleaseHeld(inputEventType, button);
				evt.StopPropagation();
			});

			// ★ 손가락을 잡고 있던 권한을 잃는 경우가 있다 — 창이 열리거나, 화면이 다시 그려지거나,
			//   조작 장치가 통째로 숨는 순간이다(개척에 들어가면 실제로 숨는다). 그때는 「뗐다」가
			//   영영 안 오므로 **누른 채로 굳는다** — 뛰기를 누르고 개척에 들어가면 마을에 돌아와도
			//   계속 뛰는 식이다. 권한을 잃는 순간을 뗀 것으로 친다.
			button.RegisterCallback<PointerCaptureOutEvent>(_ => ReleaseHeld(inputEventType, button));

			return button;
		}

		private static void SetBorder(VisualElement element, Color color, float width)
		{
			element.style.borderTopColor = color;
			element.style.borderBottomColor = color;
			element.style.borderLeftColor = color;
			element.style.borderRightColor = color;
			element.style.borderTopWidth = width;
			element.style.borderBottomWidth = width;
			element.style.borderLeftWidth = width;
			element.style.borderRightWidth = width;
		}
	}
}

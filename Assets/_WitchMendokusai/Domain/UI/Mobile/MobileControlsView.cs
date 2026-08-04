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
	public class MobileControlsView : MonoBehaviour
	{
		[Header("_" + nameof(MobileControlsView))]
		[Tooltip("스틱 판의 지름(픽셀).")]
		[SerializeField, Min(40f)] private float stickSize = 220f;
		[Tooltip("스틱 손잡이의 지름(픽셀).")]
		[SerializeField, Min(20f)] private float knobSize = 90f;
		[Tooltip("이 비율 안쪽은 안 민 것으로 본다 — 손가락이 살짝 흔들려도 캐릭터가 안 움직이게.")]
		[SerializeField, Range(0f, 0.5f)] private float stickDeadZone = 0.15f;
		[Tooltip("화면 가장자리에서 조작 장치까지 띄우는 여백(픽셀).")]
		[SerializeField, Min(0f)] private float edgeMargin = 36f;
		[Tooltip("동작 버튼 한 변(픽셀). 손끝이 뭉툭해서 데스크톱 버튼보다 커야 한다.")]
		[SerializeField, Min(44f)] private float actionButtonSize = 96f;
		[Tooltip("화면을 훑은 픽셀 → 시점 회전량 배율. 1 이면 마우스와 같은 감도.")]
		[SerializeField, Min(0.05f)] private float lookSensitivity = 1f;

		private VisualElement lookBackdrop;
		private VisualElement controlsRoot;
		private VisualElement stickBase;
		private VisualElement stickKnob;

		private int stickPointerId = -1;
		private Vector2 stickCenter;
		private Vector2 stickValue;
		private Vector2 lookAccumulated;

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
			built = false;
		}

		private void Update()
		{
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
			show = show && PlayerProvider.TryGetExistingInstance(out PlayerProvider playerProvider)
				&& playerProvider.HasPlayer;

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
					ReleaseStick();
			}

			PushStickValue();
		}

		/// <summary> 훑은 양을 넘기고 비운다 — 한 번 쓴 움직임을 다음 프레임에 또 쓰면 시점이 계속 흐른다. </summary>
		private Vector2 ConsumeLookDelta()
		{
			Vector2 delta = lookAccumulated * lookSensitivity;
			lookAccumulated = Vector2.zero;
			return delta;
		}

		private void Build()
		{
			if (built)
				return;
			if (UIRoot.TryGetExistingInstance(out UIRoot uiRoot) == false || uiRoot.HudLayer == null)
				return;

			BuildLookBackdrop(uiRoot);
			BuildControls(uiRoot);
			built = true;
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

			uiRoot.HudLayer.Insert(0, lookBackdrop);
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
		}

		private void OnLookUp(PointerUpEvent evt)
		{
			if (lookBackdrop.HasPointerCapture(evt.pointerId))
				lookBackdrop.ReleasePointer(evt.pointerId);
		}

		private void BuildControls(UIRoot uiRoot)
		{
			controlsRoot = new VisualElement { name = "MobileControls" };
			controlsRoot.style.position = Position.Absolute;
			controlsRoot.style.left = 0;
			controlsRoot.style.right = 0;
			controlsRoot.style.top = 0;
			controlsRoot.style.bottom = 0;
			controlsRoot.style.display = DisplayStyle.None;
			controlsRoot.pickingMode = PickingMode.Ignore;

			BuildStick(controlsRoot);
			BuildActionButtons(controlsRoot);

			uiRoot.HudLayer.Add(controlsRoot);
		}

		private void BuildStick(VisualElement parent)
		{
			stickBase = new VisualElement { name = "MobileStick" };
			stickBase.style.position = Position.Absolute;
			stickBase.style.left = edgeMargin;
			stickBase.style.bottom = edgeMargin;
			stickBase.style.width = stickSize;
			stickBase.style.height = stickSize;
			stickBase.style.borderTopLeftRadius = stickSize;
			stickBase.style.borderTopRightRadius = stickSize;
			stickBase.style.borderBottomLeftRadius = stickSize;
			stickBase.style.borderBottomRightRadius = stickSize;
			stickBase.style.backgroundColor = new Color(0.08f, 0.09f, 0.13f, 0.35f);
			SetBorder(stickBase, new Color(0.75f, 0.8f, 0.9f, 0.35f), 2f);
			stickBase.pickingMode = PickingMode.Position;

			stickKnob = new VisualElement { name = "MobileStickKnob" };
			stickKnob.style.position = Position.Absolute;
			stickKnob.style.width = knobSize;
			stickKnob.style.height = knobSize;
			stickKnob.style.borderTopLeftRadius = knobSize;
			stickKnob.style.borderTopRightRadius = knobSize;
			stickKnob.style.borderBottomLeftRadius = knobSize;
			stickKnob.style.borderBottomRightRadius = knobSize;
			stickKnob.style.backgroundColor = new Color(0.85f, 0.88f, 0.95f, 0.55f);
			stickKnob.pickingMode = PickingMode.Ignore;
			stickBase.Add(stickKnob);
			CenterKnob();

			stickBase.RegisterCallback<PointerDownEvent>(OnStickDown);
			stickBase.RegisterCallback<PointerMoveEvent>(OnStickMove);
			stickBase.RegisterCallback<PointerUpEvent>(OnStickUp);

			parent.Add(stickBase);
		}

		private void CenterKnob()
		{
			stickKnob.style.left = (stickSize - knobSize) * 0.5f;
			stickKnob.style.top = (stickSize - knobSize) * 0.5f;
		}

		private void OnStickDown(PointerDownEvent evt)
		{
			if (stickPointerId >= 0)
				return; // 스틱은 손가락 하나만 받는다 — 둘째 손가락은 다른 조작의 것이다.

			stickPointerId = evt.pointerId;
			stickBase.CapturePointer(evt.pointerId);
			stickCenter = new Vector2(stickSize, stickSize) * 0.5f;
			ApplyStick(evt.localPosition);
			evt.StopPropagation();
		}

		private void OnStickMove(PointerMoveEvent evt)
		{
			if (evt.pointerId != stickPointerId)
				return;
			ApplyStick(evt.localPosition);
		}

		private void OnStickUp(PointerUpEvent evt)
		{
			if (evt.pointerId != stickPointerId)
				return;
			ReleaseStick();
		}

		private void ApplyStick(Vector3 localPosition)
		{
			float radius = (stickSize - knobSize) * 0.5f;
			Vector2 offset = new Vector2(localPosition.x, localPosition.y) - stickCenter;
			offset = Vector2.ClampMagnitude(offset, radius);

			stickKnob.style.left = stickCenter.x + offset.x - knobSize * 0.5f;
			stickKnob.style.top = stickCenter.y + offset.y - knobSize * 0.5f;

			Vector2 raw = radius <= 0f ? Vector2.zero : offset / radius;
			// 화면은 아래로 갈수록 y 가 커지고, 걷는 방향은 위가 앞이다.
			raw = new Vector2(raw.x, -raw.y);

			// 죽은 구역 — 살짝 스친 것을 「걸어라」로 읽으면 캐릭터가 계속 실룩거린다.
			stickValue = raw.magnitude <= stickDeadZone ? Vector2.zero : raw;
		}

		private void ReleaseStick()
		{
			if (stickBase != null && stickPointerId >= 0 && stickBase.HasPointerCapture(stickPointerId))
				stickBase.ReleasePointer(stickPointerId);
			stickPointerId = -1;
			stickValue = Vector2.zero;
			if (stickKnob != null)
				CenterKnob();
			PushStickValue();
		}

		private void PushStickValue()
		{
			if (SOManagerBridge.HasInstance == false)
				return;
			SOManagerBridge.JoystickX.RuntimeValue = stickValue.x;
			SOManagerBridge.JoystickY.RuntimeValue = stickValue.y;
		}

		private void BuildActionButtons(VisualElement parent)
		{
			VisualElement column = new VisualElement();
			column.style.position = Position.Absolute;
			column.style.right = edgeMargin;
			column.style.bottom = edgeMargin;
			column.style.alignItems = Align.FlexEnd;
			column.pickingMode = PickingMode.Ignore;

			// 위에서 아래로: 덜 쓰는 것 → 자주 쓰는 것. 엄지는 아래쪽이 가장 편하다.
			VisualElement topRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
			topRow.pickingMode = PickingMode.Ignore;
			topRow.Add(MakeHoldButton("보조", InputEventType.Click1, actionButtonSize * 0.8f));
			topRow.Add(MakeHoldButton("뛰기", InputEventType.Sprint, actionButtonSize * 0.8f));
			column.Add(topRow);

			VisualElement bottomRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
			bottomRow.pickingMode = PickingMode.Ignore;
			bottomRow.Add(MakeHoldButton("점프", InputEventType.Jump, actionButtonSize));
			bottomRow.Add(MakeHoldButton("공격", InputEventType.Click0, actionButtonSize * 1.25f));
			column.Add(bottomRow);

			parent.Add(column);
		}

		/// <summary>
		/// 누르고 있는 동안 눌린 것으로 치는 버튼 — 「한 번 눌림」으로만 만들면 계속 휘두르는 공격이 죽는다.
		/// </summary>
		private VisualElement MakeHoldButton(string label, InputEventType inputEventType, float size)
		{
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
				evt.StopPropagation();
			});
			button.RegisterCallback<PointerUpEvent>(evt =>
			{
				if (button.HasPointerCapture(evt.pointerId))
					button.ReleasePointer(evt.pointerId);
				button.style.backgroundColor = new Color(0.1f, 0.12f, 0.17f, 0.55f);
				if (InputManager.TryGetExistingInstance(out InputManager inputManager))
					inputManager.ReleaseFromScreenButton(inputEventType);
				evt.StopPropagation();
			});

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

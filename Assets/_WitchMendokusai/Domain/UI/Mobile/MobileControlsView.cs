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

		/// <summary>
		/// 화면 아래쪽에 이미 있는 것들(체력·이름표·핫바·날짜)을 피해 띄우는 높이 (실측으로 정한 값).
		/// ★ 겹치면 둘 다 못 쓴다 — 조작 장치를 누르려다 체력바를 누르고, 체력이 얼마인지도 안 보인다.
		/// </summary>
		[Tooltip("아래쪽 기존 화면 요소(체력·날짜 등)를 피해 띄우는 높이(픽셀).")]
		[SerializeField, Min(0f)] private float bottomSafeOffset = 140f;
		[Tooltip("동작 버튼 한 변(픽셀). 손끝이 뭉툭해서 데스크톱 버튼보다 커야 한다.")]
		[SerializeField, Min(44f)] private float actionButtonSize = 96f;
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
		private VisualElement controlsRoot;
		private VisualElement stickBase;
		private VisualElement stickKnob;

		private int stickPointerId = -1;
		private Vector2 stickCenter;
		private Vector2 stickValue;
		private Vector2 lookAccumulated;
		private float lookHintDragged;

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
			// ★ 화면 층은 다시 만들어질 수 있다 (UIRoot 가 켜질 때마다 층을 새로 세운다).
			//   그러면 이 조작 장치는 *옛 층에 붙은 채* 화면에서 사라지는데, 스스로는 「만들었다」고
			//   믿고 있어서 영영 안 돌아온다 — 폰에서 갑자기 못 움직이게 되는 종류의 결함이다.
			//   붙어 있는지 매 프레임 확인하고, 떨어졌으면 다시 붙는다(조용히 없어지는 것보다 낫다).
			if (built && IsAttached() == false)
				built = false;

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
					ReleaseStick();
			}

			PushStickValue();
			UpdateInteractButton(show, playerProvider);
		}

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

		/// <summary> 훑은 양을 넘기고 비운다 — 한 번 쓴 움직임을 다음 프레임에 또 쓰면 시점이 계속 흐른다. </summary>
		private Vector2 ConsumeLookDelta()
		{
			Vector2 delta = lookAccumulated * lookSensitivity;
			lookAccumulated = Vector2.zero;
			return delta;
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
			BuildWindowMenu(controlsRoot);

			uiRoot.HudLayer.Add(controlsRoot);
		}

		private void BuildStick(VisualElement parent)
		{
			stickBase = new VisualElement { name = "MobileStick" };
			stickBase.style.position = Position.Absolute;
			stickBase.style.left = edgeMargin;
			stickBase.style.bottom = edgeMargin + bottomSafeOffset;
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
			column.style.bottom = edgeMargin + bottomSafeOffset;
			column.style.alignItems = Align.FlexEnd;
			column.pickingMode = PickingMode.Ignore;

			// 위에서 아래로: 덜 쓰는 것 → 자주 쓰는 것. 엄지는 아래쪽이 가장 편하다.
			VisualElement topRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
			topRow.pickingMode = PickingMode.Ignore;
			topRow.Add(MakeHoldButton("보조", InputEventType.Click1, actionButtonSize * 0.8f));
			topRow.Add(MakeHoldButton("뛰기", InputEventType.Sprint, actionButtonSize * 0.8f));
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
			column.Add(bottomRow);

			parent.Add(column);
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

			windowMenuColumn = new VisualElement();
			windowMenuColumn.style.display = DisplayStyle.None;
			windowMenuColumn.style.alignItems = Align.FlexEnd;
			windowMenuColumn.pickingMode = PickingMode.Ignore;

			windowMenuColumn.Add(MakeTapButton("가방", InputEventType.Inventory));
			windowMenuColumn.Add(MakeTapButton("퀘스트", InputEventType.QuestToggle));
			windowMenuColumn.Add(MakeTapButton("도감", InputEventType.CodexToggle));
			windowMenuColumn.Add(MakeTapButton("마도서", InputEventType.MagicBookToggle));
			windowMenuColumn.Add(MakeTapButton("인형", InputEventType.DollToggle));
			windowMenuColumn.Add(MakeTapButton("몸 상태", InputEventType.Status));

			Label toggle = MakeMenuToggleButton();
			corner.Add(toggle);
			corner.Add(windowMenuColumn);
			parent.Add(corner);
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
			button.RegisterCallback<PointerDownEvent>(evt =>
			{
				if (InputManager.TryGetExistingInstance(out InputManager inputManager))
				{
					inputManager.PressFromScreenButton(inputEventType);
					inputManager.ReleaseFromScreenButton(inputEventType);
				}
				evt.StopPropagation();
			});
			return button;
		}

		private void StyleRoundButton(Label button, float size)
		{
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

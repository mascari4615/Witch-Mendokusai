using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace WitchMendokusai
{
	// InputManager 의 포인터와 마우스 위치 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 InputManager.cs 를 본다.
	public partial class InputManager : MonoBehaviour
	{
		[SerializeField] private InputActionAsset inputActionAsset;
		[SerializeField] private LayerMask mouseWorldLayerMask;
		[SerializeField] private float mouseWorldRayDistance = 100f;

		// TASK-WM-200 — 손가락 몸짓 문턱값. 화면 밀도가 다른 기기에서 다시 재야 해서 꺼내 둔다.
		[Header("모바일 — 손가락 몸짓")]
		[SerializeField] private float tapMaxSeconds = 0.35f;
		[SerializeField] private float tapMaxTravelPixels = 24f;
		[SerializeField] private float dragSlopPixels = 12f;
		[SerializeField] private float pinchToZoomScale = 0.5f;
		[Tooltip("두 손가락이 이만큼 돌아가기 전엔 시점을 안 돌린다 (도). 오므릴 때 손이 딸려 도는 각도를 거른다.")]
		[SerializeField, Min(0f)] private float twistDeadZoneDegrees = 8f;

		/// <summary>
		/// 손가락 조작을 강제로 켠다 — 컴퓨터에서 *폰 화면을 확인하기 위한* 스위치 (TASK-WM-200).
		///
		/// ★ 왜 필요한가: 모바일 조작은 컴퓨터에 손가락이 없어서 영영 눈으로 확인할 수 없다. 그러면
		///   「폰에서만 깨지는 화면」이 쌓이는데, 그건 폰을 꺼내 봐야만 발견된다 — 고치는 값이 가장 비싼
		///   자리다. 켜면 마우스가 손가락 노릇을 해서 화면 조작 장치를 그대로 눌러볼 수 있다.
		/// </summary>
		[Tooltip("컴퓨터에서 폰 조작 화면을 확인할 때 켠다 — 마우스가 손가락 노릇을 한다.")]
		[SerializeField] private bool forceTouchMode;

		/// <summary>
		/// 가리키는 것 하나 — 마우스/손가락을 같은 얼굴로 만든다 (TASK-WM-200).
		/// 아래 Mouse* 프로퍼티들은 전부 이걸 통해서 나간다. 「마우스」라는 이름은 유지하는데,
		/// 부르는 쪽 300곳의 뜻이 원래부터 「가리키는 자리」였기 때문이다(이름만 마우스였다).
		/// </summary>
		private readonly PointerDevice pointer = new();

		public Vector3 MouseWorldPosition { get; private set; }
		// TASK-WM-181 INC-2 — 마우스 ray 가 맞은 표면의 법선. 마크식 면-인접 배치(빌더가 hit+normal 로 인접 셀 계산)용.
		// 히트 없으면 Vector3.up (지면 위 폴백).
		public Vector3 MouseWorldNormal { get; private set; } = Vector3.up;
		public Vector2 MouseScreenPosition { get; private set; }
		// TASK-WM-135 — Mouse.current 직접 접근 캡슐화 (DollAnimator 폴링 / 잔존 null guard 정리).
		// TASK-WM-200 — 그 캡슐 안쪽을 PointerDevice 로 갈아끼웠다. 손가락도 여기로 들어온다.
		public bool IsMouseAvailable => Mouse.current != null || Touchscreen.current != null;
		public bool IsMouseLeftButtonPressed => pointer.IsPressed;
		public bool IsMouseRightButtonPressed => pointer.IsSecondaryPressed;

		// TASK-WM-200 — 모바일 조작. 「지금 손가락인가」는 기기 종류가 아니라 *마지막으로 만진 장치*다
		// (터치 노트북에서 기기로 판정하면 마우스를 쥐고도 손가락 UI 가 뜬다).
		public bool IsTouchMode => forceTouchMode || pointer.IsTouchMode;

		/// <summary> 폰 조작 화면을 컴퓨터에서 켜고 끈다 — 확인용. </summary>
		public void SetForceTouchMode(bool force)
		{
			forceTouchMode = force;
		}
		public bool IsPointerPressed => pointer.IsPressed;
		public bool PointerTappedThisFrame => pointer.TappedThisFrame;
		public Vector2 PointerTapPosition => pointer.TapPosition;
		public bool IsPointerDragging => pointer.IsDragging;
		public Vector2 PointerDragDelta => pointer.DragDelta;
		public Vector2 PointerTwoFingerPanDelta => pointer.TwoFingerPanDelta;
		public float PointerTwistDelta => pointer.TwistDelta;

		// Calling IsPointerOverGameObject() from within event processing (such as from InputAction callbacks) will not work as expected; it will query UI state from the last frame UnityEngine.EventSystems.EventSystem:IsPointerOverGameObject ()
		// public bool IsPointerOverUI() => EventSystem.current.IsPointerOverGameObject();

		private bool isPointerOverUI;
		public bool IsPointerOverUI() => isPointerOverUI;

		// TASK-WM-200 — 장치를 읽는 유일한 자리. 시간은 unscaled 로 잰다(판이 멈춰도 손가락은 움직인다).
		private void UpdatePointer()
		{
			pointer.Tuning = new TouchGestureTuning
			{
				TapMaxSeconds = tapMaxSeconds,
				TapMaxTravelPixels = tapMaxTravelPixels,
				DragSlopPixels = dragSlopPixels,
				TwistDeadZoneDegrees = twistDeadZoneDegrees,
			};
			pointer.PinchToZoomScale = pinchToZoomScale;
			pointer.Update(Time.unscaledDeltaTime);
		}

		private void UpdateMouseWorldPosition()
		{
			// 가리킨 자리는 카메라가 없어도 뜻이 있다(UI 는 있다) — 월드 좌표만 카메라에 걸린다.
			MouseScreenPosition = pointer.Position;

			// Loading 씬은 카메라가 없음 - 2025.08.08 20:24
			if (Camera.main == null)
			{
				MouseWorldPosition = Vector3.zero;
				return;
			}

			Vector2 mouseScreen = pointer.Position;
			Vector3 mousePos = new(mouseScreen.x, mouseScreen.y, Camera.main.nearClipPlane);
			Ray ray = Camera.main.ScreenPointToRay(mousePos);

			if (TryResolveMouseWorldHit(ray, out RaycastHit hit))
			{
				MouseWorldPosition = hit.point;
				MouseWorldNormal = hit.normal; // 면-인접 배치용 (빌더가 hit+normal 로 인접 셀 결정)
			}
			else
			{
				MouseWorldPosition = Vector3.zero;
				MouseWorldNormal = Vector3.up;
			}
		}

		private bool TryResolveMouseWorldHit(Ray ray, out RaycastHit hit)
		{
			hit = default;

			float distance = Mathf.Max(1f, mouseWorldRayDistance);

			if (mouseWorldLayerMask.value != 0)
				return Physics.Raycast(ray, out hit, distance, mouseWorldLayerMask, QueryTriggerInteraction.Ignore);

			RaycastHit[] hits = Physics.RaycastAll(ray, distance, ~0, QueryTriggerInteraction.Ignore);
			if (hits == null || hits.Length == 0)
				return false;

			Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

			for (int i = 0; i < hits.Length; i++)
			{
				GroundSurface surface = hits[i].collider.GetComponent<GroundSurface>();
				if (surface != null && surface.IsWalkable)
				{
					hit = hits[i];
					return true;
				}
			}

			hit = hits[0];
			return true;
		}

		private void UpdateIsPointerOverUI()
		{
			// Loading 씬은 EventSystem이 없음 - 2025.08.08 20:24
			if (EventSystem.current == null)
			{
				isPointerOverUI = false;
				return;
			}

			if (EventSystem.current.IsPointerOverGameObject())
			{
				isPointerOverUI = true;
				return;
			}

			// ★ 손가락은 저마다 번호를 갖는다 (TASK-WM-200). 번호 없이 물으면 *마우스만* 묻는 것이라
			//   폰에서는 늘 「UI 위 아님」이 나온다 — 창 위를 눌렀는데 그 아래 땅이 반응한다.
			//   이 한 줄이 없으면 폰에서 모든 창이 클릭을 흘린다(조용히, 오직 폰에서만).
			Touchscreen screen = Touchscreen.current;
			if (screen != null)
			{
				System.Collections.Generic.IReadOnlyList<UnityEngine.InputSystem.Controls.TouchControl> touches = screen.touches;
				for (int i = 0; i < touches.Count; i++)
				{
					if (touches[i].press.isPressed == false)
						continue;
					if (EventSystem.current.IsPointerOverGameObject(touches[i].touchId.ReadValue()))
					{
						isPointerOverUI = true;
						return;
					}
				}
			}

			isPointerOverUI = false;
		}
	}
}

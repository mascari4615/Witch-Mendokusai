using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 부감(내려다보는) content 카메라 컨트롤러 — 도시 부감(심시티식) · 특수시공 개척이 **같은 컴포넌트
	/// 하나**를 쓴다. 어느 모드인지는 같은 GameObject 의 <see cref="MCamera"/> 가 들고 있는
	/// <see cref="ContentCameraMode"/> 단일 출처에서 읽으므로, vcam 을 하나 더 만들고 모드 값만 다르게
	/// 꽂으면 새 부감 시점이 생긴다(구 이름 CityViewCameraController — 도시 전용이 아니게 되어 개명).
	///
	/// 포커스 지점(맵 평면의 한 점)을 WASD/CameraMove 로 평면 이동, Q/E 로 yaw 회전, 휠로 높이 줌.
	/// 거동 수학은 <see cref="OverheadCameraRig"/> 단일 정본에 있고 본 컨트롤러는 **인스펙터 수치 +
	/// 활성 판단 + 무대 경계**만 담당한다.
	///
	/// ★ 왜 vcam(Cinemachine priority) 인가 (TASK-WM-194 근본 수정): 게임 속 게임(개척/투기장)도
	///   **진입한 순간 그 게임이 주체**다. 본편 카메라 위에 별도 Camera 를 덧대 렌더하면 밑에서 본편이
	///   계속 돌고, 화면 기준 카메라가 둘로 갈라진다 — 월드→화면 변환이 숨은 카메라를 잡아 데미지 숫자가
	///   엉뚱한 자리에 뜨던 버그가 그 증상이었다. priority 로 갈아끼우면 그려지는 카메라는 언제나 하나다.
	/// </summary>
	public class OverheadContentCameraController : FreeCameraControllerBase
	{
		// 모드별 컨트롤러 조회 — 모드 컨트롤러(개척 등)가 진입 시 무대 경계·시작 시점을 넘겨야 하는데
		// 그쪽은 다른 프리팹이라 인스펙터로 연결할 수 없다. Bridge 패턴(당사자가 자기를 등록)과 동형.
		private static readonly Dictionary<ContentCameraMode, OverheadContentCameraController> Registry = new();

		public static bool TryGet(ContentCameraMode mode, out OverheadContentCameraController controller)
		{
			return Registry.TryGetValue(mode, out controller) && controller != null;
		}

		[Header("부감 — pan")]
		[Tooltip("WASD 평면 이동 속도 (m/sec).")]
		[SerializeField] private float panSpeed = 20f;

		[Header("부감 — 회전")]
		[Tooltip("Q/E yaw 회전 속도 (deg/sec).")]
		[SerializeField] private float yawSpeed = 90f;
		[Tooltip("내려다보는 고정 pitch (deg, + = 아래). 심시티식 ~55-70.")]
		[SerializeField] private float fixedPitch = 60f;

		[Header("부감 — 줌(높이)")]
		[Tooltip("포커스로부터 카메라 거리 하한 (가까이 = 확대).")]
		[SerializeField] private float minHeight = 8f;
		[Tooltip("포커스로부터 카메라 거리 상한 (멀리 = 축소).")]
		[SerializeField] private float maxHeight = 40f;
		[Tooltip("줌 시작 높이.")]
		[SerializeField] private float initialHeight = 15f;
		[Tooltip("휠 한 칸당 높이 변화량. 플랫폼별 raw 델타 차이는 리그가 흡수한다.")]
		[SerializeField] private float zoomSpeed = 4f;

		[Header("부감 — 시작")]
		[Tooltip("포커스 시작 지점 (월드).")]
		[SerializeField] private Vector3 initialFocus = Vector3.zero;

		[Header("부감 — 무대 경계")]
		[Tooltip("포커스를 무대 안으로 가둘지. 마을처럼 끝이 없는 곳은 끄고, 개척처럼 무대가 유한하면 켠다.")]
		[SerializeField] private bool clampFocus;
		[Tooltip("화면 가장자리 몇 픽셀 안에 마우스가 들어오면 시점이 밀리나 — 0 이면 끔.")]
		[SerializeField, Min(0f)] private float edgePanBand = 24f;

		[Tooltip("가장자리 이동 세기 — 키 이동 대비 배수.")]
		[SerializeField, Min(0f)] private float edgePanStrength = 1f;

		[Tooltip("가둠 중심(월드). 모드 컨트롤러가 SetFocusBounds 로 덮어쓸 수 있다.")]
		[SerializeField] private Vector3 focusCenter;
		[Tooltip("중심에서 벗어날 수 있는 최대 거리.")]
		[SerializeField] private float focusLimit = 26f;

		private readonly OverheadCameraRig rig = new();

		private OverheadCameraRig.Settings RigSettings => new()
		{
			PanSpeed = panSpeed,
			YawSpeed = yawSpeed,
			FixedPitch = fixedPitch,
			MinHeight = minHeight,
			MaxHeight = maxHeight,
			ZoomSpeed = zoomSpeed,
			ClampFocus = clampFocus,
			FocusCenter = focusCenter,
			FocusLimit = focusLimit,
		};

		private void OnEnable()
		{
			// init-order-ok: 탐색이 아니라 자기 등록. Camera 는 base 의 lazy resolve 라 이 시점 안전.
			Registry[Camera.ContentCameraMode] = this;
		}

		private void OnDisable()
		{
			if (Registry.TryGetValue(Camera.ContentCameraMode, out OverheadContentCameraController registered) && registered == this)
				Registry.Remove(Camera.ContentCameraMode);
		}

		/// <summary>
		/// 무대 경계 지정 — 모드 컨트롤러가 진입 시 자기 무대(개척지 등) 기준으로 넘긴다.
		/// 무대 크기는 스테이지 데이터에 있으므로 카메라 프리팹에 다시 박지 않는다(수치 두 곳 박기 회피).
		/// </summary>
		public void SetFocusBounds(Vector3 center, float limit)
		{
			clampFocus = true;
			focusCenter = center;
			focusLimit = limit;
		}

		/// <summary> 시점을 지정 상태로 되돌린다 — 모드 진입 + 재시작 단일 경로. </summary>
		public void ResetView(Vector3 focus, float yaw, float height)
		{
			rig.Reset(focus, yaw, Mathf.Clamp(height, minHeight, maxHeight));
			rig.Apply(RigSettings, transform);
		}

		/// <summary> 줌 수치도 무대(스테이지 데이터)가 정할 수 있게 — 개척은 무대마다 적정 높이가 다르다. </summary>
		public void ConfigureZoom(float min, float max, float perNotch)
		{
			minHeight = min;
			maxHeight = max;
			zoomSpeed = perNotch;
		}

		protected override void Drive()
		{
			if (rig.IsInitialized == false)
				rig.Reset(initialFocus, 0f, Mathf.Clamp(initialHeight, minHeight, maxHeight));

			OverheadCameraRig.DriveInput input = new()
			{
				// 키 이동 + 화면 가장자리 이동은 *같은 축*으로 합산한다 — 둘을 따로 처리하면
				// 동시에 눌렀을 때 속도가 두 배가 되거나 서로를 덮는다.
				Move = Vector2.ClampMagnitude(InputManager.CameraMoveInput + EdgePanInput(), 1f),
				Rotate = InputManager.CameraRotateInput,
				ScrollDelta = InputManager.ScrollWheelDelta,
				SpeedMultiplier = SpeedMultiplier,
			};

			rig.Drive(input, RigSettings, Time.deltaTime, transform);
		}

		/// <summary> 시점을 그 자리로 — 지도·미니맵을 눌렀을 때(확대·회전은 그대로). </summary>
		public void LookAt(Vector3 focus)
		{
			rig.LookAt(focus);
			rig.Apply(RigSettings, transform);
		}

		/// <summary>
		/// 마우스가 화면 가장자리에 닿으면 그쪽으로 민다 — 롤·스타의 그 조작 (사용자 지시).
		///
		/// ★ 왜 화면 밖은 안 미나: 창 밖으로 커서가 나가면 마지막 좌표가 가장자리에 남아 *영원히 흐른다*.
		///   다른 창을 보다 돌아오면 판이 저 멀리 가 있는 식이라, 화면 안에 있을 때만 민다.
		/// ★ 가장자리 폭·속도는 값으로 노출한다 — 화면 크기와 손 감각에 따라 달라지는 수치다.
		/// </summary>
		private Vector2 EdgePanInput()
		{
			if (edgePanBand <= 0f)
				return Vector2.zero;

			Vector2 pointer = InputManager.MouseScreenPosition;
			if (pointer.x < 0f || pointer.y < 0f || pointer.x > Screen.width || pointer.y > Screen.height)
				return Vector2.zero;

			float x = 0f;
			float y = 0f;
			if (pointer.x <= edgePanBand)
				x = -(1f - pointer.x / edgePanBand);
			else if (pointer.x >= Screen.width - edgePanBand)
				x = 1f - (Screen.width - pointer.x) / edgePanBand;

			if (pointer.y <= edgePanBand)
				y = -(1f - pointer.y / edgePanBand);
			else if (pointer.y >= Screen.height - edgePanBand)
				y = 1f - (Screen.height - pointer.y) / edgePanBand;

			return new Vector2(x, y) * edgePanStrength;
		}
	}
}

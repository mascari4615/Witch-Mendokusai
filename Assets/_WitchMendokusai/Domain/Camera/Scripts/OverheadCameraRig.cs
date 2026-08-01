using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 부감(탑다운) 카메라 한 대의 상태 + 조작 수학 **단일 정본** — 포커스 지점 / yaw / 높이(=줌) 세 값과
	/// 「pan · 회전 · 휠 줌」 을 여기 한 곳에서만 계산한다.
	///
	/// ★ 왜 클래스로 뽑았나: 부감 카메라가 둘이 됐다(도시 부감 <see cref="CityViewCameraController"/> /
	///   특수시공 개척 모드 카메라). 같은 수학을 두 곳에 박으면 「휠 줌이 한쪽만 된다」 같은 어긋남이
	///   구조적으로 발생한다(사용자 실증: 개척에선 휠이 안 먹음). 한 곳을 고치면 둘 다 고쳐지게 한다.
	/// ★ MonoBehaviour 가 아닌 평범한 클래스 — 소유자가 MonoBehaviour(컨트롤러)든 모드 컨트롤러든
	///   필드로 들고 쓰면 되고, 수치는 소유자가 자기 정본(인스펙터 / 스테이지 SO)에서 넘긴다
	///   (수치 노출 룰: 여기엔 하드코딩 기본값을 두지 않는다).
	/// ★ Cinemachine 우회 — <see cref="FreeCameraControllerBase"/> 와 같은 근거로 transform 직접구동.
	/// </summary>
	public class OverheadCameraRig
	{
		/// <summary> 카메라 거동 수치 — 소유자의 정본(인스펙터 / SO)에서 매 프레임 넘겨받는다(캐싱 X). </summary>
		public struct Settings
		{
			public float PanSpeed;    // 평면 이동 속도 (m/sec)
			public float YawSpeed;    // 좌우 회전 속도 (deg/sec)
			public float FixedPitch;  // 내려다보는 고정 각도 (deg, + = 아래)
			public float MinHeight;   // 포커스로부터 거리 하한 (가까이 = 확대)
			public float MaxHeight;   // 상한 (멀리 = 축소)
			public float ZoomSpeed;   // 스크롤 1단위당 높이 변화량

			// 포커스 가둠 — 개척처럼 무대가 유한한 곳에서 화면 밖으로 무대를 잃어버리는 것 방지.
			public bool ClampFocus;
			public Vector3 FocusCenter;
			public float FocusLimit;
		}

		/// <summary> 이번 프레임 입력 — 소유자가 InputManager 에서 읽어 넘긴다(축 소스 단일화). </summary>
		public struct DriveInput
		{
			public Vector2 Move;         // 평면 이동 (yaw 기준 상대)
			public float Rotate;         // yaw 회전
			public float ScrollDelta;    // 휠 델타 (+ = 확대)
			public float SpeedMultiplier; // 가속(Ctrl 등)
		}

		public Vector3 Focus { get; private set; }
		public float Yaw { get; private set; }
		public float Height { get; private set; }

		/// <summary> Reset 한 번이라도 불렸는지 — 소유자가 최초 1회 초기화 판단에 쓴다. </summary>
		public bool IsInitialized { get; private set; }

		/// <summary>
		/// 상태를 통째로 되돌린다 — 최초 진입 + **재시작**의 단일 경로.
		/// (재시작이 카메라만 이전 매치 위치에 남으면 "값이 리셋됐다"가 거짓말이 된다.)
		/// </summary>
		public void Reset(Vector3 focus, float yaw, float height)
		{
			Focus = focus;
			Yaw = yaw;
			Height = height;
			IsInitialized = true;
		}

		/// <summary> 입력 반영 + target transform 에 즉시 적용. </summary>
		public void Drive(in DriveInput input, in Settings settings, float deltaTime, Transform target)
		{
			Quaternion flatYaw = Quaternion.Euler(0f, Yaw, 0f);

			Vector3 forward = flatYaw * Vector3.forward;
			Vector3 right = flatYaw * Vector3.right;
			float speedMultiplier = input.SpeedMultiplier <= 0f ? 1f : input.SpeedMultiplier;
			Vector3 nextFocus = Focus + (forward * input.Move.y + right * input.Move.x) * settings.PanSpeed * speedMultiplier * deltaTime;

			if (settings.ClampFocus)
			{
				nextFocus.x = Mathf.Clamp(nextFocus.x, settings.FocusCenter.x - settings.FocusLimit, settings.FocusCenter.x + settings.FocusLimit);
				nextFocus.z = Mathf.Clamp(nextFocus.z, settings.FocusCenter.z - settings.FocusLimit, settings.FocusCenter.z + settings.FocusLimit);
			}
			Focus = nextFocus;

			Yaw += input.Rotate * settings.YawSpeed * deltaTime;

			// 휠 = 높이(=줌). 가까이 = 확대이므로 스크롤 + 에 높이가 줄어야 한다.
			Height = Mathf.Clamp(Height - input.ScrollDelta * settings.ZoomSpeed, settings.MinHeight, settings.MaxHeight);

			Apply(settings, target);
		}

		/// <summary> 입력 없이 현재 상태만 transform 에 반영 — Reset 직후 첫 프레임 튐 방지. </summary>
		public void Apply(in Settings settings, Transform target)
		{
			if (target == null)
				return;

			Quaternion rotation = Quaternion.Euler(settings.FixedPitch, Yaw, 0f);
			Vector3 position = Focus - (rotation * Vector3.forward) * Height;
			target.SetPositionAndRotation(position, rotation);
		}
	}
}

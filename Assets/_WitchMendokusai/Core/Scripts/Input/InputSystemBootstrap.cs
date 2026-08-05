using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;

namespace WitchMendokusai
{
	// Android 부팅 초기 InputSystem 패키지 race 완화 shim.
	//
	// 증상: `ArgumentException: 'InputUpdateType.None' is not a valid update mask`
	// (스택: EnhancedTouch.Finger..ctor → InputStateHistory.set_updateMask).
	//
	// 근본 원인: Touchscreen native discovery (`OnNativeDeviceDiscovered`) 콜백이
	// InputSystem 이 자체 settings 를 완전히 apply 하기 전에 EnhancedTouch
	// 콜백을 트리거 → EnhancedTouch.s_PlayerState.updateMask 가 미초기화
	// (`None`) 상태로 Finger 를 생성 → InputStateHistory 의 mask setter 검증에서
	// reject. InvokeCallbacksSafe 가 catch/log 하므로 게임 로직 영향 0 (Touchscreen
	// 은 정상 등록·동작) — 로그 오염만.
	//
	// 완화: 부팅 최이른 phase(SubsystemRegistration) 에서
	// (1) InputSystem.settings 접근으로 lazy 정적 초기화 강제
	// (2) updateMode 자기 값 재대입 → 내부 ApplySettings 트리거 → mask 확정
	// (3) EnhancedTouch 가 이미 enabled 라면 cycle 로 s_PlayerState 리셋
	//     (WM 자체는 EnhancedTouch API 미사용 — 다른 패키지 활성화 케이스만
	//     ref-count 원복 방식으로 안전 처리).
	//
	// 첫 발동 로그는 InputSystem 패키지 자체 초기화 순서 이슈라 완전 제거는
	// 패키지 patch 필요. 본 shim 은 이후 device add/re-add 이벤트를 안전화하고,
	// 재발 시나리오(재접속·화면 회전 등)의 회귀를 차단.
	public static class InputSystemBootstrap
	{
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void HardenAtBoot()
		{
			InputSettings inputSettings = InputSystem.settings;
			if (inputSettings == null)
				return;

			inputSettings.updateMode = inputSettings.updateMode;

			if (EnhancedTouchSupport.enabled)
			{
				EnhancedTouchSupport.Disable();
				EnhancedTouchSupport.Enable();
			}
		}
	}
}

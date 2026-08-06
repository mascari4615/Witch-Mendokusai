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

			// ★ 진짜 원인 (2026-08-07, 패키지 원본 읽고 확정):
			//   `EnhancedTouchSupport.Enable()` 은 **먼저 기기 알림을 구독하고 그 다음에** 내부 상태를
			//   채운다. 그 사이에 안드로이드가 「터치스크린 생겼다」를 던지면, 아직 안 채워진 값을 읽고
			//   터진다. 즉 *늦게 켤수록* 위험하다 — 켜는 순간 기기가 이미 있거나 막 들어오기 때문이다.
			//
			//   그래서 **기기가 하나도 없는 가장 이른 시점에 우리가 먼저 켠다.** 그러면 상태가 조용히
			//   채워지고, 나중에 누가 또 켜도 그건 세기만 늘리고 지나간다(내부가 참조 세기라 안전).
			//   껐다 켜는 옛 방식은 *이미 켜져 있을 때만* 돌아서, 정작 첫 발생을 못 막았다.
			//
			//   폰에서만 나던 예외라 실기 로그가 없었으면 원인을 못 짚었다 (TASK-WM-201 의 값).
			if (EnhancedTouchSupport.enabled == false)
			{
				EnhancedTouchSupport.Enable();
			}
		}
	}
}

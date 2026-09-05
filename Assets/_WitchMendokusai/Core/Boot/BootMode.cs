using System;
using UnityEngine;

namespace WitchMendokusai
{
    /// <summary>
    /// TASK-WM-118 B3 — 결정적/헤드리스 부팅 모드.
    ///
    /// I0-B 가 매핑한 부팅 비결정 3요인: UseIntro(타이머 패널) / AutoStart(수동
    /// 버튼) / DataManager.Login(PlayFab 네트워크 critical-path, UseLocalData
    /// 무관 무조건). Deterministic 모드 = 셋을 결정 분기로 고정 → 부팅이
    /// *네트워크·타이머·수동입력 없이* WorldReady 도달 = 테스트 가능
    /// (TASK-WM-117 I5 standalone smoke 가 이 모드로 빌드 실행 + WorldReady
    /// 센티넬 회귀 판정 → 테스트불가 최종 해소, TASK-WM-116 퀄리티 first-use).
    ///
    /// 소스: 환경변수 `WM_BOOT_DETERMINISTIC` (1/true) = CI/standalone/batchmode
    /// 훅 (스모크 러너가 set 후 빌드 실행). 또는 OverrideForEditorTest (에디터
    /// 테스트 in-process). 1회 계산.
    /// </summary>
    public static class BootMode
    {
        /// <summary>에디터 in-process 테스트용 강제 override (env 보다 우선). null = env 따름.</summary>
        public static bool? OverrideForEditorTest { get; set; }

        /// <summary>에디터 솔로 dev 편의 — 타이틀 자동 스킵(AutoStart) 강제. 결정부팅과 독립(런타임 인스턴스 한정).
        /// AutoSkipTitleEditorToggle 메뉴가 SessionState 로 set. 비결정 부팅서만 적용(결정부팅은 이미 AutoStart=true).</summary>
        public static bool EditorAutoSkipTitle { get; set; }

        private static bool? _cached;

        public static bool IsDeterministic
        {
            get
            {
                if (OverrideForEditorTest.HasValue)
                {
                    return OverrideForEditorTest.Value;
                }
                if (_cached.HasValue)
                {
                    return _cached.Value;
                }
                string env = Environment.GetEnvironmentVariable("WM_BOOT_DETERMINISTIC");
                _cached = env == "1" || string.Equals(env, "true", StringComparison.OrdinalIgnoreCase);
                if (_cached.Value)
                {
                    Debug.Log("[BOOT] BootMode = Deterministic (env WM_BOOT_DETERMINISTIC) "
                        + "— Intro skip / AutoStart / PlayFab offline.");
                }
                return _cached.Value;
            }
        }
    }
}

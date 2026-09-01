using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.EditorTools
{
    /// <summary>
    /// TASK-WM-118 B3b — 에디터 in-process 결정적 부팅 토글.
    ///
    /// I5(WM-117 standalone smoke)는 빌드+env(WM_BOOT_DETERMINISTIC)로 결정 부팅을
    /// 검증하지만, *부팅 init-order 진단/회귀 피드백 루프*는 에디터 Play 가 가장 빠르고
    /// 선명하다(process.md § 가설 박기 X — 피드백 루프 먼저). [InitializeOnLoad] 정적
    /// 생성자는 매 도메인 리로드(= Play 진입 리로드 포함) 후, RuntimeInitializeOnLoad
    /// (AppSetting.OnBooting, BeforeSceneLoad) *이전*에 실행 → 이 시점에
    /// BootMode.OverrideForEditorTest 를 세팅하면 결정 부팅이 에디터에서 재현된다.
    /// SessionState 라 도메인 리로드/Play 토글에도 토글값 유지(에디터 세션 한정).
    ///
    /// 결정 = OverrideForEditorTest=true → IsDeterministic → AppSettings.ApplyDeterministicBoot
    /// (UseIntro=false, AutoStart=true, UseLocalData=true). 비결정 복귀 = override=null.
    /// </summary>
    [InitializeOnLoad]
    public static class BootModeEditorOverride
    {
        private const string KEY = "WM_BOOT_DETERMINISTIC_EDITOR";

        static BootModeEditorOverride()
        {
            if (SessionState.GetBool(KEY, false))
            {
                BootMode.OverrideForEditorTest = true;
            }
        }

        [MenuItem("WM/Boot/Deterministic Override (Editor) - Toggle")]
        private static void Toggle()
        {
            bool next = SessionState.GetBool(KEY, false) == false;
            SessionState.SetBool(KEY, next);
            BootMode.OverrideForEditorTest = next ? true : (bool?)null;
            Debug.Log($"[BOOT] Editor deterministic override = {next} "
                + "(다음 Play 부팅부터 적용 — UseIntro=false/AutoStart=true/UseLocalData=true)");
        }

        [MenuItem("WM/Boot/Deterministic Override (Editor) - Status")]
        private static void Status()
        {
            Debug.Log($"[BOOT] Editor deterministic override (SessionState) = "
                + $"{SessionState.GetBool(KEY, false)} / BootMode.IsDeterministic = {BootMode.IsDeterministic}");
        }
    }
}

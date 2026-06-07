using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.EditorTools
{
    /// <summary>
    /// 솔로 dev 편의 — 타이틀 자동 스킵(AutoStart) per-session 토글. 디스크 .asset 불변(AutoStart=0 배포 정합
    /// 유지, footgun 0). BootModeEditorOverride 동형: [InitializeOnLoad] static ctor 가 RuntimeInitializeOnLoad
    /// (AppSetting.OnBooting) *이전* 실행 → BootMode.EditorAutoSkipTitle set → OnBooting 이 런타임 인스턴스에
    /// AutoStart=true 적용. SessionState 라 도메인 리로드/Play 토글에도 유지(에디터 세션 한정).
    /// 멀티 테스트(타이틀 「멀티」 버튼)엔 OFF 로 두고, 솔로 반복 dev 시 ON 으로 타이틀 스킵.
    /// </summary>
    [InitializeOnLoad]
    public static class AutoSkipTitleEditorToggle
    {
        private const string KEY = "WM_AUTOSKIP_TITLE_EDITOR";

        static AutoSkipTitleEditorToggle()
        {
            BootMode.EditorAutoSkipTitle = SessionState.GetBool(KEY, false);
        }

        [MenuItem("WM/Boot/Auto-Skip Title (Editor) — Toggle")]
        private static void Toggle()
        {
            bool next = SessionState.GetBool(KEY, false) == false;
            SessionState.SetBool(KEY, next);
            BootMode.EditorAutoSkipTitle = next;
            Debug.Log($"[BOOT] Editor Auto-Skip Title = {next} (다음 Play 부팅부터 — AutoStart 강제, .asset 불변)");
        }

        [MenuItem("WM/Boot/Auto-Skip Title (Editor) — Status")]
        private static void Status()
        {
            Debug.Log($"[BOOT] Editor Auto-Skip Title (SessionState) = {SessionState.GetBool(KEY, false)}");
        }
    }
}

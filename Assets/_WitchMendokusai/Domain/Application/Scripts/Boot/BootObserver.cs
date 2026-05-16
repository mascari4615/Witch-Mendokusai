using UnityEngine;

namespace WitchMendokusai
{
    /// <summary>
    /// TASK-WM-118 B1 — 흩어진 부팅(Bootstrap→Intro→Lobby→Loading→World scope,
    /// I0-B 지도)을 *명시 phase 로 관측 가능* 하게 하는 비파괴 seam.
    ///
    /// 현재 부팅 로직은 5+ 독립 MonoBehaviour 생명주기에 분산 + 관측 = ad-hoc
    /// Debug.Log. B1 = 단일 phase 어휘 + 중앙 emit(`[BOOT]` prefix, frame/time).
    /// 동작 무변경(로그만). B2 가 이 seam 을 BootSequencer 로 형식화, I5(standalone
    /// smoke)가 `WorldReady` 센티넬을 회귀 판정점으로 사용.
    /// </summary>
    public enum BootPhase
    {
        RootContainerBuilt = 0, // RootLifetimeScope build callback 완료
        Intro = 1,              // Intro 씬 IntroManager.Start
        Lobby = 2,              // Lobby 씬 LobbyManager.Start
        DataReady = 3,          // DataManager.Init + Login 완료, StartGame 직전
        SceneLoading = 4,       // UISceneLoading → 대상 씬 async
        WorldScopeBuilt = 5,    // World SceneLifetimeScope build callback 완료
        WorldReady = 6,         // World 조립 완료 (BindSceneConditions 후) = 부팅 완료 센티넬
    }

    public static class BootObserver
    {
        public static BootPhase Current { get; private set; } = BootPhase.RootContainerBuilt;
        public static bool ReachedWorld => Current >= BootPhase.WorldReady;

        public static void Enter(BootPhase phase)
        {
            Current = phase;
            Debug.Log($"[BOOT] {(int)phase}:{phase} @frame={Time.frameCount} @t={Time.realtimeSinceStartup:F1}");
        }
    }
}

using System;
using UnityEngine;

namespace WitchMendokusai
{
    /// <summary>
    /// TASK-WM-118 B1/B2 — 흩어진 부팅(Bootstrap→Intro→Lobby→Loading→World scope,
    /// I0-B 지도)의 *권위 boot-state + 순서 불변 가드*.
    ///
    /// B1: 5+ 분산 MonoBehaviour 생명주기 부팅을 단일 phase 어휘 + 중앙 [BOOT] emit
    ///     으로 관측가능화 (비파괴, 로그만).
    /// B2: 단조(monotonic) phase 진행 *불변 가드* — 역행/임계-스킵이면 [BOOT-ORDER]
    ///     명시 에러(BootGuard 의 phase 판: 부팅 순서 회귀를 조용한 잠복 X, 명시 차단).
    ///     단일 권위 상태(Current/ReachedWorld) + OnPhaseEntered/OnBootComplete 이벤트
    ///     = ad-hoc "게임 준비됨?" 체크 대체 + I5(standalone smoke) 회귀 판정 훅.
    ///     제어흐름 무변경(여전히 계측점이 구동) — 형식화만 (B3 가 헤드리스/결정 진입).
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

        /// <summary>매 phase 진입 시. arg = 진입한 phase.</summary>
        public static event Action<BootPhase> OnPhaseEntered = delegate { };
        /// <summary>WorldReady 도달(부팅 완료) 1회. I5 회귀 판정 훅.</summary>
        public static event Action OnBootComplete = delegate { };

        // B2 단조 가드용 — 직전 phase. 초기 = -1 (첫 진입이 RootContainerBuilt(0) 검증).
        private static int _prev = -1;
        private static bool _completeFired;

        public static void Enter(BootPhase phase)
        {
            int p = (int)phase;

            // RootContainerBuilt = 부팅 시작 → 상태 리베이스. domain-reload off 재Play /
            // 다중 부팅 시 static 잔존으로 인한 false [BOOT-ORDER] 방지.
            if (phase == BootPhase.RootContainerBuilt)
            {
                _prev = -1;
                _completeFired = false;
            }

            // B2 순서 불변 가드: 단조 비감소만 정상. 역행 = 부팅 순서 회귀 → 명시 차단
            // (조용한 잠복 X, FastFail 정합 — BootGuard 와 동근).
            if (p < _prev)
            {
                Debug.LogError(
                    $"[BOOT-ORDER] phase 역행 — {(BootPhase)_prev}({_prev}) 후 {phase}({p}). "
                    + "부팅 순서 회귀(분산 부팅점이 잘못된 순서로 진입). TASK-WM-118 B2 가드.");
            }
            // 임계 스킵(첫 진입이 RootContainerBuilt 아님 = 조립루트 누락) 도 명시.
            else if (_prev == -1 && phase != BootPhase.RootContainerBuilt)
            {
                Debug.LogError(
                    $"[BOOT-ORDER] 첫 phase 가 {phase} (RootContainerBuilt 아님) — "
                    + "조립 루트 미진입/누락 의심. TASK-WM-118 B2 가드.");
            }

            _prev = p;
            Current = phase;
            Debug.Log($"[BOOT] {p}:{phase} @frame={Time.frameCount} @t={Time.realtimeSinceStartup:F1}");

            OnPhaseEntered(phase);
            if (phase == BootPhase.WorldReady && _completeFired == false)
            {
                _completeFired = true;
                OnBootComplete();
            }
        }
    }
}

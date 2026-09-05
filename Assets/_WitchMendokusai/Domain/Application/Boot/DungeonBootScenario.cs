using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WitchMendokusai
{
    /// <summary>
    /// TASK-WM-117 Tier-B — 헤드리스 검증 사다리를 *부팅 너머 게임로직 회귀* 로
    /// 확장하는 첫 시나리오: NPC→던전 enter/exit 와이어.
    ///
    /// WHY: WM-115 에서 NPC→던전 진입 흐름(StartDungeon→InitDungeonAndPlayer 의
    /// monsterSpawner/cardManager/dungeonUI/playerProvider.Current 체인 = R4/R5)
    /// 이 통째로 깨졌는데 *어떤 자동 테스트도 미탐지*. 부팅 smoke(WM-118 I5)는
    /// WorldReady 까지만 — 그 너머 게임로직 회귀가 검증 공백. 본 시나리오가
    /// 그 enter/exit 와이어를 결정·헤드리스로 구동해 회귀망에 편입.
    ///
    /// 설계: BootSmokeSentinel 의 정적 delegate seam 에 self-install (인터페이스
    /// X — 1 시나리오 speculative interface = 데드 인터페이스; 후속 Effect/전투
    /// 시나리오도 같은 seam 재사용 = lean). 결정 모드 ∧ env
    /// WM_BOOT_SCENARIO=="dungeon" 일 때만 등록 — 아니면 *완전 inert*
    /// (일반 플레이/부팅-only smoke 0 영향). 모든 대기 = realtime-deadline
    /// self-bound → hang 0 (센티넬 단일 quit 권위 보존).
    ///
    /// 판정 = "harness" 성격이라 예외→FAIL-verdict 변환이 올바름(FastFail 룰의
    /// 적용외: 증상 은폐가 아니라 *회귀를 명시 verdict 로 포착*).
    /// </summary>
    public static class DungeonBootScenario
    {
        private const string SCENARIO_NAME = "dungeon";
        private const float ENTER_TIMEOUT_SEC = 30f; // StartDungeon→IsDungeon=true (씬로드+init)
        private const float EXIT_TIMEOUT_SEC = 10f;  // EndDungeon→IsDungeon=false
        private const int SETTLE_FRAMES = 30;        // 던전 루프 1+ 틱 안정

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Install()
        {
            if (BootMode.IsDeterministic == false)
            {
                return;
            }
            string scenario = Environment.GetEnvironmentVariable("WM_BOOT_SCENARIO");
            if (string.Equals(scenario, SCENARIO_NAME, StringComparison.OrdinalIgnoreCase) == false)
            {
                return;
            }

            BootSmokeSentinel.ScenarioRoutine = Run;
            BootSmokeSentinel.ScenarioName = SCENARIO_NAME;
            Debug.Log($"[BOOT-SMOKE] scenario '{SCENARIO_NAME}' 등록 — WorldReady 후 던전 enter/exit 구동");
        }

        private static IEnumerator Run()
        {
            int nreStart = BootSmokeSentinel.CurrentNreCount;

            DungeonManager dungeonManager = DungeonManager.Instance;
            if (dungeonManager == null)
            {
                BootSmokeSentinel.ReportScenario(false,
                    "DungeonManager.Instance null at WorldReady (Singleton Awake 미실행 / 씬 미배치)");
                yield break;
            }

            Dungeon target = null;
            string selectError = null;
            try
            {
                List<Dungeon> dungeons = new();
                SOHelper.ForEach<Dungeon>(dungeon =>
                {
                    if (dungeon != null)
                    {
                        dungeons.Add(dungeon);
                    }
                });
                // 결정적 "첫 던전" = ID 오름차순 (DevDataLookup.SuggestRefs 와 동일 정렬).
                target = dungeons.OrderBy(dungeon => dungeon.ID).FirstOrDefault();
                if (target == null)
                {
                    selectError = "Dungeon SO 카탈로그 비어있음 (SOManager 미빌드 / 콘텐츠 0)";
                }
            }
            catch (Exception ex)
            {
                selectError = $"Dungeon SO 카탈로그 read 예외: {ex.GetType().Name} {ex.Message}";
            }
            if (target == null)
            {
                BootSmokeSentinel.ReportScenario(false, selectError);
                yield break;
            }

            Debug.Log($"[BOOT-SMOKE] dungeon scenario target = id={target.ID} name='{target.Name}'");

            // ── enter: StartDungeon (UI 우회 = StartDungeonCommand 와 동일 경로) ──
            try
            {
                dungeonManager.StartDungeon(target);
            }
            catch (Exception ex)
            {
                BootSmokeSentinel.ReportScenario(false,
                    $"StartDungeon 동기 예외: {ex.GetType().Name} {ex.Message} (enter 와이어 회귀 R4/R5 클래스)");
                yield break;
            }

            // IsDungeon=true 는 uiManager.Transition...Forget() *내부 비동기* set
            // → realtime-deadline 폴링 (동기 단언 불가).
            float enterDeadline = Time.realtimeSinceStartup + ENTER_TIMEOUT_SEC;
            while (dungeonManager.IsDungeon == false && Time.realtimeSinceStartup < enterDeadline)
            {
                yield return null;
            }
            if (dungeonManager.IsDungeon == false)
            {
                BootSmokeSentinel.ReportScenario(false,
                    $"StartDungeon 후 {ENTER_TIMEOUT_SEC}s 내 IsDungeon!=true — "
                    + "enter 와이어 회귀 (R4/R5 클래스: monsterSpawner/cardManager/dungeonUI/playerProvider 체인)");
                yield break;
            }

            for (int i = 0; i < SETTLE_FRAMES; i++)
            {
                yield return null;
            }

            if (BootSmokeSentinel.CurrentNreCount > nreStart)
            {
                BootSmokeSentinel.ReportScenario(false,
                    $"던전 enter 중 NRE 발생 (delta={BootSmokeSentinel.CurrentNreCount - nreStart})");
                yield break;
            }

            // ── exit: EndDungeon ──────────────────────────────────────────
            try
            {
                dungeonManager.EndDungeon();
            }
            catch (Exception ex)
            {
                BootSmokeSentinel.ReportScenario(false,
                    $"EndDungeon 동기 예외: {ex.GetType().Name} {ex.Message} (exit 와이어 회귀)");
                yield break;
            }

            float exitDeadline = Time.realtimeSinceStartup + EXIT_TIMEOUT_SEC;
            while (dungeonManager.IsDungeon && Time.realtimeSinceStartup < exitDeadline)
            {
                yield return null;
            }
            if (dungeonManager.IsDungeon)
            {
                BootSmokeSentinel.ReportScenario(false,
                    $"EndDungeon 후 {EXIT_TIMEOUT_SEC}s 동안 IsDungeon 가 true 고정 — exit 와이어 회귀");
                yield break;
            }

            if (BootSmokeSentinel.CurrentNreCount > nreStart)
            {
                BootSmokeSentinel.ReportScenario(false,
                    $"던전 exit 중 NRE 발생 (delta={BootSmokeSentinel.CurrentNreCount - nreStart})");
                yield break;
            }

            BootSmokeSentinel.ReportScenario(true,
                $"dungeon enter/exit OK (id={target.ID} name='{target.Name}')");
        }
    }
}

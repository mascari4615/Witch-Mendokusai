using System.Collections;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace WitchMendokusai.Tests
{
    /// <summary>
    /// 부팅 핵심 흐름 smoke (TASK-WM-117 Tier-A first-use).
    ///
    /// 배경: TASK-WM-115 스레드가 폭로한 갭 — WM-078 가 "기반 done" 선언했으나
    /// 부팅 NRE 9건 + NPC→던전 진입 통째 깨짐(R4/R5)이 *어떤 자동 테스트에도*
    /// 안 잡혔다. 검증이 부팅 콘솔 수동 확인까지였고 행동 회귀 자동화 = 0.
    /// 본 테스트 = 그 스레드에서 *수동으로* 한 검증의 코드화 = 회귀 자동탐지망.
    ///
    /// Tier-A (본 파일): 프로덕션 부팅 경로(Intro→Lobby→Loading→World)가
    ///   - 예상치 못한 Exception/Error 0 (프로젝트밖 benign missing-script 1종만 allowlist
    ///     — TASK-WM-116/MissingScriptGuardTest 가 프로젝트 에셋은 별도 가드)
    ///   - World 도달 + PlayerProvider.CurrentObject 바인드(= R3a/R5 류 회귀망)
    ///   - 핵심 매니저(UIManager/DungeonManager) 부팅 와이어
    /// 를 만족하는지. R2/R3a/R3b/R1(부팅 NRE 4종)은 이 망에 걸린다.
    ///
    /// Tier-B (미구현, TASK-WM-117 다음 증분): NPC→던전 enter/exit 흐름 직접 구동
    ///   (R4/R5 = UI 입력/Dungeon SO 커플링이라 헤드리스 구동 별도 설계 필요).
    ///   본 Tier-A 가 R4/R5 를 *직접* 커버한다고 주장하지 않음 — 정직히 다음 증분.
    ///
    /// 주의(정직): Intro 경로는 PlayFab 로그인/타이머 포함 → 네트워크/타이밍 민감.
    /// CI 무네트워크 환경에서 flaky 가능 → TASK-WM-117 에 환경 가드 후속 명시.
    /// </summary>
    public sealed class BootCoreFlowSmokeTest
    {
        private readonly List<string> _unexpected = new List<string>();

        private static bool IsAllowlisted(string condition)
        {
            // 프로젝트 직렬화 에셋 밖(PackageCache/외부)에서 나오는 native missing-script.
            // TASK-WM-116: 전 Assets 766 에셋 GUID-resolve 0 으로 프로젝트밖 확정 + benign.
            // 프로젝트 에셋의 missing-script 는 MissingScriptGuardTest(EditMode)가 별도 fail.
            return condition != null
                && condition.Contains("referenced script")
                && condition.Contains("missing");
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error)
            {
                return;
            }
            if (IsAllowlisted(condition))
            {
                return;
            }
            _unexpected.Add("[" + type + "] " + condition);
        }

        [UnityTest]
        public IEnumerator Boot_Reaches_World_NoUnexpectedErrors_PlayerBound()
        {
            _unexpected.Clear();
            Application.logMessageReceived += OnLog;

            try
            {
                SceneManager.LoadScene("Intro");

                const float timeoutSeconds = 180f;
                float start = Time.realtimeSinceStartup;
                while (SceneManager.GetActiveScene().name != "World")
                {
                    if (Time.realtimeSinceStartup - start > timeoutSeconds)
                    {
                        Assert.Fail("부팅이 " + timeoutSeconds + "s 안에 World 에 도달 못함 "
                            + "(현재 활성 씬='" + SceneManager.GetActiveScene().name + "'). "
                            + "Intro→Lobby→Loading→World 체인 또는 PlayFab/DataManager 정지 의심.");
                    }
                    yield return null;
                }

                // 첫프레임 ordering transient 가 자체 해소될 시간 (WM-115 R3a 류).
                float settleStart = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - settleStart < 6f)
                {
                    yield return null;
                }

                // 1) 부팅 회귀망 — 예상치 못한 Exception/Error 0 (R2/R3a/R3b/R1 류).
                if (_unexpected.Count > 0)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine(_unexpected.Count + " 건의 예상치 못한 런타임 Exception/Error "
                        + "(allowlist=프로젝트밖 missing-script 만):");
                    foreach (string e in _unexpected)
                    {
                        sb.AppendLine("  " + e);
                    }
                    Assert.Fail(sb.ToString());
                }

                // 2) 플레이어 바인드 (R3a/R5 류 — playerProvider.CurrentObject null 회귀망).
                PlayerProvider playerProvider =
                    Object.FindAnyObjectByType<PlayerProvider>(FindObjectsInactive.Include);
                Assert.IsNotNull(playerProvider, "PlayerProvider 가 씬에 없음 (DI/부팅 와이어 회귀).");
                Assert.IsNotNull(playerProvider.CurrentObject,
                    "playerProvider.CurrentObject == null — 플레이어 미스폰/미바인드 "
                    + "(WM-115 R3a/R5 회귀). Player.Construct→SetCurrent 경로 확인.");

                // 3) 핵심 매니저 부팅 와이어.
                Assert.IsNotNull(Object.FindAnyObjectByType<UIManager>(FindObjectsInactive.Include),
                    "UIManager 부팅 와이어 회귀.");
                Assert.IsNotNull(Object.FindAnyObjectByType<DungeonManager>(FindObjectsInactive.Include),
                    "DungeonManager 부팅 와이어 회귀 (NPC→던전 경로 전제).");
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
            }
        }
    }
}

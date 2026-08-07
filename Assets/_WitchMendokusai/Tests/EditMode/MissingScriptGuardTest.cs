using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.Tests
{
    /// <summary>
    /// 프로젝트 프리팹·씬·ScriptableObject 에 "missing script" 가 0 인지 검증.
    ///
    /// 회귀 배경: 마이그레이션/asmdef 이동이 .cs 를 삭제·이동했는데 딸린 프리팹·씬 GO·
    /// SO 의 m_Script 가 끊겨 매 Play "The referenced script (Unknown) on this Behaviour
    /// is missing!" 가 출력됐다 (code-style.md § 마이그레이션 자기 소멸 / deletion test
    /// 의 Unity 에셋 그래프 확장, unity.md § missing-script 고아).
    ///
    /// 판정은 *Unity API ground-truth* 로만 한다 (텍스트+GUIDToAssetPath 휴리스틱은
    /// Unity 빌트인 guid `0000…e000…` / 패키지·서브에셋 스크립트에 false-positive 를
    /// 내 CI 를 헛fail 시킴 — 2026-05-16 URP 렌더러 오탐으로 실증):
    ///   - 프리팹: 씬과 같은 YAML guid 판정 (2026-08-06 전환 — 예전엔
    ///     GameObjectUtility.GetMonoBehavioursWithMissingScriptCount 로 *개수만* 세서
    ///     서드파티와 진짜 사고를 못 갈랐고, 그래서 Bakery 없는 환경에선 통째로 skip 됐다)
    ///   - 씬: 텍스트 파싱하되 빌트인 guid 제외 + 해소 가능 guid 제외 (씬은 안 엶 =
    ///         비파괴·결정적; 씬 MonoBehaviour 의 패키지 스크립트 guid 는 AssetDatabase
    ///         에 정상 등재돼 해소되므로 오탐 없음).
    ///   - SO(.asset): 로드 후 null 서브에셋 또는 SerializedObject.m_Script == null
    ///     (SkyPreset 류 — m_EditorClassIdentifier fallback 으로 로드는 되나 m_Script
    ///     끊긴 케이스. 2026-05-16 SkyPreset_AnimalCrossing 실증).
    /// </summary>
    public sealed class MissingScriptGuardTest
    {
        // Unity 빌트인 리소스 GUID — missing 아님 (PanelSettings/TMP SDF/URP/BuildProfile 등 빌트인 타입 참조)
        private const string UNITY_BUILTIN_GUID = "0000000000000000e000000000000000";

        /// <summary>
        /// git 에 올리지 않는(유료·외부) 에셋의 스크립트 GUID — 개발 머신엔 있고 CI 체크아웃엔 없다.
        ///
        /// 왜 GUID 로 보는가 (2026-08-06, 두 번 헛짚고 얻은 것): 처음엔 "폴더가 있나"로 판정했는데
        /// 두 번 다 발화하지 않았다(상대 경로 → 작업 디렉토리 의존, 절대 경로 → 여전히 skipped=0).
        /// 폴더 위치·작업 디렉토리·설치 방식은 환경마다 다르지만, **GUID 는 에셋의 신분증이라 어디서든 같다**.
        /// 그래서 "이 GUID 가 이 체크아웃에서 해소되나"만 묻는다 — 판정의 근거를 환경에서 떼어낸다.
        ///
        /// 실증: Bakery(라이트맵 툴) 컴포넌트가 붙은 조명이 스테이지 프리팹·씬에 들어 있어,
        /// Bakery 없는 CI 에선 프리팹 19곳·씬 35곳이 한꺼번에 죽은 참조로 보였다. 개발 머신에선 멀쩡하다.
        ///
        /// 거짓 빨간불은 게이트를 죽인다(사람이 "또 그거네" 하고 무시하면 진짜 사고도 묻힌다).
        /// 그렇다고 조용히 통과시키면 검사가 없는 것과 같다 → **왜 건너뛰는지 말하면서 건너뛴다.**
        /// </summary>
        private static readonly Dictionary<string, string> OPTIONAL_THIRD_PARTY_SCRIPT_GUIDS =
            new Dictionary<string, string>
            {
                { "ec0b4dd729a12d046982652f834580a2", "Bakery / BakeryLightmapGroup" },
                { "b7fa80e7116296f4eb4f49ec1544ee22", "Bakery / ftLightmapsStorage" },

                // ★ 아래 셋은 **프리팹 검사를 실제로 돌리자마자** 나왔다 (2026-08-06 후속).
                //   씬은 위 둘만 있으면 통과했는데, 프리팹은 조명 컴포넌트를 더 쓴다 —
                //   즉 통째 skip 하던 동안 이 셋은 목록에 오를 기회조차 없었다.
                //   셋 다 WM-137 절차대로 **main 체크아웃에서 guid 를 되짚어** 확인했다:
                //   전부 `Assets/Bakery/*.cs` 로 해소된다(= 의도적 gitignore 서드파티).
                //   → 진짜 실종 스크립트는 **0건**이었다. 프로젝트는 이 부류에서 깨끗하다.
                { "c74ce2158ae608549902afb4112fd042", "Bakery / BakeryDirectLight" },
                { "57f24a4aaa0761b45ba25e7e5108e2c7", "Bakery / BakeryPointLight" },
                { "306a56f30ff21b5439963fc745cfe9cc", "Bakery / BakerySkyLight" },
            };

        [Test]
        public void AllPrefabAssets_HaveNoMissingScripts()
        {
            // ★ 여기도 **건너뛰지 않는다** (2026-08-06 후속, TASK-WM-137 나머지 절반).
            //
            //   예전엔 `GameObjectUtility.GetMonoBehavioursWithMissingScriptCount` 로 **개수만** 셌다.
            //   개수엔 guid 가 없어서 「Bakery 라서 안 풀리는 것」과 「진짜 깨진 것」을 못 가른다
            //   → 통째로 skip 할 수밖에 없었고, 그래서 **Bakery 없는 환경(CI·fresh worktree)에선
            //   이 검사가 영영 안 돌았다.** 초록인데 아무도 안 본 상태다.
            //
            //   씬 쪽은 YAML 에서 guid 를 직접 읽어 서드파티만 골라 넘기는 방식으로 이미 고쳤다.
            //   프리팹도 같은 YAML 이므로 같은 방법이 그대로 통한다 — 그래서 옮겼다.
            //   (`.prefab` 은 텍스트 직렬화 전제. 이 레포는 ForceText 라 성립한다.)
            Regex scriptGuid = new Regex(@"m_Script:.*guid:\s*([0-9a-f]{32})");
            List<string> offenders = new List<string>();
            int scanned = 0;
            int scriptRefs = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Packages/"))
                {
                    continue;
                }
                string full = Path.GetFullPath(path);
                if (File.Exists(full) == false)
                {
                    continue;
                }

                scanned++;
                foreach (string line in File.ReadAllLines(full))
                {
                    Match match = scriptGuid.Match(line);
                    if (match.Success == false)
                    {
                        continue;
                    }
                    scriptRefs++;
                    string guidValue = match.Groups[1].Value;
                    if (guidValue == UNITY_BUILTIN_GUID)
                    {
                        continue;
                    }
                    // 의도적 gitignore 서드파티(Bakery 등) — 이 체크아웃에 없는 게 **정상**이다.
                    if (OPTIONAL_THIRD_PARTY_SCRIPT_GUIDS.ContainsKey(guidValue))
                    {
                        continue;
                    }
                    string scriptPath = AssetDatabase.GUIDToAssetPath(guidValue);
                    bool resolves = string.IsNullOrEmpty(scriptPath) == false
                        && AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath) != null;
                    if (resolves == false)
                    {
                        offenders.Add(path + "  ->  dead m_Script guid=" + guidValue);
                    }
                }
            }

            // ★ 「대상 0건 = 통과」 방지 두 겹.
            //   ① 파일을 못 읽은 경우
            Assert.Greater(
                scanned,
                50,
                $"프리팹을 {scanned}개밖에 못 읽었다 — 위반이 없는 게 아니라 스캔이 깨진 것으로 본다.");

            //   ② 읽긴 했는데 **파싱이 안 된** 경우. 직렬화가 ForceText 가 아니게 되면(바이너리)
            //      정규식이 한 줄도 안 맞아 offenders 0 = 거짓 초록이 된다. 파일 수만 세는 ①로는 못 잡는다.
            //      프리팹 50개를 읽고 `m_Script` 가 0건일 수는 없다.
            Assert.Greater(
                scriptRefs,
                0,
                $"프리팹 {scanned}개를 읽었는데 `m_Script` 참조가 0건이다 — 텍스트 직렬화가 아니거나 파싱이 깨졌다.\n" +
                "이 상태의 「위반 0」은 「깨끗함」이 아니라 「아무것도 못 봄」이다.");

            AssertNoOffenders(offenders, "prefab");
        }

        [Test]
        public void AllProjectScenes_HaveNoMissingScripts()
        {
            // ★ 여기선 **건너뛰지 않는다** (2026-08-06, TASK-WM-137).
            //   이 검사는 씬 YAML 에서 guid 를 직접 읽으므로 「어느 guid 가 안 풀리는지」를 안다
            //   → Bakery 처럼 의도적으로 gitignore 된 서드파티 guid 만 **골라서** 넘기면 된다.
            //   프리팹 쪽도 같은 이유로 오래 skip 했는데, 같은 YAML 방식으로 옮겨서 이제 함께 돈다
            //   (2026-08-06 후속). 즉 **이 파일의 세 검사 모두 더는 통째 skip 하지 않는다.**
            //
            //   왜 바꿨나: 통째로 skip 하면 Bakery 없는 환경(CI·fresh worktree)에서 **이 검사가
            //   영영 안 돈다.** 초록인데 아무도 안 본 상태 = 검사가 없는 것과 같다.
            Regex scriptGuid = new Regex(@"m_Script:.*guid:\s*([0-9a-f]{32})");
            List<string> offenders = new List<string>();

            foreach (string guid in AssetDatabase.FindAssets("t:Scene"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Packages/"))
                {
                    continue;
                }
                string full = Path.GetFullPath(path);
                if (File.Exists(full) == false)
                {
                    continue;
                }

                foreach (string line in File.ReadAllLines(full))
                {
                    Match match = scriptGuid.Match(line);
                    if (match.Success == false)
                    {
                        continue;
                    }
                    string guidValue = match.Groups[1].Value;
                    if (guidValue == UNITY_BUILTIN_GUID)
                    {
                        continue;
                    }
                    // 의도적 gitignore 서드파티(Bakery 등) — 이 체크아웃에 없는 게 **정상**이다.
                    // 없다고 빨간불을 켜면 거짓 경고가 게이트를 죽인다. 대신 나머지는 계속 본다.
                    if (OPTIONAL_THIRD_PARTY_SCRIPT_GUIDS.ContainsKey(guidValue))
                    {
                        continue;
                    }
                    string scriptPath = AssetDatabase.GUIDToAssetPath(guidValue);
                    bool resolves = string.IsNullOrEmpty(scriptPath) == false
                        && AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath) != null;
                    if (resolves == false)
                    {
                        offenders.Add(path + "  ->  dead m_Script guid=" + guidValue);
                    }
                }
            }
            AssertNoOffenders(offenders, "scene");
        }

        [Test]
        public void AllScriptableObjectAssets_HaveNoMissingScripts()
        {
            List<string> offenders = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Packages/"))
                {
                    continue;
                }

                Object[] objects = AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (Object obj in objects)
                {
                    if (obj == null)
                    {
                        offenders.Add(path + "  ->  <null sub-asset: missing script>");
                        continue;
                    }
                    if (obj is ScriptableObject == false)
                    {
                        continue;
                    }
                    SerializedProperty scriptProp = new SerializedObject(obj).FindProperty("m_Script");
                    if (scriptProp != null && scriptProp.objectReferenceValue == null)
                    {
                        offenders.Add(path + "  ->  '" + obj.name + "' m_Script == null");
                    }
                }
            }
            AssertNoOffenders(offenders, "ScriptableObject");
        }

        private static void AssertNoOffenders(List<string> offenders, string kind)
        {
            if (offenders.Count == 0)
            {
                return;
            }
            StringBuilder message = new StringBuilder();
            message.AppendLine(offenders.Count + " " + kind + "(s) have missing script references:");
            foreach (string offender in offenders)
            {
                message.AppendLine("  " + offender);
            }
            message.AppendLine("원인: 참조하던 .cs 가 삭제/이동됐는데 m_Script GUID 가 끊김.");
            message.AppendLine("처리: 죽은 고아 = WM/Cleanup 메뉴 / 데이터 살아있으면 m_Script 재바인딩 "
                + "(unity.md § missing-script 고아).");
            Assert.Fail(message.ToString());
        }
    }
}

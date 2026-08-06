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
    ///   - 프리팹: GameObjectUtility.GetMonoBehavioursWithMissingScriptCount
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
        /// git 에 올리지 않는(유료·외부) 에셋 폴더. 개발 머신엔 있고 CI 체크아웃엔 없다.
        ///
        /// 왜 필요한가 (2026-08-06, TASK-WM-203 CI 첫 완주에서 실증): Bakery 라이트맵 컴포넌트가
        /// 붙은 조명 오브젝트가 스테이지 프리팹·씬에 들어 있는데, CI 체크아웃엔 Bakery 가 없으므로
        /// 그 m_Script 가 전부 "죽은 참조"로 보인다 — 프리팹 19곳·씬 35곳이 한꺼번에 빨간불이 됐다.
        /// 개발 머신에선 멀쩡한 것들이다.
        ///
        /// 거짓 빨간불은 게이트를 죽인다(사람이 "또 그거네" 하고 무시하기 시작하면 진짜 사고도 묻힌다).
        /// 그렇다고 조용히 통과시키면 검사가 없는 것과 같다 → **왜 건너뛰는지 말하면서 건너뛴다**.
        /// 이 검사들은 에셋이 다 깔린 개발 머신에서 의미가 있고, 거기선 그대로 전수 검사한다.
        /// </summary>
        private static readonly string[] OPTIONAL_THIRD_PARTY_ROOTS =
        {
            "Bakery",
        };

        private static void SkipIfOptionalThirdPartyAssetsMissing()
        {
            // Application.dataPath 기준 절대경로로 본다 — batchmode 는 작업 디렉토리가
            // 프로젝트 루트라는 보장이 없어서, 상대경로("Assets/Bakery")로 물으면 폴더가
            // 없는데도 "있다"로 새는 게 아니라 *판정 자체가 무의미*해진다 (실측: 첫 시도가
            // 그래서 그대로 빨간불이었다).
            foreach (string folderName in OPTIONAL_THIRD_PARTY_ROOTS)
            {
                string root = Path.Combine(Application.dataPath, folderName);

                // ★ 「폴더가 있다」로도 아직 부족하다: git worktree 를 만들면 **빈 폴더만 남는다**
                //   (`Assets/Bakery/` 항목 0개 — 실측 2026-08-06 `wm-verify`). 절대경로로 물어도
                //   그건 "있다"라서 가드가 안 걸리고, 검사가 그대로 돌아 프리팹 19·씬 35건이
                //   다시 빨간불이 된다. CI 는 폴더 자체가 없어 통과하므로 **worktree 에서만
                //   조용히 깨지는** 자리였다. 있는지가 아니라 **쓸 게 들어 있는지**를 본다.
                if (Directory.Exists(root) == false
                    || Directory.EnumerateFileSystemEntries(root).GetEnumerator().MoveNext() == false)
                {
                    Assert.Ignore(
                        "이 체크아웃엔 'Assets/" + folderName + "' 가 없거나 비어 있다 (git 미추적 외부 에셋). "
                        + "그 컴포넌트를 쓰는 프리팹·씬의 참조가 전부 죽은 것처럼 보이므로 판정 불가 — "
                        + "에셋이 깔린 개발 머신에서 이 검사가 진짜로 돈다.");
                }
            }
        }

        [Test]
        public void AllPrefabAssets_HaveNoMissingScripts()
        {
            SkipIfOptionalThirdPartyAssetsMissing();

            List<string> offenders = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }
                foreach (Transform child in prefab.GetComponentsInChildren<Transform>(true))
                {
                    int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject);
                    if (missingCount > 0)
                    {
                        offenders.Add(path + "  ->  '" + child.name + "'  x" + missingCount);
                    }
                }
            }
            AssertNoOffenders(offenders, "prefab object");
        }

        [Test]
        public void AllProjectScenes_HaveNoMissingScripts()
        {
            SkipIfOptionalThirdPartyAssetsMissing();

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

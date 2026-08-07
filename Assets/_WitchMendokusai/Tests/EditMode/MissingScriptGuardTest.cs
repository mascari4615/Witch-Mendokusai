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
            };

        private static bool GuidResolves(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) == false
                && AssetDatabase.LoadAssetAtPath<MonoScript>(path) != null;
        }

        private static void SkipIfOptionalThirdPartyAssetsMissing()
        {
            foreach (KeyValuePair<string, string> entry in OPTIONAL_THIRD_PARTY_SCRIPT_GUIDS)
            {
                if (GuidResolves(entry.Key) == false)
                {
                    Assert.Ignore(
                        "이 체크아웃엔 '" + entry.Value + "' 가 없다 (git 미추적 외부 에셋). "
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

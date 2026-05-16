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
    /// 프로젝트 프리팹·씬에 "missing script" (해소 불가 m_Script GUID) 가 0 인지 검증.
    ///
    /// 회귀 배경: WM-021/025/059 등 마이그레이션이 .cs 만 삭제하고 딸린 프리팹/씬 GO 를
    /// 안 지워 죽은 GUID 가 남았고, 매 Play "The referenced script (Unknown) on this
    /// Behaviour is missing!" 가 출력됐다. 이 가드는 그 클래스를 Play 없이 EditMode/CI
    /// 에서 자동 검출한다 (code-style.md § 마이그레이션 자기 소멸 / deletion test 의 자동화,
    /// unity.md § missing-script 고아 = deletion test 미완).
    ///
    /// 프리팹: AssetDatabase 로드 후 GameObjectUtility 카운트.
    /// 씬: .unity 파일을 *텍스트 파싱* 한다 — 씬을 열지 않으므로 사용자 작업 씬을
    ///     흔들지 않고 비파괴·결정적. (씬 제외는 2026-05-16 Intro/Lobby 의 죽은
    ///     'Reporter' GO 가 Play 경고로 잔존해 갭으로 드러나 봉합됨.)
    /// 단, 읽기전용 Unity 패키지(Packages/*) 의 SceneTemplate 등은 우리 책임 밖이라 제외.
    /// </summary>
    public sealed class MissingScriptGuardTest
    {
        [Test]
        public void AllPrefabAssets_HaveNoMissingScripts()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            List<string> offenders = new List<string>();

            foreach (string guid in prefabGuids)
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
            Regex scriptGuid = new Regex(@"m_Script:.*guid:\s*([0-9a-f]{32})");
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");
            List<string> offenders = new List<string>();

            foreach (string guid in sceneGuids)
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

                    string scriptPath = AssetDatabase.GUIDToAssetPath(match.Groups[1].Value);
                    bool resolves = string.IsNullOrEmpty(scriptPath) == false
                        && AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath) != null;
                    if (resolves == false)
                    {
                        offenders.Add(path + "  ->  dead m_Script guid=" + match.Groups[1].Value);
                    }
                }
            }

            AssertNoOffenders(offenders, "scene");
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
            message.AppendLine("원인: 참조하던 .cs 가 삭제/이동됐는데 m_Script GUID 가 남음.");
            message.AppendLine("처리: WM/Cleanup 메뉴(죽은 고아 자동) 또는 컴포넌트 재바인딩 (unity.md § missing-script 고아).");
            Assert.Fail(message.ToString());
        }
    }
}

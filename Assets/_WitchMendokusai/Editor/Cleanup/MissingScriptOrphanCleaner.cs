using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WitchMendokusai.Editor.Cleanup
{
    /// <summary>
    /// "The referenced script (Unknown) on this Behaviour is missing!" 의 즉시 진단·정리 도구.
    ///
    /// 회귀 클래스: .cs 삭제/이동 시 딸린 프리팹의 m_Script GUID 가 남아 죽은 참조가 됨
    /// (code-style.md § 마이그레이션 자기 소멸 / deletion test 의 Unity 에셋 그래프 확장,
    ///  unity.md § missing-script 고아 = deletion test 미완). EditMode 가드
    /// (WM.Tests.EditMode/MissingScriptGuardTest) 가 *검출*, 본 도구가 *즉시 정리* 를 담당한다.
    ///
    /// 안전 규칙(수동 분석을 그대로 인코딩): 프리팹을 "삭제 가능한 죽은 고아" 로 보는 조건 =
    ///   ① missing MonoBehaviour ≥ 1
    ///   ② 그 m_Script GUID 가 전부 해소 불가 (잠시 미컴파일 X, 진짜 죽음)
    ///   ③ 이 프리팹을 참조하는 모든 에셋이 *역시 죽은 고아 집합 안* (= 외부 산 참조 0)
    /// 셋 다 충족만 삭제 후보. 하나라도 어긋나면 재바인딩 대상 → 수동 검토로 남긴다.
    /// </summary>
    public static class MissingScriptOrphanCleaner
    {
        private const string MENU_SCAN = "WM/Cleanup/Missing-Script 고아 스캔 (보고만)";
        private const string MENU_PRUNE = "WM/Cleanup/Missing-Script 죽은 고아 프리팹 제거";
        private const string MENU_SCENE_SCAN = "WM/Cleanup/Missing-Script 씬 스캔 (보고만)";
        private const string MENU_SCENE_STRIP = "WM/Cleanup/Missing-Script 씬 컴포넌트 제거";

        // 씬 missing-script 판정 = MissingScriptGuardTest.AllProjectScenes 와 동일 기준
        // (텍스트 m_Script guid 가 AssetDatabase 로 MonoScript 해소 불가). 동일 스코프라
        // 본 메뉴 제거 = 그 게이트 GREEN 을 결정적으로 보장.
        private const string UNITY_BUILTIN_GUID = "0000000000000000e000000000000000";

        [MenuItem(MENU_SCAN, priority = 2000)]
        public static void Scan()
        {
            List<string> withMissing = new List<string>();
            HashSet<string> deadSet = CollectPrefabsWithMissingScripts(withMissing);
            HashSet<string> safe = ComputeSafeToDelete(deadSet);

            StringBuilder report = new StringBuilder();
            report.AppendLine("[MissingScriptOrphanCleaner] missing-script 프리팹 " + deadSet.Count + "개:");
            foreach (string path in withMissing)
            {
                bool isSafe = safe.Contains(path);
                report.AppendLine((isSafe ? "  [죽은 고아·삭제가능] " : "  [외부 산 참조 有·수동검토] ") + path);
            }
            report.AppendLine("→ 삭제 가능 " + safe.Count + " / 수동 검토 " + (deadSet.Count - safe.Count));
            report.AppendLine("정리: 메뉴 \"" + MENU_PRUNE + "\"");
            Debug.Log(report.ToString());
        }

        [MenuItem(MENU_PRUNE, priority = 2001)]
        public static void Prune()
        {
            List<string> withMissing = new List<string>();
            HashSet<string> deadSet = CollectPrefabsWithMissingScripts(withMissing);
            HashSet<string> safe = ComputeSafeToDelete(deadSet);

            if (safe.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Missing-Script 정리",
                    deadSet.Count == 0
                        ? "missing-script 프리팹이 없습니다. 클린."
                        : deadSet.Count + "개 모두 외부 산 참조가 있어 자동 삭제 불가 — 수동 재바인딩 대상입니다. (스캔 메뉴로 목록 확인)",
                    "확인");
                return;
            }

            StringBuilder list = new StringBuilder();
            foreach (string path in safe)
            {
                list.AppendLine("  " + path);
            }
            bool proceed = EditorUtility.DisplayDialog(
                "Missing-Script 죽은 고아 제거",
                "아래 " + safe.Count + "개 프리팹은 죽은 GUID 만 참조하고 외부 산 참조가 0 입니다.\n"
                    + "삭제해 마이그레이션 자기소멸을 완결합니다 (git 으로 복구 가능):\n\n" + list,
                "삭제", "취소");
            if (proceed == false)
            {
                return;
            }

            int deleted = 0;
            foreach (string path in safe)
            {
                if (AssetDatabase.DeleteAsset(path))
                {
                    deleted++;
                }
                else
                {
                    Debug.LogError("[MissingScriptOrphanCleaner] 삭제 실패: " + path);
                }
            }
            AssetDatabase.Refresh();
            Debug.Log("[MissingScriptOrphanCleaner] " + deleted + "/" + safe.Count
                + " 삭제 완료. 잔여 missing-script 검증 = WM.Tests.EditMode/MissingScriptGuardTest 실행 권장.");
        }

        // ─── 씬 임베드 missing-script (프리팹 고아 삭제로 못 잡는 케이스) ───
        // 기존 Prune 은 *프리팹 에셋 통째 삭제* 만 — 씬에 박힌 죽은 MonoBehaviour
        // (예: Bakery 라이트매퍼 잔재, TASK-WM-137) 는 미커버였다. 정본 제거 =
        // Unity API GameObjectUtility.RemoveMonoBehavioursWithMissingScript (씬
        // 무결성 보장, YAML 수기 surgery X).

        [MenuItem(MENU_SCENE_SCAN, priority = 2002)]
        public static void ScanScenes()
        {
            Dictionary<string, int> sceneMissing = CollectScenesWithMissingScripts();

            StringBuilder report = new StringBuilder();
            int total = 0;
            report.AppendLine("[MissingScriptOrphanCleaner] missing-script 씬 " + sceneMissing.Count + "개:");
            foreach (KeyValuePair<string, int> entry in sceneMissing)
            {
                report.AppendLine("  x" + entry.Value + "  " + entry.Key);
                total += entry.Value;
            }
            report.AppendLine("→ 씬 죽은 컴포넌트 총 " + total + "개. 제거 메뉴: \"" + MENU_SCENE_STRIP + "\"");
            Debug.Log(report.ToString());
        }

        [MenuItem(MENU_SCENE_STRIP, priority = 2003)]
        public static void StripScenes()
        {
            Dictionary<string, int> sceneMissing = CollectScenesWithMissingScripts();
            if (sceneMissing.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Missing-Script 씬 정리", "missing-script 씬이 없습니다. 클린.", "확인");
                return;
            }

            StringBuilder list = new StringBuilder();
            int total = 0;
            foreach (KeyValuePair<string, int> entry in sceneMissing)
            {
                list.AppendLine("  x" + entry.Value + "  " + entry.Key);
                total += entry.Value;
            }
            bool proceed = EditorUtility.DisplayDialog(
                "Missing-Script 씬 컴포넌트 제거",
                "아래 " + sceneMissing.Count + "개 씬에서 죽은 m_Script 컴포넌트 " + total + "개를 제거합니다.\n"
                    + "(Unity API RemoveMonoBehavioursWithMissingScript — git 으로 복구 가능):\n\n" + list,
                "제거", "취소");
            if (proceed == false)
            {
                return;
            }

            string[] originalSetup = LoadedScenePaths();
            int strippedScenes = 0;
            int strippedComponents = 0;
            foreach (string scenePath in new List<string>(sceneMissing.Keys))
            {
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                int removed = StripMissingInScene(scene);
                if (removed > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    strippedScenes++;
                    strippedComponents += removed;
                }
            }
            RestoreScenes(originalSetup);
            AssetDatabase.Refresh();
            Debug.Log("[MissingScriptOrphanCleaner] 씬 " + strippedScenes + "개 / 컴포넌트 "
                + strippedComponents + "개 제거 완료. 검증 = WM.Tests.EditMode/MissingScriptGuardTest.");
        }

        private static Dictionary<string, int> CollectScenesWithMissingScripts()
        {
            Dictionary<string, int> result = new Dictionary<string, int>();
            Regex scriptGuid = new Regex(@"m_Script:.*guid:\s*([0-9a-f]{32})");

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

                int count = 0;
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
                        count++;
                    }
                }
                if (count > 0)
                {
                    result.Add(path, count);
                }
            }
            return result;
        }

        private static int StripMissingInScene(Scene scene)
        {
            int removed = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0)
                    {
                        removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
                    }
                }
            }
            return removed;
        }

        private static string[] LoadedScenePaths()
        {
            List<string> paths = new List<string>();
            foreach (SceneSetup setup in EditorSceneManager.GetSceneManagerSetup())
            {
                if (setup.isLoaded && string.IsNullOrEmpty(setup.path) == false)
                {
                    paths.Add(setup.path);
                }
            }
            return paths.ToArray();
        }

        private static void RestoreScenes(string[] paths)
        {
            if (paths.Length == 0)
            {
                return;
            }
            for (int i = 0; i < paths.Length; i++)
            {
                if (File.Exists(Path.GetFullPath(paths[i])) == false)
                {
                    continue;
                }
                OpenSceneMode mode = i == 0 ? OpenSceneMode.Single : OpenSceneMode.Additive;
                EditorSceneManager.OpenScene(paths[i], mode);
            }
        }

        private static HashSet<string> CollectPrefabsWithMissingScripts(List<string> orderedPaths)
        {
            HashSet<string> result = new HashSet<string>();
            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                foreach (Transform child in prefab.GetComponentsInChildren<Transform>(true))
                {
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) > 0)
                    {
                        if (result.Add(path))
                        {
                            orderedPaths.Add(path);
                        }
                        break;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// 죽은 고아 집합 중, 자신을 참조하는 에셋이 *전부 죽은 집합 안* 인 것만 삭제 안전.
        /// (외부 씬·SO·살아있는 프리팹이 하나라도 물고 있으면 제외.)
        /// </summary>
        private static HashSet<string> ComputeSafeToDelete(HashSet<string> deadSet)
        {
            HashSet<string> safe = new HashSet<string>();
            string[] allAssets = AssetDatabase.GetAllAssetPaths();

            foreach (string deadPath in deadSet)
            {
                string deadGuid = AssetDatabase.AssetPathToGUID(deadPath);
                if (string.IsNullOrEmpty(deadGuid))
                {
                    continue;
                }

                bool externalLiveRef = false;
                foreach (string asset in allAssets)
                {
                    if (asset == deadPath || deadSet.Contains(asset))
                    {
                        continue;
                    }
                    if (asset.EndsWith(".unity") == false
                        && asset.EndsWith(".prefab") == false
                        && asset.EndsWith(".asset") == false)
                    {
                        continue;
                    }

                    string full = Path.GetFullPath(asset);
                    if (File.Exists(full) == false)
                    {
                        continue;
                    }
                    if (File.ReadAllText(full).Contains(deadGuid))
                    {
                        externalLiveRef = true;
                        break;
                    }
                }

                if (externalLiveRef == false)
                {
                    safe.Add(deadPath);
                }
            }
            return safe;
        }
    }
}

using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.Tests
{
    /// <summary>
    /// 프로젝트 전체 프리팹 에셋에 "missing script" (해소 불가 m_Script GUID) 가 0 인지 검증한다.
    ///
    /// 회귀 배경: WM-021/025/059 등 uGUI→UIToolkit 마이그레이션이 .cs 만 삭제하고
    /// 딸린 uGUI 프리팹을 안 지워 죽은 GUID 가 프리팹에 박힌 채 남았고, 매 Play 마다
    /// "The referenced script (Unknown) on this Behaviour is missing!" 가 출력됐다.
    /// 이 가드는 그 클래스의 결함을 Play 없이 EditMode 에서 자동 검출한다
    /// (code-style.md § 마이그 자기소멸 / deletion test 의 자동화).
    ///
    /// 씬(.unity) 은 사용자 작업 씬을 흔들지 않도록 스캔 대상에서 제외 — 프리팹 에셋이
    /// 이번 부패가 발생한 정확한 클래스이며 결정적·부작용 0 으로 검사 가능하다.
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

            if (offenders.Count > 0)
            {
                StringBuilder message = new StringBuilder();
                message.AppendLine(offenders.Count + " prefab object(s) have missing script references:");
                foreach (string offender in offenders)
                {
                    message.AppendLine("  " + offender);
                }
                message.AppendLine("원인: 참조하던 .cs 가 삭제/이동됐는데 프리팹의 m_Script GUID 가 남음.");
                message.AppendLine("처리: 죽은 프리팹이면 삭제(마이그 자기소멸 완결), 살아있어야 하면 컴포넌트 재바인딩.");
                Assert.Fail(message.ToString());
            }
        }
    }
}

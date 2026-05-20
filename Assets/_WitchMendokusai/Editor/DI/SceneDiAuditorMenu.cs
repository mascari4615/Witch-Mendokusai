using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.Editor.DI
{
	public static class SceneDiAuditorMenu
	{
		private const string MENU_AUDIT_ALL = "WM/Audit/Scene DI Coverage (모든 씬)";
		private const string MENU_AUDIT_ACTIVE = "WM/Audit/Scene DI Coverage (활성 씬만)";

		[MenuItem(MENU_AUDIT_ALL, priority = 2100)]
		private static void AuditAllScenes()
		{
			HashSet<Type> injectConsumingTypes = SceneDiAuditor.CollectInjectConsumingTypes();
			SceneCoverageMap coverage = SceneDiAuditor.CollectSceneCoverage();

			List<SceneDiOffender> offenders = new List<SceneDiOffender>();
			int scenesScanned = 0;
			foreach (string scenePath in SceneDiAuditor.EnumerateProjectScenePaths())
			{
				offenders.AddRange(SceneDiAuditor.AuditScene(scenePath, coverage, injectConsumingTypes));
				scenesScanned++;
			}

			LogReport(offenders, scenesScanned, injectConsumingTypes.Count, coverage);
		}

		[MenuItem(MENU_AUDIT_ACTIVE, priority = 2101)]
		private static void AuditActiveScene()
		{
			UnityEngine.SceneManagement.Scene activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
			if (string.IsNullOrEmpty(activeScene.path) == true)
			{
				Debug.LogWarning("[SceneDiAuditor] 활성 씬이 저장되지 않은 상태 — 먼저 씬을 저장하세요.");
				return;
			}

			HashSet<Type> injectConsumingTypes = SceneDiAuditor.CollectInjectConsumingTypes();
			SceneCoverageMap coverage = SceneDiAuditor.CollectSceneCoverage();

			List<SceneDiOffender> offenders = SceneDiAuditor.AuditScene(activeScene.path, coverage, injectConsumingTypes);
			LogReport(offenders, scenesScanned: 1, injectConsumingTypes.Count, coverage);
		}

		private static void LogReport(
			List<SceneDiOffender> offenders,
			int scenesScanned,
			int injectConsumingTypeCount,
			SceneCoverageMap coverage)
		{
			StringBuilder report = new StringBuilder();
			report.AppendLine($"[SceneDiAuditor] 스캔 완료 — 씬 {scenesScanned} 개 / [Inject] 소비 타입 {injectConsumingTypeCount} 종 / Directly-covered {coverage.DirectlyCovered.Count} / Hierarchy-covered {coverage.HierarchyCovered.Count}");

			if (offenders.Count == 0)
			{
				report.AppendLine("  ✅ 씬 직접배치 컴포넌트 [Inject] 누락 0 — SceneLifetimeScope 커버리지 완전.");
				Debug.Log(report.ToString());
				return;
			}

			report.AppendLine($"  ⚠ 씬 직접배치 [Inject] 미해소 컴포넌트 {offenders.Count} 건 발견:");
			foreach (SceneDiOffender offender in offenders)
			{
				report.AppendLine($"    [{offender.ScenePath}] {offender.GameObjectPath}  ::  {offender.ComponentTypeName}");
			}
			report.AppendLine("  대응: 해당 컴포넌트 타입을 SceneLifetimeScope.cs 의 Configure 또는 RegisterBuildCallback");
			report.AppendLine("        foreach (container.Inject / container.InjectGameObject) 에 추가. 또는 부모 GameObject 가");
			report.AppendLine("        InjectGameObject 경로면 false-positive (부모 타입을 hierarchy-cover 에 명시).");
			Debug.LogWarning(report.ToString());
		}
	}
}

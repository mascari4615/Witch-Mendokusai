using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using WitchMendokusai.Editor.DI;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-109-C — 씬 직접배치 컴포넌트의 DI 등록 누락 *CI 게이트*.
	///
	/// 배경: TASK-WM-109 이슈 3 — 새 컴포넌트가 World.unity 에 직접 배치됐는데 SceneLifetimeScope
	/// 누락 → 부팅 [Inject] 0 → 사용 시점 NRE → cascade. 매번 stack trace → meta GUID → grep
	/// 사이클. 본 테스트가 그 사이클의 *진입* 자체를 차단 — Composition Root 그래프 검증
	/// (CompositionRootResolveTest) 과 동급의 *씬 실재* 검증.
	///
	/// 검증 단위: `SceneDiAuditor.AuditScene` — 씬 additive open + walk + close.
	/// SceneLifetimeScope.cs 소스 *정본* 자기참조 → 새 registration 추가 시 audit 자동 인식.
	///
	/// 실행: `unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode`
	/// (격리 worktree = MCP / 에디터 락 무관, ~1-2분).
	/// </summary>
	public sealed class SceneDiCoverageTest
	{
		[Test]
		public void ProjectScenes_HaveNoUnregisteredInjectConsumingComponents()
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

			Assert.That(scenesScanned, Is.GreaterThan(0),
				$"프로젝트 씬 0 — `{SceneDiAuditor.SCENES_ROOT}` 하위 .unity 자산 검색 실패. 폴더 구조 확인.");

			if (offenders.Count == 0)
			{
				return;
			}

			StringBuilder message = new StringBuilder();
			message.AppendLine($"씬 직접배치 [Inject] 미해소 컴포넌트 {offenders.Count} 건 (스캔 씬 {scenesScanned} 개):");
			foreach (SceneDiOffender offender in offenders)
			{
				message.AppendLine($"  [{offender.ScenePath}] {offender.GameObjectPath}  ::  {offender.ComponentTypeName} ({offender.ComponentFullTypeName})");
			}
			message.AppendLine("원인: SceneLifetimeScope.Configure / RegisterBuildCallback 의 foreach 에서 누락 → 부팅 [Inject] 0 → 사용 시점 NRE.");
			message.AppendLine("처리: ① 해당 타입을 SceneLifetimeScope 에 등록 (RegisterInHierarchyIfPresent 또는 container.Inject / InjectGameObject foreach).");
			message.AppendLine("       ② 부모 GameObject 가 InjectGameObject 경로면 false-positive — SceneLifetimeScope 에 부모 타입을 InjectGameObject foreach 로 명시 (현재 누락 시).");
			message.AppendLine("진단 메뉴: WM/Audit/Scene DI Coverage (모든 씬)");
			Assert.Fail(message.ToString());
		}

		[Test]
		public void SceneLifetimeScope_CoverageMap_IsParsedNonEmpty()
		{
			// 소스 파싱이 실패해 빈 set 가 되면 audit 전체가 무용지물 (모두 누락 보고). 정본 sanity.
			SceneCoverageMap coverage = SceneDiAuditor.CollectSceneCoverage();
			Assert.That(coverage.DirectlyCovered.Count, Is.GreaterThan(0),
				"SceneLifetimeScope.cs 의 Register*<T> / Inject foreach 파싱 실패 — Auditor regex 정합 깨짐.");
			Assert.That(coverage.HierarchyCovered.Count, Is.GreaterThan(0),
				"SceneLifetimeScope.cs 의 InjectGameObject foreach 파싱 실패 — Auditor regex 정합 깨짐.");
		}

		[Test]
		public void InjectConsumingTypes_AreDetected()
		{
			// reflection 이 0 을 내면 audit 가 silently pass — 정본 sanity.
			HashSet<Type> injectConsumingTypes = SceneDiAuditor.CollectInjectConsumingTypes();
			Assert.That(injectConsumingTypes.Count, Is.GreaterThan(0),
				"[Inject] 소비 컴포넌트 reflection 결과 0 — 어셈블리 로드 / VContainer.InjectAttribute 참조 깨짐.");
		}
	}
}

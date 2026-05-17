using System;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using VContainer;

namespace WitchMendokusai.Tests
{
    /// <summary>
    /// TASK-WM-120 γ — Composition Root 의 *결정적* 검증 루프.
    ///
    /// 배경(정직): γ caller-migration / eager-line 제거 / circular 구조수정 의
    /// 유일 검증이 `wm-boot-smoke.ps1` standalone 부팅이었는데, 신규 격리
    /// worktree 콜드 빌드에서 `LobbyManager.Start→dataManager.Init()` NRE 로
    /// *flaky*(콜드 FAIL / 워밍 PASS) 임을 실증함 (Addressables/세이브 미빌드).
    /// 15~30분 + flaky + standalone + D/WM-117 인프라 의존 = 신뢰 루프 X.
    /// `process.md § 황금의 정신 — 피드백 루프 먼저` 하드게이트:「확신 가는
    /// 루프 서기 전 fix 진입 금지」. 본 테스트가 그 신뢰 루프.
    ///
    /// 메커니즘: VContainer `ContainerBuilder.BuildRegistry()` 가 registry 구성
    /// 시점에 `TypeAnalyzer.CheckCircularDependency` 를 *무조건* 호출 — 전체
    /// ctor / `[Inject]` 메서드 / field / property 의존 그래프를 walk 해
    /// **순환 또는 미등록 의존이면 VContainerException 을 인스턴스화 *전*에
    /// throw**. 즉 RootLifetimeScope.Configure 를 실제 구동해 BuildRegistry 만
    /// 돌리면 — GameObject Instantiate 0 / 매니저 Awake 0 / PlayMode 0 /
    /// standalone 0 / Addressables·세이브 무관 — Composition Root 의 조립
    /// 정합성(순환·미등록)을 결정적으로(ms, RNG/시간/FS 무관) 검증한다.
    /// EmitCallbacks(=BootGuard eager resolve = 인스턴스화)는 BuildRegistry
    /// 와 분리된 후행 메서드라 본 경로에서 호출되지 않음.
    ///
    /// γ 직접 커버: ① eager 라인 제거가 graph-derived 못 깨는지(미등록 0)
    /// ② circular 구조수정이 순환 도입/잔존 안 하는지. caller-migration
    /// ([Inject] 추가)도 dep 미등록이면 여기서 즉시 FAIL.
    ///
    /// 실행: `unity -runTests -batchmode -testPlatform EditMode
    ///        -assemblyNames WM.Tests.EditMode` (격리 worktree = MCP·에디터락
    ///        무관, ~1-2분). wm-playmode-smoke.ps1 의 EditMode 변형.
    /// </summary>
    public sealed class CompositionRootResolveTest
    {
        [Test]
        public void RootLifetimeScope_RegistrationGraph_HasNoCircularOrMissingDependency()
        {
            ContainerBuilder builder = new ContainerBuilder();

            // RootLifetimeScope.Configure 는 순수(빌더 호출 + Resources.Load +
            // static SOManagerBridge.Register) — 인스턴스 필드 미접근. Awake/
            // Build 자동구동을 피하려 GameObject 부착 X, 미초기화 인스턴스에
            // protected Configure 만 reflection 구동.
            RootLifetimeScope scope =
                (RootLifetimeScope)FormatterServices.GetUninitializedObject(typeof(RootLifetimeScope));

            MethodInfo configure = typeof(RootLifetimeScope).GetMethod(
                "Configure", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(configure, Is.Not.Null,
                "RootLifetimeScope.Configure(IContainerBuilder) 시그니처 변경 — 본 테스트 갱신 필요");

            InvokeUnwrapping(() => configure.Invoke(scope, new object[] { builder }),
                "RootLifetimeScope.Configure 실행 실패");

            // BuildRegistry() = registrations + Registry.Build +
            // TypeAnalyzer.CheckCircularDependency (순환/미등록 throw). 인스턴스화
            // (EmitCallbacks) 미포함. protected → reflection.
            MethodInfo buildRegistry = typeof(ContainerBuilder).GetMethod(
                "BuildRegistry", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(buildRegistry, Is.Not.Null,
                "ContainerBuilder.BuildRegistry() 시그니처 변경 — VContainer 버전 확인");

            InvokeUnwrapping(() => buildRegistry.Invoke(builder, null),
                "Composition Root 그래프 결함 — 순환 또는 미등록 의존");
        }

        // reflection Invoke 의 TargetInvocationException 을 벗겨 원인(VContainerException
        // = 순환 경로 / 미등록 타입 이름)을 그대로 Assert 메시지에 노출.
        private static void InvokeUnwrapping(Action invoke, string context)
        {
            try
            {
                invoke();
            }
            catch (TargetInvocationException tie)
            {
                Exception inner = tie.InnerException ?? tie;
                Assert.Fail($"{context}\n  [{inner.GetType().Name}] {inner.Message}");
            }
        }
    }
}

using System;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
    /// <summary>
    /// TASK-WM-104 (parent WM-085 Phase E) — NetCode sync 회귀 측정 자동화.
    ///
    /// Authority interface 계약 + Network 상속 구조를 *결정적*(reflection,
    /// ms, Unity Editor/PlayMode/FishNet 런타임 무관)으로 잠근다. WM-085
    /// Phase D 가 박은 `WorldClock : IAuthorityAware` + `WorldClockNetworkBridge
    /// : WMNetworkBehaviour` 구조가 회귀하면 즉시 FAIL.
    ///
    /// 6 동기 「퀄리티」 first-use baseline 정합 — sync 회귀 0 을 자동 측정
    /// 가능해야 9.5/10 ceiling 충족.
    ///
    /// 본 테스트 어셈블리(WM.Tests.EditMode)는 *의도적으로* WM.Network / FishNet
    /// 를 asmdef 참조하지 않는다 (asmdef 단방향 격리 = wm-asmdef-boundary.yml
    /// 게이트와 동근). 그래서 Phase B 는 compile-time 타입 ref 대신, EditMode
    /// 런타임에 적재된 어셈블리를 이름으로 reflection 해 검증한다.
    ///
    /// 실행: unity -runTests -batchmode -testPlatform EditMode
    ///       -assemblyNames WM.Tests.EditMode
    /// </summary>
    public sealed class NetCodeContractTest
    {
        // Phase A — WorldClock 의 IAuthorityAware 계약: 서버 권위.
        [Test]
        public void WorldClock_RequiredAuthority_IsServer()
        {
            Assert.That(typeof(IAuthorityAware).IsAssignableFrom(typeof(WorldClock)), Is.True,
                "WorldClock 이 IAuthorityAware 를 더 이상 구현하지 않음 — WM-085 Phase D 회귀");

            // WorldClock = MonoBehaviour 라 new 불가. Awake / GameObject 우회 =
            // 미초기화 인스턴스 (CompositionRootResolveTest 와 동일 reflection-purity).
            // RequiredAuthority getter 는 상수 식(=> Authority.Server) — 인스턴스
            // 상태 무접근이라 미초기화 인스턴스로도 안전.
            IAuthorityAware worldClock =
                (IAuthorityAware)FormatterServices.GetUninitializedObject(typeof(WorldClock));

            Assert.That(worldClock.RequiredAuthority, Is.EqualTo(Authority.Server),
                "WorldClock.RequiredAuthority 가 Authority.Server 아님 — sync 권위 회귀");
        }

        // Phase B — WorldClockNetworkBridge 가 WMNetworkBehaviour 상속 유지 +
        // WMNetworkBehaviour 가 FishNet NetworkBehaviour 기반인지.
        // WM.Network = FishNet 의존 → 본 asmdef 는 그걸 참조 안 함(격리). 로드된
        // 어셈블리에서 이름으로 reflection (EditMode 런타임에 WM.Network 적재됨).
        [Test]
        public void WorldClockNetworkBridge_Inherits_WMNetworkBehaviour()
        {
            Type bridgeType = FindLoadedType("WitchMendokusai.WorldClockNetworkBridge");
            Type baseType = FindLoadedType("WitchMendokusai.WMNetworkBehaviour");

            Assert.That(bridgeType, Is.Not.Null,
                "WitchMendokusai.WorldClockNetworkBridge 미발견 — 타입/asmdef 회귀");
            Assert.That(baseType, Is.Not.Null,
                "WitchMendokusai.WMNetworkBehaviour 미발견 — 타입/asmdef 회귀");

            Assert.That(baseType.IsAssignableFrom(bridgeType), Is.True,
                "WorldClockNetworkBridge 가 WMNetworkBehaviour 를 상속하지 않음 — Bridge 구조 회귀");

            // WMNetworkBehaviour 의 base = FishNet NetworkBehaviour (타입 ref 없이 이름만 검사).
            Type fishNetBase = baseType.BaseType;
            Assert.That(fishNetBase, Is.Not.Null,
                "WMNetworkBehaviour 에 base 타입 없음 — NetCode 백엔드 회귀");
            Assert.That(fishNetBase.FullName, Is.EqualTo("FishNet.Object.NetworkBehaviour"),
                "WMNetworkBehaviour base 가 FishNet.Object.NetworkBehaviour 아님 (실제: "
                    + fishNetBase.FullName + ") — NetCode 백엔드 회귀");
        }

        // 메타 — 본 테스트 어셈블리가 FishNet 에 결합되지 않음(asmdef 단방향
        // self-check). wm-asmdef-boundary.yml 게이트의 테스트측 거울.
        [Test]
        public void TestAssembly_DoesNotReference_FishNet()
        {
            AssemblyName[] referencedAssemblies =
                typeof(NetCodeContractTest).Assembly.GetReferencedAssemblies();

            foreach (AssemblyName referenced in referencedAssemblies)
            {
                bool isFishNet =
                    referenced.Name.StartsWith("FishNet", StringComparison.Ordinal);
                Assert.That(isFishNet, Is.False,
                    "WM.Tests.EditMode 가 " + referenced.Name
                        + " 참조 — 테스트 asmdef 격리 위반(FishNet 단방향 침범)");
            }
        }

        // Type.GetType 의 어셈블리 해석 nuance 회피 — 로드된 전 어셈블리 스캔.
        private static Type FindLoadedType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly assembly in assemblies)
            {
                Type found = assembly.GetType(fullName, false);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}

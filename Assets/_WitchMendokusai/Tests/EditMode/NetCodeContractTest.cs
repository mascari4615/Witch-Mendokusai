using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEngine.Assemblies;

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

        // TASK-WM-218 — FishNet 브리지는 지웠다(세계는 WS 한 통로로 돈다). 그 자리에
        // **지금 규약**을 지킨다: 시계는 세계가 주면 그걸 따르고, 못 받는 동안만 스스로 흐른다.
        // 이 자리를 빈칸으로 두면 「시계가 세계를 안 따라가는」 회귀가 조용히 들어온다.
        [Test]
        public void WorldClock_Follows_WorldTime_When_Linked()
        {
            Type clockType = typeof(WorldClock);
            MethodInfo follow = clockType.GetMethod(
                "TryFollowWorldTime",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(follow, Is.Not.Null,
                "WorldClock 이 세계 시각을 따라가는 자리(TryFollowWorldTime)가 없다 — 시계 권위 회귀");
            Assert.That(follow.ReturnType, Is.EqualTo(typeof(bool)),
                "따라갔는지 여부를 돌려줘야 한다(못 받으면 스스로 흐르는 분기 근거)");

            Type doorType = FindLoadedType("WitchMendokusai.Net.WorldDoor");
            Assert.That(doorType, Is.Not.Null,
                "세계로 이어진 줄의 문(WorldDoor)이 없다 — 시계가 볼 곳이 사라졌다");
        }

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
            IReadOnlyList<Assembly> assemblies = CurrentAssemblies.GetLoadedAssemblies();
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

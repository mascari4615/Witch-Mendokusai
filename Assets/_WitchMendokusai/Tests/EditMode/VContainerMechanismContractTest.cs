using NUnit.Framework;
using VContainer;
using VContainer.Internal;

namespace WitchMendokusai.Tests
{
    // ── 프로브 타입 (top-level — nested 면 generator 가 injector 미생성, Emitter.cs:20) ──
    //
    // TASK-WM-109-A. 본 세션(WM-109)이 VContainer 동작을 *소스 정독 없이 가설로*
    // 4회+ fix 한 회귀를 막는 "재현 테스트 우선" 게이트. WM 타입이 아니라
    // *VContainer 메커니즘 자체*를 POCO 로 결정적(EditMode, RNG/Unity 0) 고정한다.
    // 정본 해설: Assets/_WitchMendokusai/Domain/Application/DI/VCONTAINER-MECHANISM.md

    public interface IVcMechProbeDep { }

    public sealed class VcMechProbeDep : IVcMechProbeDep { }

    /// <summary>abstract base + protected relay — UnitObject/SetBaseDeps 패턴 미니 재현.</summary>
    public abstract class VcMechAbstractBase
    {
        public IVcMechProbeDep BaseDep { get; private set; }
        protected void SetBaseDep(IVcMechProbeDep dep) => BaseDep = dep;
    }

    public sealed class VcMechConcrete : VcMechAbstractBase
    {
        public IVcMechProbeDep OwnDep { get; private set; }

        [Inject]
        public void Construct(IVcMechProbeDep baseDep, IVcMechProbeDep ownDep)
        {
            SetBaseDep(baseDep);
            OwnDep = ownDep;
        }
    }

    /// <summary>base 에 [Inject] public property — 자식 injector 가 base-walk 로 자동 주입하는지.</summary>
    public abstract class VcMechBaseWithInjectProp
    {
        [Inject] public IVcMechProbeDep InheritedProp { get; set; }
    }

    public sealed class VcMechDerivedNoOwnInject : VcMechBaseWithInjectProp { }

    /// <summary>
    /// VContainer 1.17.0 메커니즘 계약 — 본 세션 4개 가설을 소스 동작으로 반증/확정.
    /// 깨지면 = VContainer 동작 모델이 바뀐 것 → DI/VCONTAINER-MECHANISM.md 재정독.
    /// </summary>
    public sealed class VContainerMechanismContractTest
    {
        // 가설③ 인증: Construct relay 패턴은 generated(빠름, VCON 진단 0) 경로여야.
        // ReflectionInjector 폴백 = generated 실패 (member < internal 등). InjectorCache.cs:16-28.
        [Test]
        public void Concrete_UsesGeneratedInjector_NotReflectionFallback()
        {
            IInjector injector = InjectorCache.GetOrBuild(typeof(VcMechConcrete));
            TestContext.WriteLine($"VcMechConcrete injector = {injector.GetType().FullName}");
            Assert.That(injector.GetType().Name, Does.EndWith("GeneratedInjector"),
                "generated injector 부재 → ReflectionInjector 폴백. member 접근성(>= internal) "
                + "위반 또는 [Inject] 메서드 2개 이상(Emitter.cs:161) 또는 generator 미적용. "
                + "DI/VCONTAINER-MECHANISM.md §0·§2·§3 참고.");
        }

        // 가설④ 인증: 자식 [Inject] Construct 가 base deps 를 SetBaseDep 로 릴레이 — 정상 동작.
        [Test]
        public void Concrete_Construct_RelaysBaseDep_AndSetsOwnDep()
        {
            ContainerBuilder builder = new ContainerBuilder();
            builder.Register<VcMechProbeDep>(Lifetime.Singleton).As<IVcMechProbeDep>();
            builder.Register<VcMechConcrete>(Lifetime.Singleton);
            IObjectResolver container = builder.Build();

            VcMechConcrete resolved = container.Resolve<VcMechConcrete>();
            Assert.That(resolved.OwnDep, Is.Not.Null, "자식 [Inject] Construct ownDep 미주입");
            Assert.That(resolved.BaseDep, Is.Not.Null,
                "SetBaseDep 릴레이 미작동 — base-deps 패턴 회귀 (UnitObject 동류).");
        }

        // ★ 가설① 반증의 핵심 재현: abstract base 의 [Inject] property 가 *자식 injector 에
        // 포함*된다 (TypeMeta.GetAllMembers base-walk, SymbolExtensions.cs:14). "abstract/base
        // [Inject] 는 generator 가 안 만든다"가 사실이면 InheritedProp == null 이어야 한다.
        [Test]
        public void AbstractBase_InjectProperty_IsInheritedIntoConcreteInjector()
        {
            ContainerBuilder builder = new ContainerBuilder();
            builder.Register<VcMechProbeDep>(Lifetime.Singleton).As<IVcMechProbeDep>();
            builder.Register<VcMechDerivedNoOwnInject>(Lifetime.Singleton);
            IObjectResolver container = builder.Build();

            VcMechDerivedNoOwnInject resolved = container.Resolve<VcMechDerivedNoOwnInject>();
            Assert.That(resolved.InheritedProp, Is.Not.Null,
                "base 의 [Inject] property 가 자식 injector 에 미포함 = 가설① 이 맞다는 뜻. "
                + "VContainer 1.17.0 소스(base-walk)대로면 non-null. 회귀 시 "
                + "DI/VCONTAINER-MECHANISM.md §1·§4① 재검증.");
        }
    }
}

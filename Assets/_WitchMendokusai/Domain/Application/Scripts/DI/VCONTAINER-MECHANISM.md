# VContainer 메커니즘 정본 (source 정독 결과)

TASK-WM-109-A. VContainer **1.17.0** (`jp.hadashikick.vcontainer`,
git `hadashiA/VContainer` tag `1.17.0`, commit `177dbda5`) 소스를 직접
정독해 확정한 동작 모델. 본 문서는 "가설 박기 X" — 모든 주장에 소스
파일·라인을 인증으로 박는다. 향후 DI 회귀 fix 시 *이 문서로 먼저 검증*
하고, 가설 기반 추측 fix 를 금지한다.

> 인증 경로 표기: `SourceGenerator/Emitter.cs:NN` = `VContainer.SourceGenerator`
> 프로젝트, `Runtime/.../X.cs:NN` = `VContainer/Assets/VContainer/Runtime/`.

---

## 0. 두 개의 인젝터 경로 (가장 중요한 분기)

`InjectorCache.GetOrBuild(Type)` (`Runtime/Internal/InjectorCache.cs:11`)
가 *런타임 구체 타입* 기준으로 인젝터를 1개 고른다:

1. `{FullName}GeneratedInjector` 가 **같은 어셈블리**에 있으면 그걸 사용
   (`InjectorCache.cs:16-19`) — source generator 산출물. 빠름.
2. 없으면 `ReflectionInjector.Build(type)` 폴백
   (`InjectorCache.cs:28`) — 리플렉션. 동작은 하되 느리고, generator
   가 *의도적으로 거부한* 케이스(아래)에서만 들어옴.

→ 즉 **source generator 가 인젝터를 안 만들면 컴파일은 통과하고
런타임에 리플렉션으로 조용히 폴백**한다. "왜 느린가 / 왜 VCON 경고가
뜨는가"의 근원이 여기다.

generator 가 인젝터를 **안 만드는** 타입 (`Emitter.cs:TryEmitGeneratedInjector`):
- **nested 타입** — `Emitter.cs:20-30` (`NestedNotSupported` VCON0005,
  단 explicit injectable 일 때만 진단)
- **abstract 타입** — `Emitter.cs:32-42` (`AbstractNotAllow` VCON0002,
  단 *그 abstract 타입 자신*이 explicit injectable 일 때만 진단)
- **제네릭 타입** — `Emitter.cs:44-47` (`return false`, 진단 없음. TODO 주석)

---

## 1. TypeMeta — [Inject] 멤버 수집은 **base 를 타고 올라간다**

`TypeMeta.GetInjectFields/Properties/Methods`
(`SourceGenerator/TypeMeta.cs:90-115`) 는 전부 `Symbol.GetAllMembers()`
를 쓴다. `GetAllMembers` (`SourceGenerator/SymbolExtensions.cs:11-33`)
는 주석 그대로 **"Iterate Parent -> Derived"** — `symbol.BaseType` 를
재귀로 먼저 훑고 자기 멤버를 훑는다 (override 는 부모 것만, 중복 제외).

→ **구체(concrete) 서브클래스의 generated injector 는 abstract/base
클래스에 선언된 `[Inject]` field/property/method 를 *포함*한다.**
"base 의 [Inject] 는 생성 안 된다"는 명백히 **틀린** 모델이다.
(ctor 만 예외 — `GetConstructors` 는 `Symbol.InstanceConstructors`,
`TypeMeta.cs:83-88`, base ctor 미포함.)

---

## 2. 접근성 규칙 — `>= internal` 만 generated 경로

`Emitter` 가 generated injector 에 멤버 주입 코드를 박기 전, 각 멤버를
`CanBeCallFromInternal()` 로 검사:

```
SymbolExtensions.cs:50-53
public static bool CanBeCallFromInternal(this ISymbol symbol)
    => symbol.DeclaredAccessibility >= Accessibility.Internal;
```

Roslyn `Accessibility` enum 순서: `Private(1) < ProtectedAndInternal(2,
=private protected) < Protected(3) < Internal(4) < ProtectedOrInternal(5,
=protected internal) < Public(6)`. `>= Internal(4)` 이므로:

| 접근성 | generated 경로 | 결과 |
|---|---|---|
| `public` (6) | ✅ | OK |
| `protected internal` (5) | ✅ | OK |
| `internal` (4) | ✅ | OK |
| `protected` (3) | ❌ | **거부** |
| `private protected` (2) | ❌ | **거부** |
| `private` (1) | ❌ | **거부** |

거부 시 `Emitter` 가 진단을 띄우고 `error=true` →
`TryEmitInjectMethod` `return false` → `TryEmitGeneratedInjector`
`return false` → **그 구체 타입의 generated injector 자체가 안 나옴**
→ 런타임 `ReflectionInjector` 폴백. 리플렉션은
`BindingFlags.NonPublic` (`Runtime/Internal/TypeAnalyzer.cs:263`) +
base 타입 walk (`TypeAnalyzer.cs:265-343`) 이므로 private/protected 도
*동작은 한다*. 즉 "private/protected 면 안 박힌다"는 부정확 — **정확히는
"generated(빠른) 경로에서 빠지고 리플렉션으로 폴백 + VCON 진단"**.

진단 ID (`SourceGenerator/DiagnosticDescriptors.cs`):
- `VCON0007` PrivateFieldNotSupported — field `< internal` (`Emitter.cs:117`)
- `VCON0008` PrivatePropertyNotSupported — setter null/init-only/`< internal` (`Emitter.cs:139-141`)
- `VCON0009` PrivateMethodNotSupported — `[Inject]` 메서드 `< internal` (`Emitter.cs:172`)

---

## 3. `[Inject]` 메서드는 **타입당 1개** (base+derived 합산)

`Emitter.cs:161-168`:

```csharp
if (typeMeta.InjectMethods.Count > 1)
{
    context.ReportDiagnostic(Diagnostic.Create(
        DiagnosticDescriptors.GenericsNotSupported,  // ← (소스 quirk) VCON0010
        ...));
    error = true;
}
```

`InjectMethods` 는 §1 대로 **base 포함** 합산. 따라서:

> base 에 `[Inject] Foo()` + derived 에 `[Inject] Construct()` = 합 2개
> → generated injector 실패 → 리플렉션 폴백.

⚠ **소스 quirk**: 이 케이스는 의미상 `MultipleInjectMethodNotSupported`
(VCON0004) 인데, `Emitter.cs:163` 이 실수로 `GenericsNotSupported`
(VCON0010) 를 띄운다 (1.17.0 기준). 즉 *"Generics not supported"*
경고가 떠도 진짜 원인이 **"[Inject] 메서드 2개 이상"** 일 수 있다.
WM 세션이 VContainer 동작에 혼란을 겪은 한 원인으로 추정 — 메시지가
원인을 오도한다. (field/property 는 개수 제한 없음 — 메서드만 1개룰.)

ctor: `[Inject]` ctor 2개 이상 = `VCON0003`
(`MultipleCtorAttributeNotSupported`, `Emitter.cs:345-352`). `[Inject]`
없으면 *파라미터 최다* ctor 자동 선택 (`Emitter.cs:354-356`).

---

## 4. 본 세션 4개 가설 — 소스 라인 인증 (가설 박기 X 의 본보기)

TASK-WM-109 배경의 4개 가설을 소스로 판정한다.

### 가설 ① "abstract base 의 [Inject] member 를 generator 가 생성 안 함" → **틀림**

- abstract *타입 자신*은 injector 미생성 (`Emitter.cs:32-42`) — 맞다.
  그러나 abstract 는 직접 resolve/instantiate 대상이 *아니므로* 무관.
- **구체 서브클래스의 generated injector 는 base `[Inject]` 멤버를
  포함**한다 (`TypeMeta.cs:92` `GetAllMembers` → `SymbolExtensions.cs:14`
  base walk). 인증 끝.
- `UnitObject`(abstract) 가 `SetBaseDeps` 릴레이를 쓰는 *진짜* 이유는
  "base 멤버 미생성"이 **아니라** §3 의 **[Inject] 메서드 1개룰**:
  자식이 `[Inject] Construct` 1개를 가지길 원하므로 base 에 또 다른
  `[Inject]` 메서드를 둘 수 없다. (대안: base deps 를 `[Inject]
  public/internal` field/property 로 노출 — 개수 제한 없고 자식
  injector 가 자동 set. §6 참고.)
- 인용된 진단 ID `VCON0010` 도 오인 — `VCON0010` 은 *Generics*
  (`DiagnosticDescriptors.cs:81-82`). abstract 는 `VCON0002`.

### 가설 ② "Init 을 Awake 에서 호출 → Start race" → **틀림 (race 아님, 결정적 순서)**

- `LifetimeScope` 는 `[DefaultExecutionOrder(-5000)]`
  (`Runtime/Unity/LifetimeScope.cs:10`). scope.`Awake` 가 *모든* 일반
  컴포넌트 Awake(기본 0) 보다 먼저 `Build()` (`LifetimeScope.cs:135-156`)
  → 컨테이너 구성 + `RegisterBuildCallback` 들 + `SetContainer` →
  `AutoInjectAll` 까지 **그 자리에서 동기 실행** (`LifetimeScope.cs:185-226`).
- 따라서 scope 가 빌드 콜백에서 eager-resolve / `InjectGameObject`
  하는 컴포넌트는 *자기 Awake 전*에 Construct 가 끝난다.
- pooled 객체: `ObjectPoolManager.CreateObject` 가 prefab 을 **비활성
  토글 후 Instantiate** (그래서 Awake/OnEnable 미발화) → `InjectGameObject`
  → Push. 활성화는 풀에서 꺼낼 때 → 이때 Awake → Start. 즉 여기서도
  Construct 가 Awake 보다 먼저.
- 어떤 경로든 *비결정 race* 가 아니라 **결정적 Unity 생명주기 ↔ DI
  호출지점 순서**다. `Init()` 을 `Start` 에 둔 건, Start 가 scope-build
  inject 와 pool inject *둘 다 끝난 뒤* 결정적으로 도는 지점이라
  견고하기 때문 (옳은 fix). 틀린 건 *진단* — "race" 라는 멘탈모델이
  churn 을 유발.

### 가설 ③ "field 인젝션 private/protected" → **부분적으로 맞음 (정확히 = `>= internal`)**

§2 가 인증. generated 경로는 `>= internal` 만 (`SymbolExtensions.cs:52`).
`protected`(3) 도 거부 — `protected internal`(5)/`internal`(4)/`public`(6)
만 OK. private/protected 도 *리플렉션 폴백으로 동작은 함* (단 VCON 진단
+ 느림). "public/internal 구분"이라는 수정된 모델이 소스와 일치.

### 가설 ④ "SetBaseDeps 패턴 불필요했을 가능성" → **부분적으로 맞음**

- `SetBaseDeps` 릴레이는 **틀린 이유(가설 ①)로 도입**됐으나, *결과적
  으로* §3 [Inject] 메서드 1개룰을 만족하는 **유효한 한 패턴**이다
  (자식 `[Inject] Construct` 1개 + 평범한 메서드로 base 전달).
- 그러나 **VContainer 강제 제약은 아니다.** base deps 를 `[Inject]
  public`(또는 `internal`) **property/field** 로 노출하면 (개수 제한
  없음, §3) 자식 injector 가 §1 base-walk 로 자동 주입 — 릴레이 불요.
- 결론: `SetBaseDeps` 는 "Construct 단일 진입점을 원할 때의 *설계
  선택*"이지 "VContainer 한계 회피"가 아니다.

---

## 5. Unity 등록 헬퍼 — 정확한 의미

### `RegisterComponentInHierarchy<T>()`

`Runtime/Unity/ContainerBuilderUnityExtensions.cs:139-159`:
- `lifetimeScope.gameObject.scene` 캡처 → `ComponentRegistrationBuilder(scene, type)`.
- 빌드 콜백에 `container.Resolve(...)` 강제 1회 (= inject 강제 실행).
- 실제 탐색은 `FindComponentProvider.SpawnInstance`
  (`Runtime/Unity/InstanceProviders/FindComponentProvider.cs:28-70`):
  scene 의 **root GameObject 들을 순회**하며 각각
  `GetComponentInChildren(componentType, true)` (`:48`, `true`=inactive
  포함) — **첫 매치 1개만** 반환 (`:49 break`). 없으면
  `VContainerException` throw (`:54`).

→ **다중 인스턴스 미지원**: 씬에 같은 컴포넌트 N개여도 등록 경로는
*첫 1개만* 주입. 나머지는 별도 `InjectGameObject`/`Inject` 루프 필요
(WM `SceneLifetimeScope.cs:137-150` 이 정확히 이 패턴).
→ **scene-local**: 해당 scope 의 씬만 검색 (DontDestroyOnLoad 의 다른
씬 객체 false-positive 없음 — `scene.GetRootGameObjects`).
→ 등록 타입이 씬에 *없으면* Resolve 가 throw → 빌드 콜백 abort →
후속 inject 전부 차단 (WM `SceneLifetimeScope` 의 `IsInScene<T>` 가드
존재 이유 = "씬 실재에 동적 일치"). `FindComponentProvider.cs:38,54`.

주입 시 `InjectorCache.GetOrBuild(monoBehaviour.GetType())`
(`FindComponentProvider.cs:64`) — **런타임 구체 타입** 기준 (§0). base
[Inject] 포함 (§1).

### `RegisterComponentInNewPrefab` / `OnNewGameObject`

`ContainerBuilderUnityExtensions.cs:166-211`. 생성형 — 씬 탐색 무관.
prefab Instantiate 또는 new GameObject + AddComponent. WM
`RootLifetimeScope.RegisterLeaf<T>` 가 이 경로 + `DontDestroyOnLoad()`.

### `IObjectResolver.InjectGameObject(go)` vs `.Inject(component)`

`Runtime/Unity/ObjectResolverUnityExtensions.cs:36-64`:
`InjectGameObject` = `InjectGameObjectRecursive` — **자기 GameObject 의
모든 MonoBehaviour (`GetComponents`) + 자식 트랜스폼 재귀 전부**에
`resolver.Inject` 호출. 반면 `container.Inject(x)` 는 *그 컴포넌트
1개만*.

→ `[RequireComponent]` 형제나 자식 컴포넌트의 `[Inject] Construct` 도
주입하려면 **반드시 `InjectGameObject`**. `Inject(x)` 는 sibling/child
미주입 → 그쪽 deps null (WM-115 R3b 사고의 근원). 단 WM 은 *자식
Construct 가 스스로 cascade 하는* 경우(`Player.Construct`) 의도적으로
`Inject(player)` 사용 — 패턴 자체를 알고 선택한 것.

---

## 6. scope vs container — 명확한 구분

- **`LifetimeScope`** = MonoBehaviour. `Configure(IContainerBuilder)`
  오버라이드로 등록 선언. `Awake`(-5000) 에서 `Build()`.
  `LifetimeScope.cs:11,135,185`.
- **`IObjectResolver Container`** = `Build()` 산출 *컨테이너 인스턴스*.
  scope 의 `Container` 프로퍼티 (`LifetimeScope.cs:127`). 실제 Resolve/
  Inject 주체.
- **부모-자식 scope**: `Build()` 에서 `Parent = GetRuntimeParent()`
  (`LifetimeScope.cs:187-188`). Parent 있으면
  `Parent.Container.CreateScope(...)` 로 *자식 컨테이너* 생성
  (`:199-205`) — 자식은 부모 등록을 resolve 가능, 부모는 자식 것
  불가 (단방향). Parent root 면 부모를 먼저 Build (`:192-196`).
- **부모 탐색 순서** (`GetRuntimeParent` `:308-353`): `parentReference.
  Object` → `FindParent()` 오버라이드 → `parentReference.Type` 로 씬
  검색 → `GlobalOverrideParents` → `VContainerSettings` 의 root. WM 은
  `RootLifetimeScope`(root, settings 등록) ← `SceneLifetimeScope`(자식)
  구조. 자식이 root 의 13 leaf/7 root 매니저를 transitive resolve.
- **eager vs lazy**: `Lifetime.Singleton` 등록은 *lazy* (첫 Resolve
  시 생성). WM 은 `RegisterBuildCallback` 안 `BootGuard.EagerResolve<T>`
  로 *빌드 시점 강제 인스턴스화* → raw `.Instance` 채널 셋 + init-order
  를 부팅에 결정화. 빌드 콜백은 `ContainerBuilder.Build()` 내부에서
  동기 실행되므로 scope.Awake(-5000) 시점에 전부 끝남.
- **그래프 검증**: `ContainerBuilder.BuildRegistry()` 가 registry 구성
  시 `TypeAnalyzer.CheckCircularDependency` 무조건 호출 — 순환/미등록을
  *인스턴스화 전* throw. `CompositionRootResolveTest` 가 이걸 이용해
  GameObject/Awake/PlayMode 0 으로 조립 정합성을 ms 검증.

---

## 7. 향후 DI 회귀 fix 프로토콜 (가설 박기 금지)

1. 증상 재현 테스트 **먼저** (`VContainerMechanismContractTest` 패턴
   — POCO + ContainerBuilder, EditMode, 결정적). 가설로 코드 만지기 전
   *실패하는 테스트*로 현상을 고정.
2. 본 문서 §0–6 으로 메커니즘 대조. 추측 금지 — 해당 소스 라인 재확인.
3. generated vs reflection 경로 어느 쪽인지부터 판정
   (`InjectorCache.GetOrBuild(t).GetType().Name` — `*GeneratedInjector`
   면 generated, `ReflectionInjector` 면 폴백 = VCON 진단 존재).
4. fix 후 재현 테스트 green + `CompositionRootResolveTest` green +
   `wm-boot-smoke` 로 회귀 확인.
5. 코드 주석에는 **소스 라인 인증**을 박는다 (예:
   `// RegisterComponentInHierarchy = GetComponentInChildren(t,true) 첫
   매치만 (FindComponentProvider.cs:48) — 다중 인스턴스 cascade X`).

---

*정독 산출: `git clone --branch 1.17.0 hadashiA/VContainer` →
`Emitter.cs` / `TypeMeta.cs` / `SymbolExtensions.cs` /
`DiagnosticDescriptors.cs` / `ContainerBuilderUnityExtensions.cs` /
`FindComponentProvider.cs` / `LifetimeScope.cs` /
`ObjectResolverUnityExtensions.cs` / `InjectorCache.cs` /
`ReflectionInjector.cs` / `TypeAnalyzer.cs` 직접 read. 추측 0.*

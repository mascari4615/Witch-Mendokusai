using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace WitchMendokusai
{
	public class SceneLifetimeScope : LifetimeScope
	{
		/// <summary>
		/// 씬 조립이 세우는 것들의 목록 (TASK-WM-409 단계 B).
		/// 예전엔 <c>Resources.Load&lt;T&gt;("Singletons/…")</c> 로 <b>이름으로</b> 찾았고,
		/// 그래서 그 폴더가 <b>모든 제품 빌드</b>에 실렸다. 이제 뿌리 조립과 <b>같은 카탈로그</b>를 본다.
		/// </summary>
		[SerializeField] private SingletonCatalog catalog;

		// ★ source 인증 (TASK-WM-109-A, VContainer 1.17.0): RegisterComponentInHierarchy<T>
		// 는 빌드 콜백서 Resolve 강제 → FindComponentProvider 가 scene root 들을 순회하며
		// GetComponentInChildren(type, true) — *첫 매치 1개만* (FindComponentProvider.cs:48-49,
		// `true`=inactive 포함), 없으면 VContainerException throw (:54). → 다중 인스턴스
		// cascade X (나머지는 아래 InjectGameObject 루프). 정본: DI/VCONTAINER-MECHANISM.md §5.
		// ★ 진짜 근본 (TASK-WM-078, 2026-05-16) — RegisterComponentInHierarchy<T> 는 BuildScope 시점
		// 등록타입을 씬에서 강제 Resolve. World.unity 에 *없는* registered 타입 1개가 VContainerException →
		// Build callback 전체 abort → 후속 모든 inject 차단 (마스킹 체인: VoxelInteraction→ChunkManager
		// →GroundGenerator→SpawnerInitializer→…). 개별 제거 = 원 캐스케이드 14커밋 함정 (씬 가변 + 미래 drift).
		// 블라인드 catch = FastFail 위반. 근본 = 등록을 씬 실재에 *동적 일치*: 존재할 때만 등록/Resolve.
		// scene-optional 부재는 정상 (씬 구성 가변), 진짜 누락은 consumer resolve 에서 명확히 실패 (FastFail 보존).
		// RegisterComponentInHierarchy 는 *이 scope 의 씬* 만 검색 (scene-local). FindAnyObjectByType 는 전 씬
		// (DontDestroyOnLoad 포함) — UIRoot(DDOL)+AddComponent MagicBookView 같은 cross-scene 을 false-positive.
		// 헬퍼의 scene-scope 의미를 VContainer 와 정확히 일치 = 이 scope 의 gameObject.scene 한정.
		// 실제로 심긴 갈래만 (프리팹 미생성이면 안 심김). 깨우는 대상도 이것뿐
		private readonly List<IFeatureInstaller> installedFeatures = new();

		private bool IsInScene<T>() where T : Component
		{
			foreach (T component in FindObjectsByType<T>(FindObjectsInactive.Include))
			{
				if (component.gameObject.scene == gameObject.scene)
					return true;
			}
			return false;
		}

		private void RegisterInHierarchyIfPresent<T>(IContainerBuilder builder) where T : Component
		{
			if (IsInScene<T>())
				builder.RegisterComponentInHierarchy<T>();
		}

		private void ResolveIfPresent<T>(IObjectResolver container) where T : Component
		{
			// TASK-WM-118 I1 — eager Resolve 를 BootGuard 경유 (손-순서↔위상 불일치/
			// dep 미해결/순환 시 부팅 시점 타입 귀속 명시 차단). 성공 시 동작 무변경.
			if (IsInScene<T>())
				BootGuard.EagerResolve<T>(container, "Scene");
		}

		protected override void Configure(IContainerBuilder builder)
		{
			// 씬 hierarchy 컴포넌트 — 존재 확인 후 등록 (씬 실재 동적 일치).
			RegisterInHierarchyIfPresent<StageManager>(builder);
			RegisterInHierarchyIfPresent<DungeonManager>(builder);
			RegisterInHierarchyIfPresent<UIManager>(builder);
			RegisterInHierarchyIfPresent<CameraManager>(builder);
			RegisterInHierarchyIfPresent<BuildManager>(builder);
			RegisterInHierarchyIfPresent<CityPaintManager>(builder); // TASK-WM-164 SimCity Phase1 step5
			// 자유 카메라 셋 (World 씬 배치). FreeCameraControllerBase 의 Construct 를 자식이 받는다 (2026-09-05)
			RegisterInHierarchyIfPresent<FreeFlyCameraController>(builder);
			RegisterInHierarchyIfPresent<OverheadContentCameraController>(builder);
			RegisterInHierarchyIfPresent<FreeFlyCrosshair>(builder);
			RegisterInHierarchyIfPresent<ChatManager>(builder);
			RegisterInHierarchyIfPresent<ToolTipPopupManager>(builder);
			RegisterInHierarchyIfPresent<UIHoldingSlot>(builder);
			RegisterInHierarchyIfPresent<CardManager>(builder);
			RegisterInHierarchyIfPresent<UINyang>(builder);
			RegisterInHierarchyIfPresent<UIWorkableDollCount>(builder);
			RegisterInHierarchyIfPresent<UIInteractPopup>(builder);
			RegisterInHierarchyIfPresent<UISkillBar>(builder);
			RegisterInHierarchyIfPresent<Player>(builder);
			RegisterInHierarchyIfPresent<PlayerObject>(builder);
			// GameEventListener = 다중 인스턴스 leaf → RegisterComponentInHierarchy(첫1개만 Resolve) 부적합.
			// 아래 build-callback 의 foreach InjectGameObject 가 전 인스턴스 주입 (TASK-WM-192, Monster/NPC 동형).
			RegisterInHierarchyIfPresent<ExpManager>(builder);
			RegisterInHierarchyIfPresent<SpawnerInitializer>(builder);
			RegisterInHierarchyIfPresent<UItemEquipPopup>(builder);
			RegisterInHierarchyIfPresent<UIQuestGrid>(builder);
			RegisterInHierarchyIfPresent<MagicBookView>(builder);
			RegisterInHierarchyIfPresent<UINPCMenu>(builder);
			// TASK-WM-118 I2 — UINPC(UIPanelGroup<NPCPanelType>) 도 CardManager 정본처럼
			// scope 등록 → Construct 가 build callback(모든 Start 전)서 결정적 실행.
			// 구: UINPC 가 UIManager.Start InjectGameObject(R4) 로만 주입 → UINPC.Start vs
			// UIManager.Start 무보장순서 → uiManager null → UIPanelGroup.Start:47 NRE.
			// 자식 패널(UIDungeonEntrance 등)은 여전히 R4 InjectGameObject 경로(불변).
			// UINPC.Construct=멱등(SetUIManager 재호출 무해).
			RegisterInHierarchyIfPresent<UINPC>(builder);
			RegisterInHierarchyIfPresent<UIUpgrade>(builder);
			RegisterInHierarchyIfPresent<UIShop>(builder);
			RegisterInHierarchyIfPresent<UICraft>(builder);
			RegisterInHierarchyIfPresent<UIItemSlot>(builder);
			RegisterInHierarchyIfPresent<UIQuestSlot>(builder);
			RegisterInHierarchyIfPresent<ToolTipTrigger>(builder);

			// prefab/code-spawn — FindObjectsByType 무관 (생성형), 존재 확인 불필요.
			if (catalog == null)
			{
				Debug.LogError("[SceneLifetimeScope] SingletonCatalog 이 안 꽂혔다 — 씬 조립을 못 세운다 (TASK-WM-409)");
				return;
			}
			DevWindowController devWindowControllerPrefab = catalog.Get<DevWindowController>();
			builder.RegisterComponentInNewPrefab(devWindowControllerPrefab, Lifetime.Scoped);

			DiscoveryWindowController discoveryWindowControllerPrefab = catalog.Get<DiscoveryWindowController>();
			builder.RegisterComponentInNewPrefab(discoveryWindowControllerPrefab, Lifetime.Scoped);

			// 갈래는 자기가 심는다. 여기는 이름을 모른다 (목록은 FeatureManifest)
			installedFeatures.Clear();
			for (int index = 0; index < FeatureManifest.Installers.Count; index++)
			{
				IFeatureInstaller feature = FeatureManifest.Installers[index];
				if (feature.InstallScene(builder, catalog))
				{
					installedFeatures.Add(feature);
				}
			}

			builder.RegisterComponentOnNewGameObject<GameModeManager>(Lifetime.Scoped, nameof(GameModeManager));
			builder.RegisterComponentOnNewGameObject<DialogueRunner>(Lifetime.Scoped, nameof(DialogueRunner));

			// Lazy 등록 강제 instantiate + raw Instance accessor 셋 트리거 (caller transitional 보존).
			// hierarchy 등록은 존재 확인 후에만 Resolve (부재 시 skip — abort 없음).
			builder.RegisterBuildCallback(container =>
			{
				ResolveIfPresent<StageManager>(container);
				ResolveIfPresent<DungeonManager>(container);
				BootGuard.EagerResolve<DevWindowController>(container, "Scene");
				BootGuard.EagerResolve<DiscoveryWindowController>(container, "Scene");
				for (int index = 0; index < installedFeatures.Count; index++)
					installedFeatures[index].ResolveScene(container);
				ResolveIfPresent<UIManager>(container);
				ResolveIfPresent<CameraManager>(container);
				ResolveIfPresent<BuildManager>(container);
				ResolveIfPresent<CityPaintManager>(container); // TASK-WM-164 step5 — 등록↔해소 짝
				ResolveIfPresent<FreeFlyCameraController>(container);
				ResolveIfPresent<OverheadContentCameraController>(container);
				ResolveIfPresent<FreeFlyCrosshair>(container);
				ResolveIfPresent<ChatManager>(container);
				ResolveIfPresent<ToolTipPopupManager>(container);
				ResolveIfPresent<UIHoldingSlot>(container);
				BootGuard.EagerResolve<GameModeManager>(container, "Scene");
				BootGuard.EagerResolve<DialogueRunner>(container, "Scene");
				ResolveIfPresent<CardManager>(container);
				ResolveIfPresent<UINyang>(container);
				ResolveIfPresent<UIWorkableDollCount>(container);
				ResolveIfPresent<UIInteractPopup>(container);
				ResolveIfPresent<UISkillBar>(container);
				ResolveIfPresent<Player>(container);
				ResolveIfPresent<PlayerObject>(container);
				ResolveIfPresent<ExpManager>(container);
				ResolveIfPresent<SpawnerInitializer>(container);
				ResolveIfPresent<UItemEquipPopup>(container);
				ResolveIfPresent<UIQuestGrid>(container);
				ResolveIfPresent<MagicBookView>(container);
				ResolveIfPresent<UINPCMenu>(container);
				ResolveIfPresent<UINPC>(container); // TASK-WM-118 I2 — Construct 결정화
				ResolveIfPresent<UIUpgrade>(container);
				ResolveIfPresent<UIShop>(container);
				ResolveIfPresent<UICraft>(container);
				ResolveIfPresent<UIItemSlot>(container);
				ResolveIfPresent<UIQuestSlot>(container);
				ResolveIfPresent<ToolTipTrigger>(container);

				// pool-spawned 컴포넌트가 scene-scope deps (UIManager 등) resolve 가능하게 pool container = scene container.
				BootGuard.EagerResolve<ObjectPoolManager>(container, "Scene").SetContainer(container);

				// 씬 직접배치 MonsterObject/ResourceNodeObject (World.unity Dummy/MineralBase 등).
				// TASK-WM-115 R3b — container.Inject(x) = *컴포넌트만* → sibling UnitMovement
				// ([RequireComponent], [Inject] GameManager/TimeManager) 미주입 → MineralBase NRE.
				// InjectGameObject = VContainer 표준 계층-재귀 primitive (ObjectPoolManager 와 동일
				// established 패턴). 발산 제거 — 씬배치 actor 도 whole-hierarchy 주입으로 수렴.
				foreach (MonsterObject monsterObject in FindObjectsByType<MonsterObject>(FindObjectsInactive.Include))
					container.InjectGameObject(monsterObject.gameObject);
				foreach (ResourceNodeObject resourceNodeObject in FindObjectsByType<ResourceNodeObject>(FindObjectsInactive.Include))
					container.InjectGameObject(resourceNodeObject.gameObject);
				// TASK-WM-174 — 씬 직접배치 NPCObject (떠돌이상인/퀘스트지기/던전지기/솥 등 크래프팅 스테이션·마을 NPC).
				// 누락 갭: Monster/ResourceNode/Player/마커는 InjectGameObject 수렴됐는데 NPCObject 만 빠져
				// scene-placed NPC 전원 [Inject] Construct 미실행 → uiManager/timeManager null →
				// NPCObject.OnInteract(uiManager) + UnitObject.Init(timeManager) NRE (4 NPC 전부 uiManager=NULL 실측).
				// 동일 established 계층-재귀 패턴으로 수렴 (Monster/ResourceNode 와 동형). NPCObject.Construct=멱등.
				foreach (NPCObject npcObject in FindObjectsByType<NPCObject>(FindObjectsInactive.Include))
					container.InjectGameObject(npcObject.gameObject);
				// TASK-WM-192 — 씬 직접배치 GameEventListener (Debug/Hit 캔버스 등 다중 인스턴스).
				// 구: RegisterComponentInHierarchy<GameEventListener> = 첫 1개만 Resolve → 나머지 [Inject] 미실행
				// → OnEnable NRE. 동일 established 계층-재귀로 전 인스턴스 수렴 (Construct=init-order 안전 멱등).
				foreach (GameEventListener gameEventListener in FindObjectsByType<GameEventListener>(FindObjectsInactive.Include))
					container.InjectGameObject(gameEventListener.gameObject);
				// 씬배치 Player/doll/Marker — Editor-dev(EditorManager additive Stage_Home) 와 production(pooled
				// stage prefab, #4 InjectGameObject) 의 DI 진입을 동일 established 패턴으로 수렴 (발산 제거).
				// Player inject → Player.Construct 가 자식 cascade (PlayerObject/PlayerRotation/DollAnimator/
				// UnitMovement 등). Marker 류는 Player.prefab 자식 아닐 수 있어 명시 (캐스케이드 ac9b1d12 증거). TASK-WM-078 2026-05-16.
				// ★ 계층 재귀로 통일 — 예전엔 Player 본인만 주입하고 자식 cascade 를 Player.Construct 가
				//   손으로 했다. 그러면 ① 검사기가 자식 커버리지를 못 보고(가짜 실패) ② 새 자식이 붙을
				//   때마다 그 손 cascade 를 사람이 기억해야 한다. Monster/NPC/GameEventListener 와
				//   같은 established 패턴으로 맞춘다(Construct 는 멱등).
				foreach (Player player in FindObjectsByType<Player>(FindObjectsInactive.Include))
					container.InjectGameObject(player.gameObject);
				foreach (InteractiveMarker interactiveMarker in FindObjectsByType<InteractiveMarker>(FindObjectsInactive.Include))
					container.Inject(interactiveMarker);
				foreach (AutoAimMarker autoAimMarker in FindObjectsByType<AutoAimMarker>(FindObjectsInactive.Include))
					container.Inject(autoAimMarker);

				// ★ 씬에 직접 놓인 UI·스포너·풀 오브젝트 — 부모(UIManager/DungeonManager)만 등록돼 있어
				//   그 아래 [Inject] 컴포넌트가 통째로 안 맞았다(NPCObject 때와 같은 갭). 계층 재귀로 수렴.
				foreach (UIManager uiManagerHost in FindObjectsByType<UIManager>(FindObjectsInactive.Include))
					container.InjectGameObject(uiManagerHost.gameObject);
				foreach (DungeonManager dungeonManagerHost in FindObjectsByType<DungeonManager>(FindObjectsInactive.Include))
					container.InjectGameObject(dungeonManagerHost.gameObject);
				foreach (PoolingObject poolingObject in FindObjectsByType<PoolingObject>(FindObjectsInactive.Include))
					container.InjectGameObject(poolingObject.gameObject);
				foreach (LobbyManager lobbyManager in FindObjectsByType<LobbyManager>(FindObjectsInactive.Include))
					container.InjectGameObject(lobbyManager.gameObject);

				// θ — Scene→Root 역방향 .Instance 제거: child scope 가 parent GameManager 에 씬 의존 조건 바인딩.
				BootObserver.Enter(BootPhase.WorldScopeBuilt); // TASK-WM-118 B1
				GameManager gameManager = BootGuard.EagerResolve<GameManager>(container, "Scene");
				GameModeManager gameModeManager = BootGuard.EagerResolve<GameModeManager>(container, "Scene");
				UIManager uiManager = BootGuard.EagerResolve<UIManager>(container, "Scene");
				container.TryResolve(out CameraManager cameraManager);
				gameManager.BindSceneConditions(gameModeManager, uiManager, cameraManager);
				// 입력 전략 선택기 (뿌리) 에 World 씬 매니저를 넘김. 전략이 static Instance 를 찾던 것의 대체
				BootGuard.EagerResolve<InputStrategySelector>(container, "Scene").BindWorld(cameraManager, gameModeManager, uiManager);
				// 뿌리 스코프의 효과 러너에 씬 서비스 (페이드, 대사) 바인딩. 전에는 효과가 static Instance 둘을 찾던 것
				DialogueRunner dialogueRunner = BootGuard.EagerResolve<DialogueRunner>(container, "Scene");
				BootGuard.EagerResolve<IEffectRunner>(container, "Scene").BindScene(uiManager, dialogueRunner);
				BootObserver.Enter(BootPhase.WorldReady); // TASK-WM-118 B1 — 부팅 완료 센티넬 (I5 회귀 판정점)
			});
		}
	}
}

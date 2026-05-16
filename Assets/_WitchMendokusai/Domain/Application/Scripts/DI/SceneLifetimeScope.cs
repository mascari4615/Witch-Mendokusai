using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace WitchMendokusai
{
	public class SceneLifetimeScope : LifetimeScope
	{
		// ★ 진짜 근본 (TASK-WM-078, 2026-05-16) — RegisterComponentInHierarchy<T> 는 BuildScope 시점
		// FindObjectsByType 실행. World.unity 에 *없는* registered 타입 1개가 VContainerException →
		// Build callback 전체 abort → 후속 모든 inject 차단 (마스킹 체인: VoxelInteraction→ChunkManager
		// →GroundGenerator→SpawnerInitializer→…). 개별 제거 = 원 캐스케이드 14커밋 함정 (씬 가변 + 미래 drift).
		// 블라인드 catch = FastFail 위반. 근본 = 등록을 씬 실재에 *동적 일치*: 존재할 때만 등록/Resolve.
		// scene-optional 부재는 정상 (씬 구성 가변), 진짜 누락은 consumer resolve 에서 명확히 실패 (FastFail 보존).
		// RegisterComponentInHierarchy 는 *이 scope 의 씬* 만 검색 (scene-local). FindAnyObjectByType 는 전 씬
		// (DontDestroyOnLoad 포함) — UIRoot(DDOL)+AddComponent MagicBookView 같은 cross-scene 을 false-positive.
		// 헬퍼의 scene-scope 의미를 VContainer 와 정확히 일치 = 이 scope 의 gameObject.scene 한정.
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
			if (IsInScene<T>())
				container.Resolve<T>();
		}

		protected override void Configure(IContainerBuilder builder)
		{
			// 씬 hierarchy 컴포넌트 — 존재 확인 후 등록 (씬 실재 동적 일치).
			RegisterInHierarchyIfPresent<StageManager>(builder);
			RegisterInHierarchyIfPresent<DungeonManager>(builder);
			RegisterInHierarchyIfPresent<UIManager>(builder);
			RegisterInHierarchyIfPresent<CameraManager>(builder);
			RegisterInHierarchyIfPresent<BuildManager>(builder);
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
			RegisterInHierarchyIfPresent<GameEventListener>(builder);
			RegisterInHierarchyIfPresent<ExpManager>(builder);
			RegisterInHierarchyIfPresent<SpawnerInitializer>(builder);
			RegisterInHierarchyIfPresent<UItemEquipPopup>(builder);
			RegisterInHierarchyIfPresent<UIQuestGrid>(builder);
			RegisterInHierarchyIfPresent<MagicBookView>(builder);
			RegisterInHierarchyIfPresent<UINPCMenu>(builder);
			RegisterInHierarchyIfPresent<UIUpgrade>(builder);
			RegisterInHierarchyIfPresent<UIShop>(builder);
			RegisterInHierarchyIfPresent<UICraft>(builder);
			RegisterInHierarchyIfPresent<UIItemSlot>(builder);
			RegisterInHierarchyIfPresent<UIQuestSlot>(builder);
			RegisterInHierarchyIfPresent<ToolTipTrigger>(builder);

			// prefab/code-spawn — FindObjectsByType 무관 (생성형), 존재 확인 불필요.
			DevWindowController devWindowControllerPrefab = Resources.Load<DevWindowController>("Singletons/DevWindowController");
			builder.RegisterComponentInNewPrefab(devWindowControllerPrefab, Lifetime.Scoped);

			CodexWindowController codexWindowControllerPrefab = Resources.Load<CodexWindowController>("Singletons/CodexWindowController");
			builder.RegisterComponentInNewPrefab(codexWindowControllerPrefab, Lifetime.Scoped);

			builder.RegisterComponentOnNewGameObject<GameModeManager>(Lifetime.Scoped, nameof(GameModeManager));
			builder.RegisterComponentOnNewGameObject<DialogueRunner>(Lifetime.Scoped, nameof(DialogueRunner));

			// Lazy 등록 강제 instantiate + raw Instance accessor 셋 트리거 (caller transitional 보존).
			// hierarchy 등록은 존재 확인 후에만 Resolve (부재 시 skip — abort 없음).
			builder.RegisterBuildCallback(container =>
			{
				ResolveIfPresent<StageManager>(container);
				ResolveIfPresent<DungeonManager>(container);
				container.Resolve<DevWindowController>();
				container.Resolve<CodexWindowController>();
				ResolveIfPresent<UIManager>(container);
				ResolveIfPresent<CameraManager>(container);
				ResolveIfPresent<BuildManager>(container);
				ResolveIfPresent<ChatManager>(container);
				ResolveIfPresent<ToolTipPopupManager>(container);
				ResolveIfPresent<UIHoldingSlot>(container);
				container.Resolve<GameModeManager>();
				container.Resolve<DialogueRunner>();
				ResolveIfPresent<CardManager>(container);
				ResolveIfPresent<UINyang>(container);
				ResolveIfPresent<UIWorkableDollCount>(container);
				ResolveIfPresent<UIInteractPopup>(container);
				ResolveIfPresent<UISkillBar>(container);
				ResolveIfPresent<Player>(container);
				ResolveIfPresent<PlayerObject>(container);
				ResolveIfPresent<GameEventListener>(container);
				ResolveIfPresent<ExpManager>(container);
				ResolveIfPresent<SpawnerInitializer>(container);
				ResolveIfPresent<UItemEquipPopup>(container);
				ResolveIfPresent<UIQuestGrid>(container);
				ResolveIfPresent<MagicBookView>(container);
				ResolveIfPresent<UINPCMenu>(container);
				ResolveIfPresent<UIUpgrade>(container);
				ResolveIfPresent<UIShop>(container);
				ResolveIfPresent<UICraft>(container);
				ResolveIfPresent<UIItemSlot>(container);
				ResolveIfPresent<UIQuestSlot>(container);
				ResolveIfPresent<ToolTipTrigger>(container);

				// pool-spawned 컴포넌트가 scene-scope deps (UIManager 등) resolve 가능하게 pool container = scene container.
				container.Resolve<ObjectPoolManager>().SetContainer(container);

				// 씬 직접배치 MonsterObject/ResourceNodeObject (World.unity Dummy/MineralBase 등) — RegisterComponentInHierarchy
				// 단일 FindObjectOfType 한계 우회, 명시 type 다중 인스턴스 cascade Inject.
				foreach (MonsterObject monsterObject in FindObjectsByType<MonsterObject>(FindObjectsInactive.Include))
					container.Inject(monsterObject);
				foreach (ResourceNodeObject resourceNodeObject in FindObjectsByType<ResourceNodeObject>(FindObjectsInactive.Include))
					container.Inject(resourceNodeObject);
				// 씬배치 Player/doll/Marker — Editor-dev(EditorManager additive Stage_Home) 와 production(pooled
				// stage prefab, #4 InjectGameObject) 의 DI 진입을 동일 established 패턴으로 수렴 (발산 제거).
				// Player inject → Player.Construct 가 자식 cascade (PlayerObject/PlayerRotation/DollAnimator/
				// UnitMovement 등). Marker 류는 Player.prefab 자식 아닐 수 있어 명시 (캐스케이드 ac9b1d12 증거). TASK-WM-078 2026-05-16.
				foreach (Player player in FindObjectsByType<Player>(FindObjectsInactive.Include))
					container.Inject(player);
				foreach (InteractiveMarker interactiveMarker in FindObjectsByType<InteractiveMarker>(FindObjectsInactive.Include))
					container.Inject(interactiveMarker);
				foreach (AutoAimMarker autoAimMarker in FindObjectsByType<AutoAimMarker>(FindObjectsInactive.Include))
					container.Inject(autoAimMarker);

				// θ — Scene→Root 역방향 .Instance 제거: child scope 가 parent GameManager 에 씬 의존 조건 바인딩.
				GameManager gameManager = container.Resolve<GameManager>();
				GameModeManager gameModeManager = container.Resolve<GameModeManager>();
				UIManager uiManager = container.Resolve<UIManager>();
				gameManager.BindSceneConditions(gameModeManager, uiManager);
			});
		}
	}
}

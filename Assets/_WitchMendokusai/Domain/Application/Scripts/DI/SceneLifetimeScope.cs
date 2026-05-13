using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace WitchMendokusai
{
	public class SceneLifetimeScope : LifetimeScope
		{
		protected override void Configure(IContainerBuilder builder)
		{
			builder.RegisterComponentInHierarchy<StageManager>();
			builder.RegisterComponentInHierarchy<DungeonManager>();

			DevWindowController devWindowControllerPrefab = Resources.Load<DevWindowController>("Singletons/DevWindowController");
			builder.RegisterComponentInNewPrefab(devWindowControllerPrefab, Lifetime.Scoped);

			CodexWindowController codexWindowControllerPrefab = Resources.Load<CodexWindowController>("Singletons/CodexWindowController");
			builder.RegisterComponentInNewPrefab(codexWindowControllerPrefab, Lifetime.Scoped);

			// γ P2-2 / ζ — B 그룹 8 매니저 (TASK-WM-078, 2026-05-13).
			// scene 의존 ([SerializeField] / Canvas / Cinemachine refs) 6 = RegisterComponentInHierarchy.
			// pure logic / code-spawn 2 (GameModeManager / DialogueRunner) = RegisterComponentOnNewGameObject.
			builder.RegisterComponentInHierarchy<UIManager>();
			builder.RegisterComponentInHierarchy<CameraManager>();
			builder.RegisterComponentInHierarchy<BuildManager>();
			builder.RegisterComponentInHierarchy<ChatManager>();
			builder.RegisterComponentInHierarchy<ToolTipPopupManager>();
			builder.RegisterComponentInHierarchy<UIHoldingSlot>();

			builder.RegisterComponentOnNewGameObject<GameModeManager>(Lifetime.Scoped, nameof(GameModeManager));
			builder.RegisterComponentOnNewGameObject<DialogueRunner>(Lifetime.Scoped, nameof(DialogueRunner));

			// Lifetime.Scoped 의 RegisterComponentInNewPrefab / RegisterComponentOnNewGameObject 는 default lazy — 명시 Resolve 가 없으면 prefab Instantiate 가 일어나지 X.
			// RootLifetimeScope 의 eager Resolve 패턴 따라 build 시점 강제 instantiate.
			// Hierarchy 등록도 raw Instance accessor 셋 트리거 위해 Resolve 호출 (caller transitional 보존).
			builder.RegisterBuildCallback(container =>
			{
				container.Resolve<StageManager>();
				container.Resolve<DungeonManager>();
				container.Resolve<DevWindowController>();
				container.Resolve<CodexWindowController>();
				container.Resolve<UIManager>();
				container.Resolve<CameraManager>();
				container.Resolve<BuildManager>();
				container.Resolve<ChatManager>();
				container.Resolve<ToolTipPopupManager>();
				container.Resolve<UIHoldingSlot>();
				container.Resolve<GameModeManager>();
				container.Resolve<DialogueRunner>();
			});
		}
	}
}

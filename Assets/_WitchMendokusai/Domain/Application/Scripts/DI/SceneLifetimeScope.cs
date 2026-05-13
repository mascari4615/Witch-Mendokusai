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

			DevWindowController devWindowControllerPrefab = Resources.Load<DevWindowController>("Singletons/DevWindowController");
			builder.RegisterComponentInNewPrefab(devWindowControllerPrefab, Lifetime.Scoped);

			CodexWindowController codexWindowControllerPrefab = Resources.Load<CodexWindowController>("Singletons/CodexWindowController");
			builder.RegisterComponentInNewPrefab(codexWindowControllerPrefab, Lifetime.Scoped);

			builder.RegisterComponentInHierarchy<DungeonManager>();

			// Lifetime.Scoped 의 RegisterComponentInNewPrefab 는 default lazy — 명시 Resolve 가 없으면 prefab Instantiate 가 일어나지 X.
			// 결과: DevWindowController / CodexWindowController 인스턴스 0 → / 와 B 단축키 동작 X.
			// RootLifetimeScope 의 eager Resolve 패턴 따라 build 시점 강제 instantiate.
			builder.RegisterBuildCallback(container =>
			{
				container.Resolve<StageManager>();
				container.Resolve<DevWindowController>();
				container.Resolve<CodexWindowController>();
				container.Resolve<DungeonManager>();
			});
		}
	}
}

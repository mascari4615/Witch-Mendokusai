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
		}
	}
}

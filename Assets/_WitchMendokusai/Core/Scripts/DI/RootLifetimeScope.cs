using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace WitchMendokusai
{
	public class RootLifetimeScope : LifetimeScope
	{
		protected override void Configure(IContainerBuilder builder)
		{
			EventBus eventBusPrefab = Resources.Load<EventBus>("Singletons/EventBus");
			builder.RegisterComponentInNewPrefab(eventBusPrefab, Lifetime.Singleton)
				.DontDestroyOnLoad()
				.AsImplementedInterfaces();
		}
	}
}

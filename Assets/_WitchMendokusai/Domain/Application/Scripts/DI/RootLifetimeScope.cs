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

			// γ leaf 매니저 13 등록 (TASK-WM-078 P1, 2026-05-11)
			RegisterLeaf<AudioManager>(builder);
			RegisterLeaf<ShaderPackManager>(builder);
			RegisterLeaf<SkyDirector>(builder);
			RegisterLeaf<GameEventManager>(builder);
			RegisterLeaf<HoldingManager>(builder);
			RegisterLeaf<InputManager>(builder);
			RegisterLeaf<ObjectPoolManager>(builder);
			RegisterLeaf<UnitStatCalculator>(builder);
			RegisterLeaf<CodexPreviewController>(builder);
			RegisterLeaf<WorldClock>(builder);
			RegisterLeaf<PlayerProvider>(builder);
			RegisterLeaf<TimeManager>(builder);
			RegisterLeaf<WeatherSystem>(builder);

			// γ root 매니저 7 등록 (TASK-WM-078 P2, 2026-05-11)
			RegisterLeaf<WindowManager>(builder);
			RegisterLeaf<DataLoader>(builder);
			RegisterLeaf<TooltipController>(builder);
			RegisterLeaf<DataManager>(builder);
			RegisterLeaf<WeatherDirector>(builder);
			RegisterLeaf<GameManager>(builder);
			RegisterLeaf<UIRoot>(builder);
		}

		private static void RegisterLeaf<T>(IContainerBuilder builder) where T : MonoBehaviour
		{
			T prefab = Resources.Load<T>($"Singletons/{typeof(T).Name}");
			builder.RegisterComponentInNewPrefab(prefab, Lifetime.Singleton)
				.DontDestroyOnLoad();
		}
	}
}

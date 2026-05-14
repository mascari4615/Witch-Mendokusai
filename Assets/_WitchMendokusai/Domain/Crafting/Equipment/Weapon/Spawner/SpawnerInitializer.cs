using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace WitchMendokusai
{
	public class SpawnerInitializer : MonoBehaviour
	{
		[SerializeField] private Spawner spawner;
		[SerializeField] private UnitStatType spawnCountStat;

		private PlayerProvider playerProvider;

		[Inject]
		public void Construct(PlayerProvider playerProvider)
		{
			this.playerProvider = playerProvider;
		}

		private void Awake()
		{
			spawner = GetComponent<Spawner>();
			LifetimeScope.Find<SceneLifetimeScope>()?.Container.Inject(this);
		}

		private void Start()
		{
			playerProvider.Current.UnitStat.AddListener(spawnCountStat, OnSpawnCountStatChanged);
			OnSpawnCountStatChanged(playerProvider.Current.UnitStat[spawnCountStat]);
		}

		private void OnSpawnCountStatChanged(int newValue)
		{
			spawner.SpawnCount = 1 + newValue;
		}
	}
}
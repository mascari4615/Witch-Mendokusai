using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public class SpawnerInitializer : MonoBehaviour
	{
		[SerializeField] private Spawner spawner;
		[SerializeField] private UnitStatType spawnCountStat;
		[SerializeField, Min(1)] private int baseCount = 1;

		private PlayerProvider playerProvider;

		[Inject]
		public void Construct(PlayerProvider playerProvider)
		{
			this.playerProvider = playerProvider;
		}

		private void Awake()
		{
			spawner = GetComponent<Spawner>();
		}

		private void OnEnable()
		{
			playerProvider.Current.UnitStat.AddListener(spawnCountStat, OnSpawnCountStatChanged);
			OnSpawnCountStatChanged(playerProvider.Current.UnitStat[spawnCountStat]);
		}

		private void OnDisable()
		{
			playerProvider.Current.UnitStat.RemoveListener(spawnCountStat, OnSpawnCountStatChanged);
		}

		private void OnSpawnCountStatChanged(int newValue)
		{
			spawner.SpawnCount = baseCount + newValue;
		}
	}
}

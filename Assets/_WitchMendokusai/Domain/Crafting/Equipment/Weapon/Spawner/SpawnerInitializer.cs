using UnityEngine;
using VContainer;

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
		}

		private void OnEnable()
		{
			UnitStat playerStat = playerProvider.Current.UnitStat;
			playerStat.AddListener(spawnCountStat, UpdateSpawnCount);
			playerStat.AddListener(UnitStatType.PROJECTILE_COUNT_BONUS, UpdateSpawnCount);
			UpdateSpawnCount();
		}

		private void OnDisable()
		{
			UnitStat playerStat = playerProvider.Current.UnitStat;
			playerStat.RemoveListener(spawnCountStat, UpdateSpawnCount);
			playerStat.RemoveListener(UnitStatType.PROJECTILE_COUNT_BONUS, UpdateSpawnCount);
		}

		private void UpdateSpawnCount()
		{
			UnitStat playerStat = playerProvider.Current.UnitStat;
			spawner.SpawnCount = 1 + playerStat[spawnCountStat] + playerStat[UnitStatType.PROJECTILE_COUNT_BONUS];
		}
	}
}
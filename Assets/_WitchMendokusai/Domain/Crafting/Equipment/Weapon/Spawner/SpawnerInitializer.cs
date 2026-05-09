using UnityEngine;

namespace WitchMendokusai
{
	public class SpawnerInitializer : MonoBehaviour
	{
		[SerializeField] private Spawner spawner;
		[SerializeField] private UnitStatType spawnCountStat;

		private void Awake()
		{
			spawner = GetComponent<Spawner>();
		}

		private void Start()
		{
			PlayerProvider.Instance.Current.UnitStat.AddListener(spawnCountStat, OnSpawnCountStatChanged);
			OnSpawnCountStatChanged(PlayerProvider.Instance.Current.UnitStat[spawnCountStat]);
		}

		private void OnSpawnCountStatChanged(int newValue)
		{
			spawner.SpawnCount = 1 + newValue;
		}
	}
}
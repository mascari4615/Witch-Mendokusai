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
			Player.Instance.UnitStat.AddListener(spawnCountStat, OnSpawnCountStatChanged);
			OnSpawnCountStatChanged(Player.Instance.UnitStat[spawnCountStat]);
		}

		private void OnSpawnCountStatChanged(int newValue)
		{
			spawner.SpawnCount = 1 + newValue;
		}
	}
}
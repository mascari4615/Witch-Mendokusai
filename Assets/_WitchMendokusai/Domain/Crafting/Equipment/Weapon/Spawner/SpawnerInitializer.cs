using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public class SpawnerInitializer : MonoBehaviour
	{
		[SerializeField] private Spawner spawner;
		[SerializeField] private UnitStatType spawnCountStat;
		// 0 이면 자기 수 카드를 받기 전까지 쏘지 않음 (공용 투사체 수 카드만으로는 시작 안 함)
		[SerializeField, Min(0)] private int baseCount = 1;
		// 자기 수가 이보다 적어도 이만큼은 냄 (알리사 완드 빛탄: 바늘을 안 키웠어도 1발)
		[SerializeField, Min(0)] private int minCount = 0;
		// 이 스탯을 받기 전에는 쏘지 않음 (알리사 정령 합창: 정령 수만큼 빛줄기, 마무리 카드가 켬). NONE 이면 늘 켜짐
		[SerializeField] private UnitStatType gateStat = UnitStatType.NONE;

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

		// 구독한 대상에서 해제. Play 종료 때 플레이어가 먼저 파괴돼 Current 가 비어도 OnDisable 이 안전
		private UnitStat subscribedStat;

		private void OnEnable()
		{
			UnitStat playerStat = playerProvider.Current.UnitStat;
			subscribedStat = playerStat;
			playerStat.AddListener(spawnCountStat, UpdateSpawnCount);
			playerStat.AddListener(UnitStatType.PROJECTILE_COUNT_BONUS, UpdateSpawnCount);
			if (gateStat != UnitStatType.NONE)
				playerStat.AddListener(gateStat, UpdateSpawnCount);
			UpdateSpawnCount();
		}

		private void OnDisable()
		{
			subscribedStat?.RemoveListener(spawnCountStat, UpdateSpawnCount);
			subscribedStat?.RemoveListener(UnitStatType.PROJECTILE_COUNT_BONUS, UpdateSpawnCount);
			if (gateStat != UnitStatType.NONE)
				subscribedStat?.RemoveListener(gateStat, UpdateSpawnCount);
			subscribedStat = null;
		}

		private void UpdateSpawnCount()
		{
			UnitStat playerStat = playerProvider.Current.UnitStat;
			if (gateStat != UnitStatType.NONE && playerStat[gateStat] <= 0)
			{
				spawner.SpawnCount = 0;
				return;
			}
			int ownCount = Mathf.Max(minCount, baseCount + playerStat[spawnCountStat]);
			spawner.SpawnCount = ownCount > 0 ? ownCount + playerStat[UnitStatType.PROJECTILE_COUNT_BONUS] : 0;
		}
	}
}

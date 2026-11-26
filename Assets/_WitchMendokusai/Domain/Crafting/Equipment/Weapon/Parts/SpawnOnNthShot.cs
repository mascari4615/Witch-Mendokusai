using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	// 같은 오브젝트의 생성기 (Spawner) 발사를 한 발씩 세어 everyNth 번째마다 주인공 자리에 prefab 생성. 카드 스탯 (gateStat) 이 켬
	// 쓰는 곳: 알리사 한 바퀴 베기 (단도 다섯 번째)
	[RequireComponent(typeof(Spawner))]
	public class SpawnOnNthShot : SkillComponent
	{
		[SerializeField] private UnitStatType gateStat = UnitStatType.NONE;
		[SerializeField, Min(1)] private int everyNth = 5;
		[SerializeField] private GameObject prefab;
		[SerializeField] private UnitStatType damageBonusStat = UnitStatType.NONE;

		private Spawner spawner;
		private SkillContext context;
		private int count;

		private PlayerProvider playerProvider;
		private ObjectPoolManager objectPoolManager;

		[Inject]
		public void Construct(PlayerProvider playerProvider, ObjectPoolManager objectPoolManager)
		{
			this.playerProvider = playerProvider;
			this.objectPoolManager = objectPoolManager;
		}

		public override void InitContext(SkillObject skillObject) => context = skillObject.Context;

		private void Awake() => spawner = GetComponent<Spawner>();

		private void OnEnable()
		{
			count = 0;
			spawner.Shot += HandleShot;
		}

		private void OnDisable() => spawner.Shot -= HandleShot;

		private void HandleShot(GameObject shot, Vector3 direction)
		{
			if (SkillSpawn.Stacks(context, gateStat) <= 0)
				return;
			count = (count + 1) % everyNth;
			if (count != 0)
				return;
			int bonus = damageBonusStat == UnitStatType.NONE ? 0 : SkillSpawn.Stacks(context, damageBonusStat);
			SkillSpawn.Spawn(objectPoolManager, prefab, playerProvider.Current.transform.position, Quaternion.identity, context, bonus).SetActive(true);
		}
	}
}

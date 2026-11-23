using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	// 맞힌 적이 죽으면 그 자리에서 작은 탄 여럿을 사방으로 냄. 작은 탄 프리팹에는 이 부품을 두지 않아 다시 갈라지지 않음
	// 카드 스탯 (gateStat) 을 받아야 켜짐. 쓰는 곳: 알리사 분열 빛탄
	public class SplitOnKill : SkillComponent, IHitEffect
	{
		[SerializeField] private UnitStatType gateStat = UnitStatType.NONE;
		[SerializeField] private GameObject splitPrefab;
		[SerializeField, Min(1)] private int count = 3;

		private SkillContext context;
		private ObjectPoolManager objectPoolManager;

		[Inject]
		public void Construct(ObjectPoolManager objectPoolManager) => this.objectPoolManager = objectPoolManager;

		public override void InitContext(SkillObject skillObject) => context = skillObject.Context;

		public void OnHit(DamagingObject source, UnitObject victim)
		{
			if (victim.IsAlive || SkillSpawn.Stacks(context, gateStat) <= 0)
				return;
			Vector3 position = victim.transform.position;
			float startAngle = Random.Range(0f, 360f);
			for (int i = 0; i < count; i++)
			{
				Vector3 direction = Quaternion.Euler(0f, startAngle + 360f * i / count, 0f) * Vector3.forward;
				GameObject spawned = SkillSpawn.Spawn(objectPoolManager, splitPrefab, position, Quaternion.LookRotation(direction, Vector3.up), context, 0);
				if (spawned.TryGetComponent(out BulletMovement bulletMovement))
					bulletMovement.SetMoveDirection(direction);
				spawned.SetActive(true);
			}
		}
	}
}

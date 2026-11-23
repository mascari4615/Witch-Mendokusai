using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	// 이 탄이 나갈 때 같은 자리, 같은 방향으로 다른 것 하나를 같이 냄. 카드 스탯 (gateStat) 을 받아야 켜짐
	// 쓰는 곳: 알리사 완드 빛 잔상 (베기 자리에 남는 장판)
	public class SpawnWithShot : SkillComponent, IShotReceiver
	{
		[SerializeField] private UnitStatType gateStat = UnitStatType.NONE;
		[SerializeField] private GameObject prefab;
		[SerializeField, Tooltip("이 탄의 크기를 따라감 (넓은 호 같은 크기 카드)")] private bool followScale = true;

		private SkillContext context;
		private ObjectPoolManager objectPoolManager;

		[Inject]
		public void Construct(ObjectPoolManager objectPoolManager) => this.objectPoolManager = objectPoolManager;

		public override void InitContext(SkillObject skillObject) => context = skillObject.Context;

		public void OnShot(ShotInfo shot)
		{
			if (SkillSpawn.Stacks(context, gateStat) <= 0)
				return;
			GameObject spawned = SkillSpawn.Spawn(objectPoolManager, prefab, transform.position, transform.rotation, context, 0);
			if (followScale)
				spawned.transform.localScale = Vector3.Scale(prefab.transform.localScale, transform.localScale);
			spawned.SetActive(true);
		}
	}
}

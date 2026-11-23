using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	// 맞힌 자리에 그림 하나를 냄 (빛 파편, 섬광). 피해와 무관. 방향은 탄에서 적 쪽 (파편이 뒤로 튐)
	// 카드 스탯 (gateStat) 을 받아야 켜짐, NONE 이면 늘 켜짐
	// 쓰는 곳: 알리사 완드 빛 베기의 적중 파편
	public class SpawnOnHit : SkillComponent, IHitEffect
	{
		[SerializeField] private UnitStatType gateStat = UnitStatType.NONE;
		[SerializeField] private GameObject prefab;
		[SerializeField, Tooltip("적 발밑에서 위로")] private float height = 0.8f;

		private SkillContext context;
		private ObjectPoolManager objectPoolManager;

		[Inject]
		public void Construct(ObjectPoolManager objectPoolManager) => this.objectPoolManager = objectPoolManager;

		public override void InitContext(SkillObject skillObject) => context = skillObject.Context;

		public void OnHit(DamagingObject source, UnitObject victim)
		{
			if (SkillSpawn.Stacks(context, gateStat) <= 0)
				return;
			Vector3 victimPosition = victim.transform.position;
			Vector3 away = victimPosition - source.transform.position;
			away.y = 0f;
			Quaternion rotation = away.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(away) : source.transform.rotation;
			GameObject spawned = objectPoolManager.Spawn(prefab, victimPosition + Vector3.up * height, rotation);
			spawned.SetActive(true);
		}
	}
}

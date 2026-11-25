using System.Collections;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	// 몇 번째 투척마다 맞은 적에 표식을 붙이고, 그 동안 무엇에든 죽으면 그 자리에서 터짐. 카드 스탯 (gateStat) 을 받아야 켜짐
	// 투척 수는 이 부품이 붙은 탄끼리 이어 셈 (한 자루가 하나). 쓰는 곳: 알리사 그림자 표식
	public class MarkBurstOnHit : SkillComponent, IHitEffect, IShotReceiver
	{
		[SerializeField] private UnitStatType gateStat = UnitStatType.NONE;
		[SerializeField] private int markKey;
		[SerializeField, Min(1)] private int marksEveryNth = 4;
		[SerializeField, Min(0f)] private float markSeconds = 4f;
		[SerializeField] private GameObject burstPrefab;

		// 나머지만 쓰므로 판이 바뀌어도 되돌릴 필요 없음
		private static int throwCount;

		private SkillContext context;
		private bool marks;

		private ObjectPoolManager objectPoolManager;

		[Inject]
		public void Construct(ObjectPoolManager objectPoolManager) => this.objectPoolManager = objectPoolManager;

		public override void InitContext(SkillObject skillObject)
		{
			context = skillObject.Context;
			marks = false;
		}

		public void OnShot(ShotInfo shot)
		{
			if (SkillSpawn.Stacks(context, gateStat) <= 0)
				return;
			throwCount = (throwCount + 1) % marksEveryNth;
			marks = throwCount == 0;
		}

		public void OnHit(DamagingObject source, UnitObject victim)
		{
			if (marks == false)
				return;
			if (victim.IsAlive == false)
			{
				Burst(objectPoolManager, burstPrefab, victim.transform.position, context);
				return;
			}
			bool watching = HitMarks.IsMarked(markKey, victim);
			HitMarks.Mark(markKey, victim, markSeconds);
			if (watching == false)
				CombatFxRunner.Instance.Run(Watch(victim, markKey, objectPoolManager, burstPrefab, context));
		}

		// 이 탄은 풀로 돌아가므로 실행기에서 지켜봄. 죽으면 바로 끝내 풀에서 다시 나온 적에 이어지지 않게
		private static IEnumerator Watch(UnitObject victim, int markKey, ObjectPoolManager objectPoolManager, GameObject burstPrefab, SkillContext context)
		{
			while (victim != null && HitMarks.IsMarked(markKey, victim))
			{
				if (victim.IsAlive == false || victim.isActiveAndEnabled == false)
				{
					HitMarks.Mark(markKey, victim, 0f);
					Burst(objectPoolManager, burstPrefab, victim.transform.position, context);
					yield break;
				}
				yield return null;
			}
		}

		private static void Burst(ObjectPoolManager objectPoolManager, GameObject prefab, Vector3 position, SkillContext context)
			=> SkillSpawn.Spawn(objectPoolManager, prefab, position, Quaternion.identity, context, 0).SetActive(true);
	}
}

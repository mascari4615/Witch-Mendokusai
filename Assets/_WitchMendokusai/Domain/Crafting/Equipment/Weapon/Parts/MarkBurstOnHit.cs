using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	// 몇 번째 투척마다 맞은 적에 표식을 붙이고, 표식이 붙은 적이 이 탄에 죽으면 그 자리에서 터짐. 카드 스탯 (gateStat) 을 받아야 켜짐
	// 표식은 HitMarks. 투척 수는 같은 counterKey 를 쓰는 탄끼리 이어 셈. 쓰는 곳: 알리사 그림자 표식
	public class MarkBurstOnHit : SkillComponent, IHitEffect, IShotReceiver
	{
		[SerializeField] private UnitStatType gateStat = UnitStatType.NONE;
		[SerializeField] private int markKey;
		[SerializeField, Min(0), Tooltip("이 번째 투척마다 표식. 0 이면 터뜨리기만")] private int marksEveryNth = 4;
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
			if (marksEveryNth <= 0 || SkillSpawn.Stacks(context, gateStat) <= 0)
				return;
			throwCount = (throwCount + 1) % marksEveryNth;
			marks = throwCount == 0;
		}

		public void OnHit(DamagingObject source, UnitObject victim)
		{
			if (SkillSpawn.Stacks(context, gateStat) <= 0)
				return;
			if (victim.IsAlive == false)
			{
				if (marks || HitMarks.IsMarked(markKey, victim))
					SkillSpawn.Spawn(objectPoolManager, burstPrefab, victim.transform.position, Quaternion.identity, context, 0).SetActive(true);
				return;
			}
			if (marks)
				HitMarks.Mark(markKey, victim, markSeconds);
		}
	}
}

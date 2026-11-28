using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	// 강화 일격. 몸을 따라다니며 기다리다가 사거리 안에 적이 처음 들어오면 그 적 자리에 일격 프리팹 (판정, 상태이상, 터짐) 을 내고 꺼짐
	// 기다리는 동안의 이동속도 상승은 같은 탄의 StatWhileActive, 끝 시각은 ObjectWithTime (적이 안 오면 그냥 꺼짐)
	// 내는 법은 ExplodeOnEnd 와 같은 꼴 (풀에서 꺼내 맥락을 넘기고 켬)
	public class SkillMaterialEmpoweredStrike : SkillComponent
	{
		[SerializeField] private GameObject strikePrefab;
		[SerializeField, Min(0.1f)] private float range = 3f;

		private SkillObject owner;
		private bool struck;
		private ObjectPoolManager objectPoolManager;

		[Inject]
		public void Construct(ObjectPoolManager objectPoolManager)
		{
			this.objectPoolManager = objectPoolManager;
		}

		public override void InitContext(SkillObject skillObject)
		{
			owner = skillObject;
			struck = false;
		}

		private void Update()
		{
			if (struck || owner == null || owner.Context == null)
				return;
			GameObject target = CompanionTargeting.NearestMonster(transform.position, range);
			if (target == null)
				return;
			struck = true;
			GameObject strike = objectPoolManager.Spawn(strikePrefab, target.transform.position, Quaternion.LookRotation(Flat(target.transform.position - transform.position)));
			if (strike.TryGetComponent(out SkillObject skillObject))
				skillObject.InitContext(owner.Context);
			strike.SetActive(true);
			gameObject.SetActive(false);
		}

		private static Vector3 Flat(Vector3 offset)
		{
			offset.y = 0f;
			return offset.sqrMagnitude < 0.0001f ? Vector3.forward : offset.normalized;
		}
	}
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static WitchMendokusai.WMHelper;

namespace WitchMendokusai
{
	public class SkillObject : MonoBehaviour
	{
		[field: Header("Context")]
		public SkillContext Context { get; private set; }
		public bool UsedByPlayer { get; private set; }

		private SkillComponent[] skillComponents;
		private Vector3 baseScale;
		private bool hasBaseScale;

		private void OnEnable()
		{
			ObjectBufferManager.AddObject(ObjectType.Skill, gameObject);
		}

		private void OnDisable()
		{
			ObjectBufferManager.RemoveObject(ObjectType.Skill, gameObject);
		}

		public void InitContext(SkillContext context)
		{
			Context = context;
			UsedByPlayer = context.User is PlayerObject;

			skillComponents = GetComponentsInChildren<SkillComponent>(true);

			foreach (SkillComponent skillComponent in skillComponents)
				skillComponent.InitContext(this);
		}

		// 공격 판정 초기화, 공격 범위 강화 적용. 풀 재사용이라 처음 크기 기준으로 계산
		public void InitAttack(SkillContext context)
		{
			InitContext(context);
			if (hasBaseScale == false)
			{
				baseScale = transform.localScale;
				hasBaseScale = true;
			}
			transform.localScale = baseScale * context.User.UnitStat.PercentMultiplier(UnitStatType.PROJECTILE_SCALE_BONUS);
		}
	}
}
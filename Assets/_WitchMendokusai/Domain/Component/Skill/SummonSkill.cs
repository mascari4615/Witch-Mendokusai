using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = nameof(SummonSkill), menuName = "WM/Skill/SummonSkill")]
	public class SummonSkill : SkillData
	{
		[field: SerializeField] public GameObject Prefab { get; private set; }
		[field: SerializeField] public bool SetRotation { get; private set; }

		// TASK-WM-107 Slice 4 — static Bridge 폐기, ctx 경유 (behavior 무변경: ctx 서비스 = Bridge 와 동일 VContainer 싱글턴).
		public override void ActualUse(SkillContext context)
		{
			GameObject o = context.ObjectPoolManager.Spawn(Prefab);
			o.transform.position = context.User.transform.position;

			if (SetRotation)
			{
				// 공격 위치를 향하도록 회전
				o.transform.rotation = Quaternion.LookRotation(context.PlayerProvider.Current.AimDirection);
			}

			if (o.TryGetComponent(out SkillObject skillObject))
				skillObject.InitContext(context);

			o.SetActive(true);
		}
	}
}
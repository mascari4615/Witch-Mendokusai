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

		public override void ActualUse(SkillContext context)
		{
			GameObject o = ObjectPoolManagerBridge.Spawn(Prefab);
			o.transform.position = context.User.transform.position;

			if (SetRotation)
			{
				// 공격 위치를 향하도록 회전
				o.transform.rotation = Quaternion.LookRotation(PlayerProviderBridge.Current.AimDirection);
			}

			if (o.TryGetComponent(out SkillObject skillObject))
				skillObject.InitContext(context);

			o.SetActive(true);
		}
	}
}
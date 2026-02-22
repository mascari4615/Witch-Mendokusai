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

		public override void ActualUse(UnitObject unitObject)
		{
			GameObject o = ObjectPoolManager.Instance.Spawn(Prefab);
			o.transform.position = unitObject.transform.position;

			if (SetRotation)
			{
				// 공격 위치를 향하도록 회전
				o.transform.rotation = Quaternion.LookRotation(Player.Instance.AimDirection);
			}

			if (o.TryGetComponent(out SkillObject skillObject))
				skillObject.InitContext(unitObject);

			o.SetActive(true);
		}
	}
}
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
				// WM-165: 전술 타겟(context.Target) 우선 조준, 없으면 레거시 플레이어 조준(Current null 가드).
				Vector3? targetPosition = context.Target != null ? context.Target.transform.position : (Vector3?)null;
				Vector3? fallbackAim = (context.PlayerProvider != null && context.PlayerProvider.Current != null)
					? context.PlayerProvider.Current.AimDirection
					: (Vector3?)null;
				o.transform.rotation = Quaternion.LookRotation(
					ProjectileAim.Resolve(o.transform.position, targetPosition, fallbackAim, o.transform.forward));
			}

			if (o.TryGetComponent(out SkillObject skillObject))
				skillObject.InitContext(context);

			o.SetActive(true);
		}
	}
}
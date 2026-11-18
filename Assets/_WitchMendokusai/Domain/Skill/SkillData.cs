using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public enum SkillPlayMode
	{
		Manual = 0,
		Auto = 1,
		AutoWhenDungeon = 2,
	}

	// TASK-WM-107 Slice 4 — POCO Skill 이 static Bridge 안 알도록 app 서비스를 ctx 운반
	// (EffectContext/CriteriaContext 동형). PlayerProvider/ObjectPoolManager 는
	// VContainer 싱글턴 — *Bridge.Register(this) 동일 인스턴스라 behavior 무변경.
	public class SkillContext
	{
		public UnitObject User { get; private set; }
		// WM-165 — 아레나 전술 코어가 지정한 타겟. null = 레거시(플레이어 조준/타겟 없음).
		public UnitObject Target { get; private set; }
		public PlayerProvider PlayerProvider { get; private set; }
		public ObjectPoolManager ObjectPoolManager { get; private set; }
		public IItemData UsedEquipment { get; private set; }

		public SkillContext(UnitObject user, PlayerProvider playerProvider, ObjectPoolManager objectPoolManager, IItemData usedEquipment = null, UnitObject target = null)
		{
			User = user;
			Target = target;
			PlayerProvider = playerProvider;
			ObjectPoolManager = objectPoolManager;
			UsedEquipment = usedEquipment;
		}
	}

	public abstract class SkillData : DataSO
	{
		[field: SerializeField] public SkillPlayMode PlayMode { get; set; }
		[field: SerializeField] public float Cooltime { get; set; }
		[field: SerializeField] public float PrevDelay { get; set; } = 0;
		[field: SerializeField] public float AfterDelay { get; set; } = 0;

		// 바닥 예고. PrevDelay 동안 시전자 자리에서 재생. 부품이 비었거나 크기가 전부 0 이면 예고 없음
		[field: Header("예고 (PrevDelay 동안)")]
		[field: SerializeField, Tooltip("PrevDelay 동안 바닥에 보일 예고 부품. 비우면 예고 없음")] public CombatFx Telegraph { get; private set; }
		[field: SerializeField, Tooltip("원형 예고 반지름 (월드 단위). 0 이면 안 씀")] public float TelegraphRadius { get; private set; } = 0;
		[field: SerializeField, Tooltip("선형 예고 길이 (월드 단위). 0 이면 안 씀")] public float TelegraphLength { get; private set; } = 0;
		[field: SerializeField, Tooltip("선형 예고 폭 (월드 단위)")] public float TelegraphWidth { get; private set; } = 0;

		private bool HasTelegraph => Telegraph != null && (TelegraphRadius > 0 || TelegraphLength > 0);

		public void Use(SkillContext context)
		{
			context.User.StartCoroutine(SkillCoroutine(context));
		}

		public IEnumerator SkillCoroutine(SkillContext context)
		{
			yield return null;

			UnitObject unitObject = context.User;

			if (PrevDelay > 0)
			{
				unitObject.UnitStat[UnitStatType.CASTING_SKILL]++;
				if (HasTelegraph)
					PlayTelegraph(context);
				yield return new WaitForSeconds(PrevDelay);
				unitObject.UnitStat[UnitStatType.CASTING_SKILL]--;
			}

			ActualUse(context);

			if (AfterDelay > 0)
			{
				unitObject.UnitStat[UnitStatType.CASTING_SKILL]++;
				yield return new WaitForSeconds(AfterDelay);
				unitObject.UnitStat[UnitStatType.CASTING_SKILL]--;
			}
		}

		/// <summary> 시전자 자리에서 PrevDelay 길이로 예고 재생. 방향은 이 순간의 대상 쪽으로 고정 </summary>
		private void PlayTelegraph(SkillContext context)
		{
			Transform userTransform = context.User.transform;
			Telegraph.Play(new CombatFxContext
			{
				Position = userTransform.position,
				Direction = ResolveTelegraphDirection(context),
				Follow = userTransform,
				Radius = TelegraphRadius,
				Length = TelegraphLength,
				Width = TelegraphWidth,
				Seconds = PrevDelay,
			});
		}

		// 탄환 조준 (BulletMovement) 과 같은 순서: 지정 대상, 플레이어가 쓰면 조준 방향, 그 밖은 플레이어 쪽
		private static Vector3 ResolveTelegraphDirection(SkillContext context)
		{
			Transform userTransform = context.User.transform;
			Vector3 direction = userTransform.forward;
			if (context.Target != null)
				direction = context.Target.transform.position - userTransform.position;
			else if (context.PlayerProvider != null && context.PlayerProvider.Current != null)
				direction = context.User is PlayerObject
					? context.PlayerProvider.Current.AimDirection
					: context.PlayerProvider.Current.transform.position - userTransform.position;

			direction.y = 0f;
			// 대상과 겹쳐 방향이 없으면 시전자가 보는 쪽
			return direction.sqrMagnitude > 0f ? direction : userTransform.forward;
		}

		public abstract void ActualUse(SkillContext context);
	}
}
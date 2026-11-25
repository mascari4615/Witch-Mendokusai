using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 적의 정면 외 위협 한 종류 (원거리, 돌진, 장판 등). 일반 적, 정예, 보스가 같이 씀. 몬스터 자산 (Monster.Attack) 에 끼우면 FSMSlime 이 추적 대신 이것을 돌림
	// 공통 박자: 사거리 안 + 준비됨 -> 멈추고 예고 (WindupSeconds) -> 실행 -> 숨 고르기 (RecoverySeconds) -> 대기 (Cooldown, 그동안 추적)
	// 예고 뒤 한 번 실행하는 종류는 WindupMonsterAttack, 돌진은 기존 돌진 행동 (BT_Dash) 을 씀. 정본 memo wm/design/crossing-threat-20261119 th6
	public abstract class MonsterAttack : ScriptableObject
	{
		/// <summary>도감에 보이는 공격 종류 이름</summary>
		public abstract string KindName { get; }

		[field: Header("박자")]
		[field: SerializeField, Tooltip("이 거리 안에 들어오면 준비 시작 (m)"), Min(0.5f)] public float TriggerRange { get; private set; } = 6f;
		[field: SerializeField, Tooltip("멈춰서 예고하는 초. 피할 틈"), Min(0f)] public float WindupSeconds { get; private set; } = 0.8f;
		[field: SerializeField, Tooltip("실행 뒤 멈춰 있는 초. 반격 틈"), Min(0f)] public float RecoverySeconds { get; private set; } = 0.6f;
		[field: SerializeField, Tooltip("한 번 실행 뒤 다음 준비까지 (초). 그동안은 추적"), Min(0f)] public float Cooldown { get; private set; } = 3f;

		[field: Header("예고")]
		[field: SerializeField, Tooltip("바닥 예고 부품. Seconds 는 WindupSeconds")] public CombatFx WindupTelegraph { get; private set; }

		[field: SerializeField, Tooltip("숨 고르기 동안 발밑 표시 (반격 창). 비우면 없음")] public CombatFx RecoveryFx { get; private set; }
		[field: SerializeField, Tooltip("반격 창 반지름 (m)"), Min(0.1f)] public float RecoveryFxRadius { get; private set; } = 2f;

		[field: Header("몸과 물체")]
		[field: SerializeField] public AttackPose Pose { get; private set; } = new();
		[field: SerializeField, Tooltip("예고 동안 나타나는 물체 (날아오는 것, 땅에서 돋는 것). 때리는 자리마다 하나, Seconds 가 때리는 순간")] public CombatFx WindupFx { get; private set; }
		[field: SerializeField, Tooltip("때리는 순간 이펙트. 빗나가도 나옴. 때리는 자리마다 하나")] public CombatFx StrikeFx { get; private set; }

		// 이 종류의 행동이 실제로 재생하는가. 안 하면 끼웠을 때 검사가 알림
		protected virtual bool PlaysWindupFx => false;
		protected virtual bool PlaysStrikeFx => false;

		// 이 공격을 돌릴 행동 트리. 몬스터가 켜질 때마다 새로
		public abstract BTRunner CreateBehaviour(UnitObject unitObject, PlayerProvider playerProvider);

		// 행동이 도중에 끊길 때 (사망, 풀 반납, 패턴 이동, 재배치). 예고 정리
		public abstract void Interrupt(BTRunner behaviour);

		// 숨 고르기 시작에 반격 창 표시. 돌려받은 함수로 도중에 치움
		public Action PlayRecovery(UnitObject unitObject)
		{
			if (RecoveryFx == null || RecoverySeconds <= 0f)
				return null;
			return RecoveryFx.Play(new CombatFxContext
			{
				Position = unitObject.transform.position,
				Follow = unitObject.transform,
				Radius = RecoveryFxRadius,
				Seconds = RecoverySeconds,
			});
		}

		// 몸동작. 자세가 비었으면 몸에 아무것도 안 붙임
		public void PlayWindupPose(UnitObject unitObject, float seconds)
		{
			if (Pose.IsEmpty == false)
				MonsterPoseDriver.Of(unitObject).Windup(Pose, seconds);
		}

		// owner 는 이 공격을 낀 몬스터 자산
		public virtual void Validate(Monster owner, List<string> errors)
		{
			if (WindupSeconds > 0f && WindupTelegraph == null)
				errors.Add($"{name}: 예고 초 {WindupSeconds} 인데 {nameof(WindupTelegraph)} 비어 있음");
			if (RecoveryFx != null && RecoverySeconds <= 0f)
				errors.Add($"{name}: 반격 창 표시가 있는데 숨 고르기 초가 0");
			if ((WindupFx != null && PlaysWindupFx == false) || (StrikeFx != null && PlaysStrikeFx == false))
				errors.Add($"{name}: {KindName} 은 이 물체나 타격 이펙트를 재생하지 않음");
			if (WindupFx != null && WindupSeconds <= 0f)
				errors.Add($"{name}: 예고 물체가 있는데 예고 초가 0");
			// 애니메이터가 도는 몸은 매 프레임 그림을 덮음
			if (Pose.HasSprite && (owner.Sprites == null || owner.Sprites.Count == 0))
				errors.Add($"{name}: 자세 그림이 있는데 {owner.name} 이 애니메이터 몸 ({nameof(owner.Sprites)} 비어 있음)");
			// 공격을 돌리는 것은 FSMSlime 뿐. 다른 UnitBrain 프리팹이면 공격 없이 추적만 함
			if (owner.Prefab == null || owner.Prefab.GetComponent<FSMSlime>() == null)
				errors.Add($"{owner.name}: 공격 {name} 을 돌릴 {nameof(FSMSlime)} 이 프리팹에 없음");
		}

		// 공격 피해 한 건. 시간 상승, 적 피해 %, 위협 배율은 다른 적 피해와 같은 길 (DamagingObject.ScaleEnemyDamage)
		public static DamageInfo MakeDamage(UnitObject source, int baseDamage) => new()
		{
			type = DamageType.Normal,
			damage = DamagingObject.ScaleEnemyDamage(baseDamage, source),
			damageSource = source,
			equipmentDataId = DamageInfo.NO_DATA_ID,
			skillDataId = DamageInfo.NO_DATA_ID,
		};
	}
}

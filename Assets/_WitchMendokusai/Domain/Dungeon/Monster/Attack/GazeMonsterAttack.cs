using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 시선. 예고가 차는 순간 둘레 원 안의 주인공이 걷는 쪽 (UnitMovement.LookDirection) 으로 시전자를 보고 있으면 맞음. 등을 돌리면 안전
	// 걷는 쪽은 마지막으로 걸은 쪽이라 멈춰 서도 시전자를 향해 있으면 맞음. 판정은 실행 순간 한 번만
	[CreateAssetMenu(fileName = "MA_Gaze", menuName = "WM/Variable/MonsterAttack/" + nameof(GazeMonsterAttack))]
	public class GazeMonsterAttack : WindupMonsterAttack
	{
		public override string KindName => "시선";

		[field: Header("시선")]
		[field: SerializeField, Tooltip("이 거리 안의 주인공만 봄 (m)"), Min(0.5f)] public float Range { get; private set; } = 7f;
		[field: SerializeField, Tooltip("걷는 쪽과 시전자 쪽 사이 각이 이 각의 절반 안이면 마주 본 것 (도)"), Range(10f, 180f)] public float GazeDegrees { get; private set; } = 100f;
		[field: SerializeField, Tooltip("기본 피해. 효과는 피해가 실제로 들어가야 걸림"), Min(1)] public int Damage { get; private set; } = 1;
		[field: SerializeField, Tooltip("피해가 실제로 들어간 순간 주인공에게 거는 것 (둔화 등)")] public MonsterHitEffect HitEffect { get; private set; } = new();

		// 둘레 원이 시전자를 따라감. 고정하는 것은 쓰지 않는 방향
		public override void ResolveTargets(UnitObject unitObject, Vector3 playerPosition, Vector3 moveVelocity, List<Vector3> results) =>
			results.Add(ScatterZoneMonsterAttack.FlatDirection(unitObject.transform.position, playerPosition));

		protected override bool TargetsArePositions => false;

		public override CombatFxContext WindupContext(UnitObject unitObject, Vector3 target, int index) => new()
		{
			Position = unitObject.transform.position,
			Follow = unitObject.transform,
			Direction = target,
			Radius = Range,
			Seconds = WindupSeconds,
		};

		public override bool Execute(UnitObject unitObject, PlayerObject player, Vector3 target, int index)
		{
			Vector3 caster = unitObject.transform.position;
			if (player.IsAlive == false || SlimeKingPatternMath.IsWithin(caster, player.transform.position, Range + player.UnitMovement.BodyRadius) == false)
				return false;
			if (IsFacing(player.UnitMovement.LookDirection, player.transform.position, caster, GazeDegrees) == false)
				return false;
			HitEffect.Strike(unitObject, caster, player, MakeDamage(unitObject, Damage));
			return true;
		}

		// look 쪽으로 caster 를 보고 있나. 높이는 무시. 겹쳐 있으면 늘 본 것
		public static bool IsFacing(Vector3 look, Vector3 viewer, Vector3 caster, float gazeDegrees)
		{
			Vector3 toCaster = caster - viewer;
			toCaster.y = 0f;
			look.y = 0f;
			if (toCaster.sqrMagnitude < SlimeKingPatternMath.DIRECTION_EPSILON_SQR)
				return true;
			if (look.sqrMagnitude < SlimeKingPatternMath.DIRECTION_EPSILON_SQR)
				return false;
			return Vector3.Angle(look, toCaster) <= gazeDegrees * 0.5f;
		}

		public override void Validate(Monster owner, List<string> errors)
		{
			base.Validate(owner, errors);
			if (WindupSeconds <= 0f)
				errors.Add($"{name}: 시선은 예고가 있어야 등을 돌릴 수 있음 ({nameof(WindupSeconds)} 0)");
			if (TriggerRange > Range)
				errors.Add($"{name}: 사거리 {TriggerRange} 가 보는 거리 {Range} 보다 커 시작해도 못 봄");
			HitEffect.Validate(name, errors);
		}
	}
}

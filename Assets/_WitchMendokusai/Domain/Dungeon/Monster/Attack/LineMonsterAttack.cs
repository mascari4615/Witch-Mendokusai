using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 직선 띠. 준비를 시작한 순간 방향을 고정, 예고가 차면 띠 안 주인공에게 피해 한 번. 각도 여럿이면 부채처럼, 간격 (StaggerSeconds) 이 있으면 차례로 쓸기
	// 고정하는 것 (target) 은 자리가 아니라 방향. 예고 중 밀려도 예고와 판정이 시전자를 같이 따라감
	// 따라 도는 초가 있으면 예고 시작부터 그 초까지 주인공 쪽으로 돌다가 고정 (고정 순간 LockFx). 0 이면 준비 시작에 고정
	[CreateAssetMenu(fileName = "MA_Line", menuName = "WM/Variable/MonsterAttack/" + nameof(LineMonsterAttack))]
	public class LineMonsterAttack : WindupMonsterAttack
	{
		public override string KindName => "직선 띠";

		[field: Header("직선 띠")]
		[field: SerializeField, Tooltip("길이 (m)"), Min(0.5f)] public float Length { get; private set; } = 9f;
		[field: SerializeField, Tooltip("폭 (m)"), Min(0.2f)] public float Width { get; private set; } = 1.4f;
		[field: SerializeField, Tooltip("주인공 쪽에서 벌어진 각도 (도). 하나마다 띠 하나")] public List<float> Angles { get; private set; } = new() { 0f };
		[field: SerializeField, Tooltip("띠마다 늦어지는 초. 0 이면 동시. 예고는 처음에 모두 보이고 순서대로 차오름"), Min(0f)] public float StaggerSeconds { get; private set; }
		[field: SerializeField, Tooltip("기본 피해"), Min(1)] public int Damage { get; private set; } = 4;
		[field: SerializeField, Tooltip("피해가 실제로 들어간 순간 주인공에게 거는 것")] public MonsterHitEffect HitEffect { get; private set; } = new();

		[field: Header("따라 도는 조준")]
		[field: SerializeField, Tooltip("예고 시작부터 이 초까지 주인공 쪽으로 돎. 0 이면 준비 시작에 고정. 예고 초보다 짧아야 고정 뒤 피할 틈이 있음"), Min(0f)] public float TrackSeconds { get; private set; }
		[field: SerializeField, Tooltip("따라 도는 빠르기 상한 (초당 도). 빨리 돌아 뛰면 조준이 뒤처짐"), Min(1f)] public float TrackDegreesPerSecond { get; private set; } = 90f;
		[field: SerializeField, Tooltip("고정 순간 같은 띠에 띄우는 다른 색 예고. 따라 돌면 반드시")] public CombatFx TrackLockFx { get; private set; }

		public override float TrackingSeconds => TrackSeconds;
		public override CombatFx LockFx => TrackLockFx;

		// 각도를 지킨 채 주인공 쪽으로 빠르기 상한만큼 돎
		public override Vector3 Track(UnitObject unitObject, Vector3 playerPosition, Vector3 target, int index, float deltaTime)
		{
			Vector3 wanted = Quaternion.Euler(0f, Angles[index], 0f) * ScatterZoneMonsterAttack.FlatDirection(unitObject.transform.position, playerPosition);
			return TurnToward(target, wanted, TrackDegreesPerSecond * deltaTime);
		}

		// 수평 단위 벡터를 wanted 쪽으로 최대 maxDegrees 만큼
		public static Vector3 TurnToward(Vector3 current, Vector3 wanted, float maxDegrees) =>
			Vector3.RotateTowards(current, wanted, maxDegrees * Mathf.Deg2Rad, 0f);

		public override void ResolveTargets(UnitObject unitObject, Vector3 playerPosition, Vector3 moveVelocity, List<Vector3> results)
		{
			Vector3 toPlayer = ScatterZoneMonsterAttack.FlatDirection(unitObject.transform.position, playerPosition);
			foreach (float angle in Angles)
				results.Add(Quaternion.Euler(0f, angle, 0f) * toPlayer);
		}

		public override float StrikeDelay(int index) => StaggerSeconds * index;

		protected override bool TargetsArePositions => false;

		public override CombatFxContext WindupContext(UnitObject unitObject, Vector3 target, int index) => new()
		{
			Position = unitObject.transform.position,
			Direction = target,
			Follow = unitObject.transform,
			Length = Length,
			Width = Width,
			Seconds = WindupSeconds,
		};

		public override bool Execute(UnitObject unitObject, PlayerObject player, Vector3 target, int index)
		{
			Vector3 origin = unitObject.transform.position;
			if (player.IsAlive == false || IsInStrip(origin, target, Length, Width, player.transform.position, player.UnitMovement.BodyRadius) == false)
				return false;
			HitEffect.Strike(unitObject, origin, player, MakeDamage(unitObject, Damage));
			return true;
		}

		// 띠 안인가. 높이는 무시, direction 은 수평 단위 벡터
		public static bool IsInStrip(Vector3 origin, Vector3 direction, float length, float width, Vector3 point, float bodyRadius)
		{
			Vector3 offset = point - origin;
			offset.y = 0f;
			float along = Vector3.Dot(offset, direction);
			if (along < -bodyRadius || along > length + bodyRadius)
				return false;
			return (offset - direction * along).magnitude <= width * 0.5f + bodyRadius;
		}

		public override void Validate(Monster owner, List<string> errors)
		{
			base.Validate(owner, errors);
			if (WindupSeconds <= 0f)
				errors.Add($"{name}: 직선 띠는 예고가 있어야 피할 수 있음 ({nameof(WindupSeconds)} 0)");
			if (Angles.Count == 0)
				errors.Add($"{name}: 각도가 비어 띠가 없음");
			if (TrackSeconds > 0f && TrackSeconds >= WindupSeconds)
				errors.Add($"{name}: 따라 도는 초 {TrackSeconds} 가 예고 {WindupSeconds} 이상이라 고정 뒤 피할 틈이 없음");
			if (TrackSeconds > 0f && TrackLockFx == null)
				errors.Add($"{name}: 따라 도는데 고정 순간 표시 ({nameof(TrackLockFx)}) 가 비어 있음");
			HitEffect.Validate(name, errors);
		}
	}
}

using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 직선 띠. 준비를 시작한 순간 방향을 고정, 예고가 차면 띠 안 주인공에게 피해 한 번. 각도 여럿이면 부채처럼, 간격 (StaggerSeconds) 이 있으면 차례로 쓸기
	// 고정하는 것 (target) 은 자리가 아니라 방향. 예고 중 밀려도 예고와 판정이 시전자를 같이 따라감
	[CreateAssetMenu(fileName = "MA_Line", menuName = "WM/Variable/MonsterAttack/" + nameof(LineMonsterAttack))]
	public class LineMonsterAttack : WindupMonsterAttack
	{
		[field: Header("직선 띠")]
		[field: SerializeField, Tooltip("길이 (m)"), Min(0.5f)] public float Length { get; private set; } = 9f;
		[field: SerializeField, Tooltip("폭 (m)"), Min(0.2f)] public float Width { get; private set; } = 1.4f;
		[field: SerializeField, Tooltip("주인공 쪽에서 벌어진 각도 (도). 하나마다 띠 하나")] public List<float> Angles { get; private set; } = new() { 0f };
		[field: SerializeField, Tooltip("띠마다 늦어지는 초. 0 이면 동시. 예고는 처음에 모두 보이고 순서대로 차오름"), Min(0f)] public float StaggerSeconds { get; private set; }
		[field: SerializeField, Tooltip("기본 피해"), Min(1)] public int Damage { get; private set; } = 4;
		[field: SerializeField, Tooltip("피해가 실제로 들어간 순간 주인공에게 거는 것")] public MonsterHitEffect HitEffect { get; private set; } = new();

		public override void ResolveTargets(UnitObject unitObject, Vector3 playerPosition, Vector3 moveVelocity, List<Vector3> results)
		{
			Vector3 toPlayer = ScatterZoneMonsterAttack.FlatDirection(unitObject.transform.position, playerPosition);
			foreach (float angle in Angles)
				results.Add(Quaternion.Euler(0f, angle, 0f) * toPlayer);
		}

		public override float StrikeDelay(int index) => StaggerSeconds * index;

		public override CombatFxContext WindupContext(UnitObject unitObject, Vector3 target) => new()
		{
			Position = unitObject.transform.position,
			Direction = target,
			Follow = unitObject.transform,
			Length = Length,
			Width = Width,
			Seconds = WindupSeconds,
		};

		public override void Execute(UnitObject unitObject, PlayerObject player, List<Vector3> targets)
		{
			if (player.IsAlive == false)
				return;
			Vector3 origin = unitObject.transform.position;
			foreach (Vector3 direction in targets)
			{
				if (IsInStrip(origin, direction, Length, Width, player.transform.position, player.UnitMovement.BodyRadius) == false)
					continue;
				HitEffect.Strike(unitObject, origin, player, MakeDamage(unitObject, Damage));
				return;
			}
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
			HitEffect.Validate(name, errors);
		}
	}
}

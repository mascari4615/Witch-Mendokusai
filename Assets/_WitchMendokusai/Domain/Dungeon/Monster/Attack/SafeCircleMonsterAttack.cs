using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public enum SafeCircleStart
	{
		// 준비를 시작한 순간 주인공 자리. 처음엔 원 안에 있음
		Target = 0,
		// 시전자 자리. 원까지 걸어 들어가야 함
		Caster = 1,
	}

	// 따라다니는 안전 원. 예고 뒤 원이 정한 길을 따라 움직이고, 그동안 원 밖의 주인공은 간격마다 맞음. 정본 memo wm/design/boss-arena-20261126
	// 길은 시작 자리 기준의 꺾인 점들. 앞 (+y) 은 원이 나오는 순간 (예고 끝) 시전자에서 주인공 쪽. 원은 SafeCircleField 가 들고 감 (공격 자산은 상태 없음)
	[CreateAssetMenu(fileName = "MA_SafeCircle", menuName = "WM/Variable/MonsterAttack/" + nameof(SafeCircleMonsterAttack))]
	public class SafeCircleMonsterAttack : WindupMonsterAttack
	{
		public override string KindName => "따라다니는 안전 원";

		[field: Header("안전 원")]
		[field: SerializeField] public SafeCircleStart Start { get; private set; } = SafeCircleStart.Target;
		[field: SerializeField, Tooltip("원 반지름 (m). 주인공 중심이 이 안이면 안전"), Min(0.5f)] public float Radius { get; private set; } = 2f;
		[field: SerializeField, Tooltip("원이 남는 초. 예고 뒤부터"), Min(0.5f)] public float Seconds { get; private set; } = 6f;
		[field: SerializeField, Tooltip("시작 자리 기준 꺾인 점 (m). x 는 오른쪽, y 는 앞. 비우면 제자리")] public List<Vector2> Path { get; private set; } = new();
		[field: SerializeField, Tooltip("원이 길을 가는 빠르기 (m/s). 끝점에 닿으면 멈춤"), Min(0.1f)] public float Speed { get; private set; } = 1.5f;
		[field: SerializeField, Tooltip("원 밖 피해 간격 (초). 첫 피해도 원이 생기고 이만큼 뒤"), Min(0.2f)] public float OutsideInterval { get; private set; } = 0.8f;
		[field: SerializeField, Tooltip("원 밖 기본 피해"), Min(1)] public int OutsideDamage { get; private set; } = 2;
		[field: SerializeField, Tooltip("원 밖에서 맞은 순간 거는 것")] public MonsterHitEffect HitEffect { get; private set; } = new();
		[field: SerializeField, Tooltip("원이 있는 동안 원을 그리는 바닥 부품. Follow 로 움직이는 원을 따라감")] public CombatFx CircleFx { get; private set; }

		protected override bool TargetsArePositions => true;

		// 시작 자리 하나. 길의 앞쪽은 Execute 때 다시 셈 (시전자가 예고 중 밀려도 시작 자리는 고정)
		public override void ResolveTargets(UnitObject unitObject, Vector3 playerPosition, Vector3 moveVelocity, List<Vector3> results)
		{
			Vector3 start = Start == SafeCircleStart.Target ? playerPosition : unitObject.transform.position;
			start.y = playerPosition.y;
			results.Add(start);
		}

		public override CombatFxContext WindupContext(UnitObject unitObject, Vector3 target, int index) => new()
		{
			Position = target,
			Radius = Radius,
			Seconds = WindupSeconds,
		};

		// 원을 맡김. 원은 맞히는 공격이 아니라 같은 순간의 다른 자리를 막지 않음
		public override bool Execute(UnitObject unitObject, PlayerObject player, Vector3 target, int index)
		{
			if (player.IsAlive == false || unitObject.IsAlive == false)
				return false;
			Vector3 forward = ScatterZoneMonsterAttack.FlatDirection(unitObject.transform.position, player.transform.position);
			SafeCircleField.Instance.Add(unitObject, this, target, forward);
			return false;
		}

		// 흐른 초에 원 중심. 길을 Speed 로 따라가다 끝점에서 멈춤. forward 는 길의 앞 (+y)
		public static Vector3 PositionAt(Vector3 start, Vector3 forward, IReadOnlyList<Vector2> path, float speed, float elapsed)
		{
			forward.y = 0f;
			forward = forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;
			Vector3 right = new(forward.z, 0f, -forward.x);
			float remaining = Mathf.Max(0f, elapsed) * speed;
			Vector3 from = start;
			foreach (Vector2 point in path)
			{
				Vector3 to = start + right * point.x + forward * point.y;
				float length = Vector3.Distance(from, to);
				if (remaining <= length)
					return length < 0.0001f ? to : Vector3.Lerp(from, to, remaining / length);
				remaining -= length;
				from = to;
			}
			return from;
		}

		// 중심이 원 안이면 안전 (몸이 걸쳐도 안전). 높이는 무시
		public static bool IsInside(Vector3 center, float radius, Vector3 point)
		{
			Vector3 offset = point - center;
			offset.y = 0f;
			return offset.sqrMagnitude <= radius * radius;
		}

		public override void Validate(Monster owner, List<string> errors)
		{
			base.Validate(owner, errors);
			if (WindupSeconds <= 0f)
				errors.Add($"{name}: 안전 원은 나올 자리를 예고해야 함 ({nameof(WindupSeconds)} 0)");
			if (CircleFx == null)
				errors.Add($"{name}: 원을 그릴 {nameof(CircleFx)} 이 비어 있음");
			if (Linger.Seconds > 0f)
				errors.Add($"{name}: 안전 원은 남는 물체를 쓰지 않음");
			if (HitEffect.HitFx == null)
				errors.Add($"{name}: 원 밖에서 맞은 순간이 안 보임 ({nameof(HitEffect)}.{nameof(HitEffect.HitFx)} 비어 있음)");
			HitEffect.Validate(name, errors);
		}
	}
}

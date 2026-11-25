using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 잇기. 이 유닛이 남긴 물체 (Linger) 를 둘레 차례로 이은 띠에 피해. 남은 것이 둘보다 적으면 안 함
	// 고정하는 것 (target) 은 띠의 시작 물체 자리. 끝은 그 다음 물체라 물체가 사라지면 그 띠는 안 때림
	[CreateAssetMenu(fileName = "MA_Link", menuName = "WM/Variable/MonsterAttack/" + nameof(LinkMonsterAttack))]
	public class LinkMonsterAttack : WindupMonsterAttack
	{
		// 같은 물체로 보는 거리 제곱 (m)
		private const float SAME_SPOT_SQUARED = 0.01f;

		private static readonly List<Vector3> posts = new();

		public override string KindName => "잇기";

		[field: Header("잇기")]
		[field: SerializeField, Tooltip("띠 폭 (m)"), Min(0.2f)] public float Width { get; private set; } = 1f;
		[field: SerializeField, Tooltip("이보다 먼 물체끼리는 안 이음 (m)"), Min(1f)] public float MaxLength { get; private set; } = 9f;
		[field: SerializeField, Tooltip("기본 피해"), Min(1)] public int Damage { get; private set; } = 4;
		[field: SerializeField, Tooltip("피해가 실제로 들어간 순간 주인공에게 거는 것")] public MonsterHitEffect HitEffect { get; private set; } = new();

		public override bool IsReady(UnitObject unitObject) => Collect(unitObject).Count >= 2;

		public override void ResolveTargets(UnitObject unitObject, Vector3 playerPosition, Vector3 moveVelocity, List<Vector3> results)
		{
			List<Vector3> ordered = Collect(unitObject);
			// 둘이면 띠 하나
			int count = ordered.Count == 2 ? 1 : ordered.Count;
			for (int i = 0; i < count; i++)
			{
				if (Vector3.Distance(ordered[i], ordered[(i + 1) % ordered.Count]) <= MaxLength)
					results.Add(ordered[i]);
			}
		}

		public override CombatFxContext WindupContext(UnitObject unitObject, Vector3 target, int index)
		{
			bool hasEnd = TryEnd(unitObject, target, out Vector3 end);
			return new CombatFxContext
			{
				Position = target,
				Direction = hasEnd ? ScatterZoneMonsterAttack.FlatDirection(target, end) : Vector3.forward,
				Length = hasEnd ? Vector3.Distance(target, end) : 0f,
				Width = Width,
				Seconds = WindupSeconds,
			};
		}

		public override bool Execute(UnitObject unitObject, PlayerObject player, Vector3 target, int index)
		{
			if (player.IsAlive == false || TryEnd(unitObject, target, out Vector3 end) == false)
				return false;
			Vector3 direction = ScatterZoneMonsterAttack.FlatDirection(target, end);
			if (LineMonsterAttack.IsInStrip(target, direction, Vector3.Distance(target, end), Width, player.transform.position, player.UnitMovement.BodyRadius) == false)
				return false;
			HitEffect.Strike(unitObject, unitObject.transform.position, player, MakeDamage(unitObject, Damage));
			return true;
		}

		private static bool TryEnd(UnitObject unitObject, Vector3 start, out Vector3 end)
		{
			List<Vector3> ordered = Collect(unitObject);
			for (int i = 0; i < ordered.Count && ordered.Count >= 2; i++)
			{
				if ((ordered[i] - start).sqrMagnitude > SAME_SPOT_SQUARED)
					continue;
				end = ordered[(i + 1) % ordered.Count];
				return true;
			}
			end = start;
			return false;
		}

		private static List<Vector3> Collect(UnitObject unitObject)
		{
			LingerField.Instance.CollectHot(unitObject, posts);
			SortAround(posts);
			return posts;
		}

		// 가운데에서 본 각도 차례. 이은 띠가 서로 안 엇갈리게
		public static void SortAround(List<Vector3> points)
		{
			if (points.Count < 3)
				return;
			Vector3 center = Vector3.zero;
			foreach (Vector3 point in points)
				center += point / points.Count;
			points.Sort((left, right) => Mathf.Atan2(left.x - center.x, left.z - center.z).CompareTo(Mathf.Atan2(right.x - center.x, right.z - center.z)));
		}

		public override void Validate(Monster owner, List<string> errors)
		{
			base.Validate(owner, errors);
			if (WindupSeconds <= 0f)
				errors.Add($"{name}: 잇기는 예고가 있어야 피할 수 있음 ({nameof(WindupSeconds)} 0)");
			HitEffect.Validate(name, errors);
		}
	}
}

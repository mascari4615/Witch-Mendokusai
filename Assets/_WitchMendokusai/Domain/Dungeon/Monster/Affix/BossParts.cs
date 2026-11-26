using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 보스와 부위 몸의 짝. 부위는 별도 몬스터 (체력 따로). 세우는 쪽은 PartsAffix, 부위 쪽 일은 PartBodyAffix, 꺾인 부위의 패턴을 끄는 쪽은 PartGatedMonsterAttack
	// 몸은 풀에서 다시 쓰이므로 물을 때마다 살아 있고 켜져 있고 같은 종인지 봄. 정본 memo wm/design/boss-arena-20261126
	public static class BossParts
	{
		private sealed class Link
		{
			public MonsterObject Boss;
			public Monster Kind;
			public float DamageShare;
		}

		private static readonly Dictionary<MonsterObject, Link> links = new();
		private static readonly List<MonsterObject> stale = new();

		public static void Register(MonsterObject boss, MonsterObject part, float damageShare)
		{
			Prune();
			links[part] = new Link { Boss = boss, Kind = part.UnitData, DamageShare = damageShare };
		}

		public static void Forget(MonsterObject part) => links.Remove(part);

		// 살아 있는 보스 부위인가 (화면 밖 정리에서 뺌)
		public static bool IsPart(MonsterObject monster) => links.TryGetValue(monster, out Link link) && IsLive(monster, link);

		// 이 보스의 kind 부위가 살아 있나
		public static bool HasLivingPart(UnitObject boss, Monster kind)
		{
			foreach (KeyValuePair<MonsterObject, Link> pair in links)
			{
				if (pair.Value.Boss == boss && pair.Value.Kind == kind && IsLive(pair.Key, pair.Value))
					return true;
			}
			return false;
		}

		// 부위의 주인 보스. 보스가 쓰러졌거나 꺼졌으면 false
		public static bool TryGetBoss(MonsterObject part, out MonsterObject boss, out float damageShare)
		{
			boss = null;
			damageShare = 0f;
			if (links.TryGetValue(part, out Link link) == false || IsLive(part, link) == false)
				return false;
			if (link.Boss == null || link.Boss.isActiveAndEnabled == false || link.Boss.IsAlive == false)
				return false;
			boss = link.Boss;
			damageShare = link.DamageShare;
			return true;
		}

		// 이 보스의 살아 있는 부위들
		public static void CollectParts(UnitObject boss, List<MonsterObject> results)
		{
			results.Clear();
			foreach (KeyValuePair<MonsterObject, Link> pair in links)
			{
				if (pair.Value.Boss == boss && IsLive(pair.Key, pair.Value))
					results.Add(pair.Key);
			}
		}

		private static bool IsLive(MonsterObject part, Link link) =>
			part != null && part.isActiveAndEnabled && part.IsAlive && part.UnitData == link.Kind;

		// 풀로 돌아가 다른 몸이 된 것과 파괴된 것을 지움
		private static void Prune()
		{
			stale.Clear();
			foreach (KeyValuePair<MonsterObject, Link> pair in links)
			{
				if (IsLive(pair.Key, pair.Value) == false)
					stale.Add(pair.Key);
			}
			foreach (MonsterObject part in stale)
				links.Remove(part);
		}

		// 보스 둘레 자리. offset 의 x 는 오른쪽, y 는 앞 (+z). 보스는 방향이 없어 월드 축
		public static Vector3 SlotPosition(Vector3 bossPosition, Vector2 offset) => bossPosition + new Vector3(offset.x, 0f, offset.y);

		// 부위가 맞은 피해 가운데 보스로 넘기는 몫. 0 이면 안 넘김
		public static int SharedDamage(int damage, float share) => share <= 0f ? 0 : Mathf.Max(1, Mathf.RoundToInt(damage * share));
	}
}

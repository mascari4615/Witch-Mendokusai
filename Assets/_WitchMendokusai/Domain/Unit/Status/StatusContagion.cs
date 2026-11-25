using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 처치 시 상태 전염. 상태 틱으로 죽은 몬스터가 가까운 몬스터에게 같은 상태를 퍼뜨림
	// - 규칙과 상한은 StatusContagionRules 와 UnitStatusConfig. 여기는 몬스터 모으기와 적용만
	// - 죽은 유닛은 곧 꺼지며 상태가 지워지므로 죽기 전에 Begin 으로 세대를 받아 둠
	public static class StatusContagion
	{
		public readonly struct Origin
		{
			public readonly UnitObject Unit;
			public readonly UnitStatusType Type;
			public readonly Vector3 Position;
			public readonly int Generation;
			public readonly IDamageSource Source;

			public Origin(UnitObject unit, UnitStatusType type, Vector3 position, int generation, IDamageSource source)
			{
				Unit = unit;
				Type = type;
				Position = position;
				Generation = generation;
				Source = source;
			}
		}

		private static readonly List<MonsterObject> candidates = new();
		private static readonly List<float> squaredDistances = new();
		private static readonly List<int> receivedCounts = new();
		private static readonly List<int> picked = new();

		// 이 틱이 전염 대상 상태이고 세대가 더 퍼질 수 있으면 true
		public static bool Begin(UnitObject unit, UnitStatusType type, IDamageSource source, out Origin origin)
		{
			origin = default;
			UnitStatusConfig config = ResourceManager.Instance.UnitStatus;
			if (config == null || unit is not MonsterObject || config.ContagionStatuses.Contains(type) == false)
				return false;
			if (UnitStatus.Has(unit, type) == false)
				return false;
			int generation = UnitStatus.Generation(unit, type);
			if (StatusContagionRules.CanSpread(generation, config.ContagionMaxGenerations) == false)
				return false;
			origin = new Origin(unit, type, unit.transform.position, generation, source);
			return true;
		}

		public static void Spread(Origin origin)
		{
			UnitStatusConfig config = ResourceManager.Instance.UnitStatus;
			candidates.Clear();
			squaredDistances.Clear();
			receivedCounts.Clear();
			float radiusSquared = config.ContagionRadius * config.ContagionRadius;
			foreach (GameObject monsterObject in ObjectBufferManager.GetObjects(ObjectType.Monster))
			{
				if (monsterObject == null || monsterObject.activeInHierarchy == false || MonsterObject.IsHiddenMonster(monsterObject))
					continue;
				if (monsterObject.TryGetComponent(out MonsterObject monster) == false || monster == origin.Unit || monster.IsAlive == false)
					continue;
				float distanceSquared = (monster.transform.position - origin.Position).sqrMagnitude;
				if (distanceSquared > radiusSquared)
					continue;
				candidates.Add(monster);
				squaredDistances.Add(distanceSquared);
				receivedCounts.Add(UnitStatus.ContagionReceiveCount(monster, origin.Type));
			}
			StatusContagionRules.SelectTargets(squaredDistances, receivedCounts, config.ContagionRadius, config.ContagionMaxTargets, config.ContagionMaxReceives, picked);
			int nextGeneration = StatusContagionRules.NextGeneration(origin.Generation);
			foreach (int index in picked)
				UnitStatus.ApplyContagion(candidates[index], origin.Type, origin.Source, nextGeneration);
			candidates.Clear();
			squaredDistances.Clear();
			receivedCounts.Clear();
			picked.Clear();
		}
	}
}

using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 소환. 예고 뒤 시전자 둘레에 몬스터 몇 마리. 근처에 같은 종이 상한만큼 살아 있으면 그만큼 덜 부름
	// 상한은 시전자가 부른 수가 아니라 둘레의 같은 종 수. 공격 자산을 여러 유닛이 나눠 써서 시전자별 기록을 두지 않음
	[CreateAssetMenu(fileName = "MA_Summon", menuName = "WM/Variable/MonsterAttack/" + nameof(SummonMonsterAttack))]
	public class SummonMonsterAttack : WindupMonsterAttack
	{
		[field: Header("소환")]
		[field: SerializeField] public Monster Summoned { get; private set; }
		[field: SerializeField, Min(1)] public int Count { get; private set; } = 3;
		[field: SerializeField, Tooltip("시전자에게서 거리 (m)"), Min(0.5f)] public float Distance { get; private set; } = 2.6f;
		[field: SerializeField, Tooltip("둘레에 살아 있는 같은 종 상한"), Min(1)] public int MaxAlive { get; private set; } = 6;
		[field: SerializeField, Tooltip("상한을 세는 거리 (m)"), Min(1f)] public float CountRadius { get; private set; } = 14f;
		[field: SerializeField, Tooltip("예고 원 반지름 (m)"), Min(0.1f)] public float TelegraphRadius { get; private set; } = 0.8f;

		public override void ResolveTargets(UnitObject unitObject, Vector3 playerPosition, Vector3 moveVelocity, List<Vector3> results) =>
			ScatterZoneMonsterAttack.Arrange(ScatterArrangement.Ring, unitObject.transform.position, playerPosition, Count, Distance, 0f, Random.value * 360f, () => 0f, results);

		public override CombatFxContext WindupContext(UnitObject unitObject, Vector3 target) => new()
		{
			Position = target,
			Radius = TelegraphRadius,
			Seconds = WindupSeconds,
		};

		public override void Execute(UnitObject unitObject, PlayerObject player, List<Vector3> targets)
		{
			int allowed = AllowedCount(CountAlive(unitObject.transform.position), targets.Count, MaxAlive);
			for (int i = 0; i < allowed; i++)
				Spawn(targets[i]);
		}

		public static int AllowedCount(int alive, int requested, int maxAlive) => Mathf.Clamp(maxAlive - alive, 0, requested);

		private int CountAlive(Vector3 center)
		{
			int count = 0;
			foreach (GameObject candidate in ObjectBufferManager.GetObjects(ObjectType.Monster))
			{
				if (candidate == null || candidate.activeInHierarchy == false || SlimeKingPatternMath.IsWithin(center, candidate.transform.position, CountRadius) == false)
					continue;
				if (candidate.TryGetComponent(out MonsterObject monster) && monster.UnitData.ID == Summoned.ID && monster.IsAlive)
					count++;
			}
			return count;
		}

		private void Spawn(Vector3 position)
		{
			GameObject spawnedObject = ObjectPoolManager.Instance.Spawn(Summoned.Prefab);
			spawnedObject.transform.position = position;
			spawnedObject.GetComponent<MonsterObject>().Init(Summoned);
			spawnedObject.SetActive(true);
		}

		public override void Validate(Monster owner, List<string> errors)
		{
			base.Validate(owner, errors);
			if (Summoned == null)
				errors.Add($"{name}: 부를 몬스터가 비어 있음");
			else if (Summoned == owner)
				errors.Add($"{name}: 자기 자신을 부름 (끝없이 불어남)");
			else if (Summoned.Type == MonsterType.Boss)
				errors.Add($"{name}: 보스를 부를 수 없음");
		}
	}
}

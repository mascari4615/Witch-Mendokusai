using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TargetingSystem(전술 타겟 선정) 결정성·필터 회귀. ICombatant 스텁으로 UnitObject 없이 검증.
	/// WM-165 item 2 — 퀄리티 동기 first-use(결정적 타겟 선택).
	/// </summary>
	public class TargetingSystemTests
	{
		private sealed class FakeCombatant : ICombatant
		{
			public int CombatantId { get; set; }
			public int TeamId { get; set; }
			public bool IsAlive { get; set; } = true;
			public Vector3 Position { get; set; }
			public int Hp { get; set; } = 100;
			public int HpMax { get; set; } = 100;
		}

		private static FakeCombatant Make(int id, int team, Vector3 pos, int hp = 100, bool alive = true)
		{
			return new FakeCombatant { CombatantId = id, TeamId = team, Position = pos, Hp = hp, HpMax = 100, IsAlive = alive };
		}

		[Test]
		public void Query_EnemyNearest_PicksClosestEnemy()
		{
			TargetingSystem targeting = new();
			FakeCombatant self = Make(id: 0, team: 0, pos: Vector3.zero);
			FakeCombatant near = Make(id: 1, team: 1, pos: new Vector3(2f, 0f, 0f));
			FakeCombatant far = Make(id: 2, team: 1, pos: new Vector3(8f, 0f, 0f));
			targeting.Register(self);
			targeting.Register(near);
			targeting.Register(far);

			ICombatant result = targeting.Query(self, new TargetQuery(TargetSide.Enemy, TargetPriority.Nearest));

			Assert.AreSame(near, result);
		}

		[Test]
		public void Query_Enemy_ExcludesAlliesAndSelf()
		{
			TargetingSystem targeting = new();
			FakeCombatant self = Make(id: 0, team: 0, pos: Vector3.zero);
			FakeCombatant ally = Make(id: 1, team: 0, pos: new Vector3(1f, 0f, 0f));
			FakeCombatant enemy = Make(id: 2, team: 1, pos: new Vector3(5f, 0f, 0f));
			targeting.Register(self);
			targeting.Register(ally);
			targeting.Register(enemy);

			ICombatant result = targeting.Query(self, new TargetQuery(TargetSide.Enemy, TargetPriority.Nearest));

			Assert.AreSame(enemy, result);
		}

		[Test]
		public void Query_LowestHp_PicksLowestHpEnemy()
		{
			TargetingSystem targeting = new();
			FakeCombatant self = Make(id: 0, team: 0, pos: Vector3.zero);
			FakeCombatant healthy = Make(id: 1, team: 1, pos: new Vector3(1f, 0f, 0f), hp: 90);
			FakeCombatant wounded = Make(id: 2, team: 1, pos: new Vector3(9f, 0f, 0f), hp: 10);
			targeting.Register(self);
			targeting.Register(healthy);
			targeting.Register(wounded);

			ICombatant result = targeting.Query(self, new TargetQuery(TargetSide.Enemy, TargetPriority.LowestHp));

			Assert.AreSame(wounded, result);
		}

		[Test]
		public void Query_Tiebreak_PrefersLowerCombatantId()
		{
			// 두 적이 같은 거리(점수 동률) → 결정적으로 CombatantId 작은 쪽 선택(리플레이 안정).
			TargetingSystem targeting = new();
			FakeCombatant self = Make(id: 0, team: 0, pos: Vector3.zero);
			FakeCombatant high = Make(id: 5, team: 1, pos: new Vector3(3f, 0f, 0f));
			FakeCombatant low = Make(id: 2, team: 1, pos: new Vector3(0f, 3f, 0f)); // 같은 거리 3
			targeting.Register(self);
			targeting.Register(high);
			targeting.Register(low);

			ICombatant result = targeting.Query(self, new TargetQuery(TargetSide.Enemy, TargetPriority.Nearest));

			Assert.AreSame(low, result, "동률 시 CombatantId 작은 쪽(결정성)");
		}

		[Test]
		public void Query_ExcludesDead()
		{
			TargetingSystem targeting = new();
			FakeCombatant self = Make(id: 0, team: 0, pos: Vector3.zero);
			FakeCombatant deadNear = Make(id: 1, team: 1, pos: new Vector3(1f, 0f, 0f), alive: false);
			FakeCombatant aliveFar = Make(id: 2, team: 1, pos: new Vector3(7f, 0f, 0f));
			targeting.Register(self);
			targeting.Register(deadNear);
			targeting.Register(aliveFar);

			ICombatant result = targeting.Query(self, new TargetQuery(TargetSide.Enemy, TargetPriority.Nearest));

			Assert.AreSame(aliveFar, result);
		}

		[Test]
		public void Query_MaxRange_ExcludesOutOfRange()
		{
			TargetingSystem targeting = new();
			FakeCombatant self = Make(id: 0, team: 0, pos: Vector3.zero);
			FakeCombatant outOfRange = Make(id: 1, team: 1, pos: new Vector3(10f, 0f, 0f));
			targeting.Register(self);
			targeting.Register(outOfRange);

			ICombatant result = targeting.Query(self, new TargetQuery(TargetSide.Enemy, TargetPriority.Nearest, maxRange: 5f));

			Assert.IsNull(result);
		}

		[Test]
		public void Query_NoCandidates_ReturnsNull()
		{
			TargetingSystem targeting = new();
			FakeCombatant self = Make(id: 0, team: 0, pos: Vector3.zero);
			targeting.Register(self);

			ICombatant result = targeting.Query(self, new TargetQuery(TargetSide.Enemy, TargetPriority.Nearest));

			Assert.IsNull(result);
		}

		// --- TargetSide.EnemyObjective (TASK-WM-194) — "앞을 막는 아무 유닛" 이 아니라 "적 기지" 를 향하는 질의 ---

		// 핵심: 더 가까운 일반 적(타워)이 있어도 목표물(코어)을 고른다 = 웨이브가 방어선을 뚫고 기지로 전진.
		[Test]
		public void Query_EnemyObjective_PicksObjective_EvenIfOtherEnemyIsCloser()
		{
			TargetingSystem targeting = new();
			FakeCombatant self = Make(id: 0, team: 1, pos: Vector3.zero);
			FakeCombatant tower = Make(id: 1, team: 0, pos: new Vector3(2f, 0f, 0f));
			FakeCombatant core = Make(id: 2, team: 0, pos: new Vector3(20f, 0f, 0f));
			targeting.Register(self);
			targeting.Register(tower);
			targeting.Register(core);
			targeting.RegisterObjective(core);

			ICombatant result = targeting.Query(self, new TargetQuery(TargetSide.EnemyObjective, TargetPriority.Nearest));

			Assert.AreSame(core, result);
		}

		// 목표물 미등록이면 후보 0 → null (전진 룰이 fallthrough 하도록).
		[Test]
		public void Query_EnemyObjective_NoObjectiveRegistered_ReturnsNull()
		{
			TargetingSystem targeting = new();
			FakeCombatant self = Make(id: 0, team: 1, pos: Vector3.zero);
			FakeCombatant tower = Make(id: 1, team: 0, pos: new Vector3(2f, 0f, 0f));
			targeting.Register(self);
			targeting.Register(tower);

			ICombatant result = targeting.Query(self, new TargetQuery(TargetSide.EnemyObjective, TargetPriority.Nearest));

			Assert.IsNull(result);
		}

		// 아군 목표물(자기 팀 코어)은 안 잡힘 = 적 진영 필터 유지.
		[Test]
		public void Query_EnemyObjective_ExcludesOwnTeamObjective()
		{
			TargetingSystem targeting = new();
			FakeCombatant self = Make(id: 0, team: 0, pos: Vector3.zero);
			FakeCombatant ownCore = Make(id: 1, team: 0, pos: new Vector3(3f, 0f, 0f));
			targeting.Register(self);
			targeting.Register(ownCore);
			targeting.RegisterObjective(ownCore);

			ICombatant result = targeting.Query(self, new TargetQuery(TargetSide.EnemyObjective, TargetPriority.Nearest));

			Assert.IsNull(result);
		}

		// 코어가 파괴되면 목표물 질의도 null — 죽은 목표를 계속 때리러 가지 않는다.
		[Test]
		public void Query_EnemyObjective_DeadObjective_ReturnsNull()
		{
			TargetingSystem targeting = new();
			FakeCombatant self = Make(id: 0, team: 1, pos: Vector3.zero);
			FakeCombatant core = Make(id: 1, team: 0, pos: new Vector3(5f, 0f, 0f), alive: false);
			targeting.Register(self);
			targeting.Register(core);
			targeting.RegisterObjective(core);

			ICombatant result = targeting.Query(self, new TargetQuery(TargetSide.EnemyObjective, TargetPriority.Nearest));

			Assert.IsNull(result);
		}

		// Unregister 는 목표물 표시도 함께 해제 — 풀 반환 유닛이 좀비 목표로 남지 않음.
		[Test]
		public void Unregister_AlsoClearsObjectiveMark()
		{
			TargetingSystem targeting = new();
			FakeCombatant self = Make(id: 0, team: 1, pos: Vector3.zero);
			FakeCombatant core = Make(id: 1, team: 0, pos: new Vector3(5f, 0f, 0f));
			targeting.Register(self);
			targeting.Register(core);
			targeting.RegisterObjective(core);
			targeting.Unregister(core);

			targeting.Register(core); // 목표물 표시 없이 일반 참가자로만 재등록.
			ICombatant result = targeting.Query(self, new TargetQuery(TargetSide.EnemyObjective, TargetPriority.Nearest));

			Assert.IsNull(result);
		}
	}
}

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 3v3 을 <b>끝날 때까지</b> 돌린다 — 전술 파이프라인 + 진짜 TargetingSystem + ArenaMatchCore + 승리 모드를
	/// 한 루프에 물려서. 씬·물리·MonoBehaviour 0.
	///
	/// ★ 무엇을 잡으려는 시험인가: 이 레포에서 되풀이된 실패는 「매치가 안 끝난다」다.
	///   `SpawnRules` 주석은 겹쳐 스폰된 유닛이 맵 밖으로 튕겨 <b>죽지도 않고 남아</b> 판이 안 끝난 걸 적어뒀고,
	///   `ITacticActuator.Approach` 주석은 마수가 코어에 파묻혀 「다 잡았는데 안 넘어간다」로 보인 걸 적어뒀다.
	///   둘 다 **화면에선 원인이 안 보이는** 부류다. 그 종결 조건을 EditMode 로 내린다.
	///
	/// <see cref="ArenaDryMatchTests"/> 가 <b>한 틱</b>의 판단을 본다면, 여기는 <b>여러 틱의 수렴</b>을 본다 —
	/// 전진 → 사거리 진입 → 시전 → 사망 → 전멸 → 종결까지 한 줄로 이어지는지.
	/// </summary>
	public class ArenaFullMatchSimTests
	{
		private sealed class SimCombatant : ICombatant
		{
			public int CombatantId { get; set; }
			public int TeamId { get; set; }
			public bool IsAlive => Hp > 0;
			public Vector3 Position { get; set; }
			public int Hp { get; set; } = 100;
			public int HpMax { get; set; } = 100;
			public override string ToString() => $"팀{TeamId}#{CombatantId}(hp{Hp})";
		}

		/// <summary> 행동을 실제 상태 변화로 옮기는 최소 시뮬레이터 — 이동은 한 걸음, 시전은 고정 피해. </summary>
		private sealed class SimActuator : ITacticActuator
		{
			private readonly SimCombatant self;
			public SimActuator(SimCombatant self) { this.self = self; }

			public bool StopsToAttack => true;
			public int ActionsTaken;

			public void UseSkill(int skillSlot, ICombatant target)
			{
				ActionsTaken++;
				if (target is SimCombatant victim)
					victim.Hp = Mathf.Max(0, victim.Hp - SKILL_DAMAGE);
			}

			public void MoveToward(ICombatant target)
			{
				ActionsTaken++;
				self.Position = Vector3.MoveTowards(self.Position, target.Position, STEP_DISTANCE);
			}

			public void Approach(ICombatant target, float stopDistance)
			{
				ActionsTaken++;
				self.Position = Vector3.MoveTowards(self.Position, target.Position, STEP_DISTANCE);
			}

			public void Retreat(ICombatant target) { ActionsTaken++; }
			public void Hold() { ActionsTaken++; }
		}

		// 출하 프리셋과 같은 수치(ArenaMatchConfig_Dolls) — 여기가 어긋나면 다른 게임을 검사하게 된다.
		private const float PRESET_ATTACK_RANGE = 7f;
		private const int PRESET_SKILL_SLOT = 2;
		private const int SKILL_DAMAGE = 34;   // 100 HP → 3 대에 죽는다(틱 수를 사람이 셀 수 있게)
		private const float STEP_DISTANCE = 2f;
		private const float TICK_SECONDS = 0.1f;
		// 넉넉히 크되 **무한은 아니다** — 안 끝나는 판을 시험이 영원히 기다리면 그것도 「안 끝나는」 것이다.
		private const int MAX_TICKS = 500;

		private static TacticProgram PresetTactic()
		{
			TacticProgram program = new();
			program.Rules.Add(new TacticRule
			{
				Conditions = new List<TacticCondition> { new() { Kind = ConditionKind.EnemyInRange } },
				Target = new TargetQuery(TargetSide.Enemy, TargetPriority.Nearest, PRESET_ATTACK_RANGE),
				Action = new TacticAction { Kind = ActionKind.UseSkill, SkillSlot = PRESET_SKILL_SLOT },
			});
			program.Rules.Add(new TacticRule
			{
				Conditions = new List<TacticCondition>(),
				Target = new TargetQuery(TargetSide.Enemy, TargetPriority.Nearest, 0f),
				Action = new TacticAction { Kind = ActionKind.MoveToTarget },
			});
			return program;
		}

		private sealed class SimResult
		{
			public bool Concluded;
			public int Ticks;
			public int WinnerTeamId;
			public bool ByElimination;
			public bool ByTimeout;
			public List<SimCombatant> Roster;
		}

		private static SimResult RunMatch(List<SimCombatant> roster, float timeLimitSeconds)
		{
			TargetingSystem targeting = new();
			Dictionary<SimCombatant, TacticBTRunner> runners = new();
			Dictionary<int, List<ICombatant>> byTeam = new();

			foreach (SimCombatant combatant in roster)
			{
				targeting.Register(combatant);
				if (byTeam.ContainsKey(combatant.TeamId) == false)
					byTeam[combatant.TeamId] = new List<ICombatant>();
				byTeam[combatant.TeamId].Add(combatant);
			}

			foreach (SimCombatant combatant in roster)
			{
				TacticContext context = new(combatant, targeting, new SimActuator(combatant), _ => true);
				runners[combatant] = new TacticBTRunner(context, PresetTactic());
			}

			List<MatchTeam> teams = new();
			foreach (KeyValuePair<int, List<ICombatant>> pair in byTeam)
				teams.Add(new MatchTeam(pair.Key, pair.Value));

			BrawlArenaMode mode = ScriptableObject.CreateInstance<BrawlArenaMode>();
			ArenaMatchCore core = new(teams, mode, timeLimitSeconds);

			SimResult result = new() { Roster = roster };
			for (int tick = 0; tick < MAX_TICKS; tick++)
			{
				foreach (SimCombatant combatant in roster)
				{
					// 죽은 유닛은 안 움직인다 — 실제 셸도 드라이버를 멈춘다(좀비 틱 방지).
					if (combatant.IsAlive)
						runners[combatant].UpdateBT();
				}

				if (core.Poll(TICK_SECONDS))
				{
					result.Concluded = true;
					result.Ticks = tick + 1;
					result.WinnerTeamId = core.WinnerTeamId;
					result.ByElimination = core.ConcludedByElimination;
					result.ByTimeout = core.ConcludedByTimeout;
					break;
				}
			}
			return result;
		}

		private static List<SimCombatant> Roster(params (int team, float x, int hp)[] units)
		{
			List<SimCombatant> roster = new();
			for (int i = 0; i < units.Length; i++)
			{
				roster.Add(new SimCombatant
				{
					CombatantId = i,
					TeamId = units[i].team,
					Position = new Vector3(units[i].x, 0f, 0f),
					Hp = units[i].hp,
					HpMax = 100,
				});
			}
			return roster;
		}

		// 대칭 3v3 은 서로 죽여 **상호 전멸**로 끝난다(같은 틱에 같은 피해를 주고받으므로).
		// 중요한 건 승패가 아니라 **끝난다는 것** — 안 끝나는 게 이 레포의 반복 실패다.
		[Test]
		public void 대칭_3v3_은_반드시_끝난다()
		{
			SimResult result = RunMatch(Roster((0, -20f, 100), (0, -22f, 100), (0, -24f, 100),
												(1, 20f, 100), (1, 22f, 100), (1, 24f, 100)), 0f);

			Assert.IsTrue(result.Concluded, $"{MAX_TICKS} 틱 안에 안 끝났다 — 「다 잡았는데 안 넘어간다」의 재현이다");
			Assert.IsTrue(result.ByElimination, "전멸로 끝나야 한다(제한시간 0 = 무제한)");
		}

		// 한쪽이 압도적이면 그쪽이 이기고, **진 팀은 전원 죽어 있어야** 한다.
		// 「이겼다는데 적이 살아있다」면 승리 판정과 실제 상태가 어긋난 것이다.
		[Test]
		public void 우세한_팀이_이기고_진_팀은_전원_죽는다()
		{
			SimResult result = RunMatch(Roster((0, -10f, 100), (0, -12f, 100), (0, -14f, 100),
												(1, 10f, 34)), 0f);

			Assert.IsTrue(result.Concluded, "안 끝났다");
			Assert.AreEqual(0, result.WinnerTeamId, "3기 팀이 이겨야 한다");
			foreach (SimCombatant combatant in result.Roster)
			{
				if (combatant.TeamId == 1)
					Assert.IsFalse(combatant.IsAlive, combatant + " 가 살아있는데 상대가 이겼다");
			}
		}

		// 죽은 유닛이 승리 판정에서 안 빠지면 판이 영영 안 끝난다 — 그 자리를 직접 짚는다.
		[Test]
		public void 죽은_유닛은_생존_판정에서_빠진다()
		{
			SimResult result = RunMatch(Roster((0, -5f, 100), (1, 5f, 1)), 0f);

			Assert.IsTrue(result.Concluded, "1 HP 짜리가 죽었는데도 안 끝났다면 생존 판정이 시체를 세고 있다");
			Assert.AreEqual(0, result.WinnerTeamId);
			Assert.Less(result.Ticks, 60, "한 대면 죽는 상대인데 종결이 너무 늦다");
		}

		// 서로 못 죽이는 짝(피해 0이 되도록 HP 를 크게) → 제한시간이 유일한 출구.
		// 제한시간이 없으면 이런 판은 영원히 돈다 — 그래서 timeLimit 은 안전장치다.
		[Test]
		public void 결판이_안_나는_판은_제한시간이_끝낸다()
		{
			// 양쪽 HP 를 아주 크게 = MAX_TICKS 안에 못 죽인다. 제한시간 2초(= 20틱)가 먼저 온다.
			SimResult result = RunMatch(Roster((0, -3f, 100000), (1, 3f, 100000)), 2f);

			Assert.IsTrue(result.Concluded, "제한시간이 있는데 안 끝났다");
			Assert.IsTrue(result.ByTimeout, "제한시간으로 끝나야 한다");
			Assert.AreEqual(ArenaModeSO.NO_WINNER, result.WinnerTeamId, "생존 수 동률 → 무승부");
		}
	}
}

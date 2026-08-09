using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
// ★ 좌표는 판정 쪽 (TASK-WM-214) — 시험이 구현하는 ICombatant 가 판정 타입을 쓴다.
using Vector3 = WitchMendokusai.Numerics.Vector3;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 「마른 매치」 — 씬·물리·MonoBehaviour 없이 <b>진짜 TargetingSystem</b> 을 진짜 전술 파이프라인에 물려
	/// 3v3 을 한 틱 돌린다.
	///
	/// ★ 왜 필요한가 (2026-08-06): 이음매 하나가 어느 시험에도 안 걸려 있었다.
	///   - `TargetingSystemTests` = TargetingSystem <b>단독</b>
	///   - `TacticEngineTests`    = 전술 파이프라인 + <b>FakeResolver</b>
	///   둘 다 초록인데 **그 둘이 만나는 자리**는 아무도 안 봤다. TargetQuery 의 의미
	///   (MaxRange = 탐색 반경 / Side 필터 / 동률 타이브레이크)를 전술 룰이 실제로 어떻게 쓰는지가
	///   그 자리에서 정해진다. 투기장은 PlayMode 검증이 막혀 있어(WM-165 item 12) 이게 지금 가능한
	///   가장 가까운 「진짜로 돌아가나」다.
	///
	/// 전술은 상상해서 만들지 않고 **실제 출하 프리셋**(`ArenaMatchConfig_Dolls.asset`)을 그대로 옮겼다:
	///   룰0: EnemyInRange(가장 가까운 적, 사거리 7) → UseSkill(슬롯 2)
	///   룰1: (조건 없음 = 항상) 가장 가까운 적 → MoveToTarget
	/// 즉 「7 안이면 쏘고, 아니면 다가간다」. 이 시험이 깨지면 출하 프리셋이 안 도는 것이다.
	/// </summary>
	public class ArenaDryMatchTests
	{
		private sealed class FakeCombatant : ICombatant
		{
			public int CombatantId { get; set; }
			public int TeamId { get; set; }
			public bool IsAlive { get; set; } = true;
			public Vector3 Position { get; set; }
			public int Hp { get; set; } = 100;
			public int HpMax { get; set; } = 100;
			public override string ToString() => $"팀{TeamId}#{CombatantId}";
		}

		private sealed class RecordingActuator : ITacticActuator
		{
			public string LastAction = "none";
			public int LastSkillSlot = -1;
			public ICombatant LastTarget;

			public bool StopsToAttack => true;
			public void UseSkill(int skillSlot, ICombatant target)
			{
				LastAction = "UseSkill";
				LastSkillSlot = skillSlot;
				LastTarget = target;
			}
			public void MoveToward(ICombatant target) { LastAction = "MoveToward"; LastTarget = target; }
			public void Approach(ICombatant target, float stopDistance) { LastAction = "Approach"; LastTarget = target; }
			public void Retreat(ICombatant target) { LastAction = "Retreat"; LastTarget = target; }
			public void Hold() { LastAction = "Hold"; }
		}

		// 출하 프리셋과 같은 수치 — 여기가 어긋나면 시험이 다른 게임을 검사하는 셈이 된다.
		private const float PRESET_ATTACK_RANGE = 7f;
		private const int PRESET_SKILL_SLOT = 2;

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
				// 출하본은 Conditions 가 **빈 리스트**다(= 항상 참). 그 모양 그대로 둔다 —
				// Always 조건을 하나 넣어 「고쳐 쓰면」 빈 리스트 경로를 아무도 안 밟게 된다.
				Conditions = new List<TacticCondition>(),
				Target = new TargetQuery(TargetSide.Enemy, TargetPriority.Nearest, 0f),
				Action = new TacticAction { Kind = ActionKind.MoveToTarget },
			});
			return program;
		}

		/// <summary> 두 팀을 세우고 전원을 진짜 TargetingSystem 에 등록한 뒤 각자 한 틱 돌린다. </summary>
		private static Dictionary<ICombatant, RecordingActuator> RunOneTick(List<FakeCombatant> roster)
		{
			TargetingSystem targeting = new();
			foreach (FakeCombatant combatant in roster)
				targeting.Register(combatant);

			Dictionary<ICombatant, RecordingActuator> log = new();
			foreach (FakeCombatant combatant in roster)
			{
				RecordingActuator actuator = new();
				TacticContext context = new(combatant, targeting, actuator, _ => true);
				new TacticBTRunner(context, PresetTactic()).UpdateBT();
				log[combatant] = actuator;
			}
			return log;
		}

		private static List<FakeCombatant> Roster(params (int team, float x)[] units)
		{
			List<FakeCombatant> roster = new();
			for (int i = 0; i < units.Length; i++)
			{
				roster.Add(new FakeCombatant
				{
					CombatantId = i,
					TeamId = units[i].team,
					Position = new Vector3(units[i].x, 0f, 0f),
				});
			}
			return roster;
		}

		// 30 만큼 벌어진 3v3 = 매치 시작 직후. 아무도 사거리 안에 없으니 **전원 전진**해야 한다.
		// 여기서 Hold 가 하나라도 나오면 그게 「왜 안 움직이지」의 EditMode 판 재현이다.
		[Test]
		public void 멀리_떨어진_3v3_은_전원_전진한다()
		{
			List<FakeCombatant> roster = Roster((0, -15f), (0, -16f), (0, -17f), (1, 15f), (1, 16f), (1, 17f));
			Dictionary<ICombatant, RecordingActuator> log = RunOneTick(roster);

			foreach (FakeCombatant combatant in roster)
			{
				Assert.AreEqual("MoveToward", log[combatant].LastAction, combatant + " 가 전진하지 않았다");
				Assert.AreEqual(1 - combatant.TeamId, ((FakeCombatant)log[combatant].LastTarget).TeamId,
					combatant + " 가 적이 아닌 것을 노렸다");
			}
		}

		// 사거리(7) 안으로 붙은 순간 전원 스킬로 전환. 슬롯 번호까지 본다 —
		// 편집기에서 슬롯을 못 고르던 시절 전부 0번으로 굳었던 자리라 회귀 표면이다.
		[Test]
		public void 사거리_안에서는_전원_프리셋_슬롯으로_시전한다()
		{
			List<FakeCombatant> roster = Roster((0, -2f), (0, -3f), (1, 2f), (1, 3f));
			Dictionary<ICombatant, RecordingActuator> log = RunOneTick(roster);

			foreach (FakeCombatant combatant in roster)
			{
				Assert.AreEqual("UseSkill", log[combatant].LastAction, combatant + " 가 시전하지 않았다");
				Assert.AreEqual(PRESET_SKILL_SLOT, log[combatant].LastSkillSlot, combatant + " 의 스킬 슬롯이 프리셋과 다르다");
			}
		}

		// 경계 바로 안/밖. MaxRange 는 **탐색 반경**이라 밖이면 룰0 의 타겟 해석 자체가 실패해
		// 룰1(무제한 이동)으로 떨어져야 한다 — 「가만히 선다」가 아니라.
		[Test]
		public void 사거리_경계에서_시전과_전진이_갈린다()
		{
			Dictionary<ICombatant, RecordingActuator> inside = RunOneTick(Roster((0, 0f), (1, PRESET_ATTACK_RANGE - 0.5f)));
			Assert.AreEqual("UseSkill", inside[FirstOfTeam(inside, 0)].LastAction, "사거리 안인데 시전 안 함");

			Dictionary<ICombatant, RecordingActuator> outside = RunOneTick(Roster((0, 0f), (1, PRESET_ATTACK_RANGE + 0.5f)));
			Assert.AreEqual("MoveToward", outside[FirstOfTeam(outside, 0)].LastAction,
				"사거리 밖이면 fallback 으로 전진해야 한다(멈추면 매치가 안 끝난다)");
		}

		// 아군만 남으면 노릴 게 없다 → 타겟 필요 행동은 전부 불발이고 **아무 일도 안 일어난다**.
		// 이건 버그가 아니라 정의다. 다만 「Hold 조차 아닌 none」임을 못박아, 나중에 누가
		// 무타겟 fallback 을 넣을 때 이 시험이 의도적으로 깨지게 한다.
		[Test]
		public void 적이_없으면_아무_행동도_안_한다()
		{
			Dictionary<ICombatant, RecordingActuator> log = RunOneTick(Roster((0, 0f), (0, 1f)));
			foreach (KeyValuePair<ICombatant, RecordingActuator> pair in log)
				Assert.AreEqual("none", pair.Value.LastAction, pair.Key + " 가 적 없이 뭔가를 했다");
		}

		// 죽은 적은 후보에서 빠져야 한다 — 안 빠지면 산 유닛이 시체를 향해 걸어가고 매치가 안 끝난다.
		[Test]
		public void 죽은_적은_노리지_않는다()
		{
			List<FakeCombatant> roster = Roster((0, 0f), (1, 3f), (1, 20f));
			roster[1].IsAlive = false; // 코앞의 적이 죽어 있다

			Dictionary<ICombatant, RecordingActuator> log = RunOneTick(roster);
			RecordingActuator actor = log[roster[0]];

			Assert.AreEqual("MoveToward", actor.LastAction, "죽은 적을 사거리 안으로 세어 시전했다");
			Assert.AreEqual(2, ((FakeCombatant)actor.LastTarget).CombatantId, "살아있는 먼 적을 노려야 한다");
		}

		// ── 베낀 프리셋이 아직 출하본과 같은가 ────────────────────────────────────────
		//
		// ★ 위 머리말이 「출하 프리셋을 그대로 옮겼다」고 적어뒀지만, **적어둔 것만으로는 안 지켜진다.**
		//   사람이 인스펙터에서 사거리나 스킬 슬롯을 바꾸는 순간 이 시험 묶음은 조용히
		//   *다른 게임*을 검사하게 된다 — 전부 초록인 채로. 그래서 기계가 대조한다.
		//
		// 출하본을 통째로 얼리지는 않는다(전술은 사람이 만지라고 있는 것이다).
		// 「베낀 그 모양이 출하본 어딘가에 아직 실재하는가」만 본다 — 없어지면 여기를 갱신하라는 뜻이다.
		[Test]
		public void 베낀_프리셋이_아직_출하_설정_안에_실재한다()
		{
			bool found = false;
			List<string> seen = new();

			foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:" + nameof(ArenaMatchConfig)))
			{
				string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
				ArenaMatchConfig config = UnityEditor.AssetDatabase.LoadAssetAtPath<ArenaMatchConfig>(path);
				if (config == null)
					continue;

				foreach (ArenaMatchConfig.ArenaUnitEntry entry in config.Roster)
				{
					if (entry.Tactic == null)
						continue;

					foreach (TacticRule rule in entry.Tactic.Rules)
					{
						if (rule.Action.Kind != ActionKind.UseSkill)
							continue;

						seen.Add($"{config.name}: 사거리 {rule.Target.MaxRange} / 슬롯 {rule.Action.SkillSlot}");
						if (Mathf.Approximately(rule.Target.MaxRange, PRESET_ATTACK_RANGE) && rule.Action.SkillSlot == PRESET_SKILL_SLOT)
							found = true;
					}
				}
			}

			Assert.IsTrue(found,
				$"이 시험 묶음이 베낀 프리셋(사거리 {PRESET_ATTACK_RANGE} / 슬롯 {PRESET_SKILL_SLOT})이 "
				+ "출하 설정 어디에도 없다 — 지금 이 묶음은 실제로 배포되는 전술이 아니라 옛 사본을 검사하는 중이다. "
				+ "출하본을 보고 위 상수를 갱신하라. 실제 출하 값: " + string.Join(" · ", seen));
		}

		private static ICombatant FirstOfTeam(Dictionary<ICombatant, RecordingActuator> log, int teamId)
		{
			foreach (KeyValuePair<ICombatant, RecordingActuator> pair in log)
			{
				if (pair.Key.TeamId == teamId)
					return pair.Key;
			}
			Assert.Fail("팀 " + teamId + " 이 없다");
			return null;
		}
	}
}

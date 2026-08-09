using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
// ★ 좌표는 판정 쪽 (TASK-WM-214) — 시험이 구현하는 ICombatant 가 판정 타입을 쓴다.
using Vector3 = WitchMendokusai.Numerics.Vector3;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 편집기가 <b>새로 열어준 것들이 실제로 도는지</b> — 진짜 TargetingSystem 을 물려서 확인한다.
	///
	/// ★ 왜 이 시험이 따로 있나 (2026-08-06): WM-165 item 10 에서 죽어 있던 setter 5개를 배선했다
	///   (조건 연산자·값 / 조건 슬롯 / 행동 슬롯 / 타겟 진영 / 사거리). 그런데 「편집기가 값을 넣을 수
	///   있다」와 「그 값으로 짠 전술이 실제로 발동한다」는 **다른 이야기**다. 배선만 확인하고 끝내면
	///   「칸은 생겼는데 여전히 안 먹는다」를 못 잡는다 — 그게 바로 배선 전의 증상이었다.
	///
	///   `TacticEngineTests` 는 이 조건들을 <b>FakeResolver</b> 로 검증한다(타겟이 항상 주어짐).
	///   여기서는 <b>진짜 TargetingSystem</b> 이 진영·사거리·생존을 걸러낸 뒤에도 룰이 도는지를 본다.
	///   특히 <b>아군 대상</b>은 편집기가 진영을 못 고르던 시절엔 표현 자체가 불가능했던 영역이다.
	/// </summary>
	public class TacticLanguageReachTests
	{
		private sealed class FakeCombatant : ICombatant
		{
			public int CombatantId { get; set; }
			public int TeamId { get; set; }
			public bool IsAlive { get; set; } = true;
			public Vector3 Position { get; set; }
			public int Hp { get; set; } = 100;
			public int HpMax { get; set; } = 100;
			public override string ToString() => $"팀{TeamId}#{CombatantId}(hp{Hp})";
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

		/// <summary> self 를 포함한 전원을 진짜 TargetingSystem 에 등록하고 self 의 전술을 한 틱 돌린다. </summary>
		private static RecordingActuator RunOneTick(TacticProgram program, ICombatant self, params ICombatant[] others)
		{
			TargetingSystem targeting = new();
			targeting.Register(self);
			foreach (ICombatant other in others)
				targeting.Register(other);

			RecordingActuator actuator = new();
			TacticContext context = new(self, targeting, actuator, _ => true);
			new TacticBTRunner(context, program).UpdateBT();
			return actuator;
		}

		private static TacticRule Rule(TacticCondition[] conditions, TargetQuery target, ActionKind action, int skillSlot = 0)
		{
			return new TacticRule
			{
				Conditions = new List<TacticCondition>(conditions),
				Target = target,
				Action = new TacticAction { Kind = action, SkillSlot = skillSlot },
			};
		}

		// ── ① 「HP 30% 아래면 후퇴」 — FF12 갬빗의 교과서적 첫 줄 ─────────────────────────
		//    연산자/값 칸이 없던 시절 이 줄은 (Equal, 0) 으로 굳어 **죽어야 참**이었다(= 영영 안 먹음).
		//    이제 정말 발동하는지, 그리고 **HP 가 넉넉할 땐 발동 안 하는지**(둘 다) 본다.
		[Test]
		public void HP_비율_임계_후퇴가_실제로_발동한다()
		{
			TacticProgram program = new();
			program.Rules.Add(Rule(
				new[] { new TacticCondition { Kind = ConditionKind.SelfHpRatio, Operator = ComparisonOperator.LessThan, Value = 0.3f } },
				new TargetQuery(TargetSide.Enemy, TargetPriority.Nearest, 0f), ActionKind.Retreat));
			program.Rules.Add(Rule(
				new TacticCondition[0],
				new TargetQuery(TargetSide.Enemy, TargetPriority.Nearest, 0f), ActionKind.MoveToTarget));

			FakeCombatant hurt = new() { CombatantId = 0, TeamId = 0, Hp = 20, HpMax = 100 };
			FakeCombatant enemy = new() { CombatantId = 1, TeamId = 1, Position = new Vector3(5f, 0f, 0f) };
			Assert.AreEqual("Retreat", RunOneTick(program, hurt, enemy).LastAction, "HP 20% 인데 후퇴 안 함");

			FakeCombatant healthy = new() { CombatantId = 0, TeamId = 0, Hp = 90, HpMax = 100 };
			FakeCombatant enemy2 = new() { CombatantId = 1, TeamId = 1, Position = new Vector3(5f, 0f, 0f) };
			Assert.AreEqual("MoveToward", RunOneTick(program, healthy, enemy2).LastAction,
				"HP 90% 인데 후퇴했다 — 임계가 안 걸린 것(둘 다 참이면 값이 무시되고 있다는 뜻)");
		}

		// ── ② 아군 대상 — 편집기가 진영을 못 고르던 시절엔 **표현 자체가 불가능**했던 영역 ──────
		//    「가장 다친 아군에게 스킬」. 진짜 TargetingSystem 이 자기 자신을 후보에서 빼는지도 같이 걸린다.
		[Test]
		public void 가장_다친_아군을_고를_수_있다()
		{
			TacticProgram program = new();
			program.Rules.Add(Rule(
				new TacticCondition[0],
				new TargetQuery(TargetSide.Ally, TargetPriority.LowestHpRatio, 0f), ActionKind.UseSkill, skillSlot: 1));

			// self 가 판 안에서 가장 다쳤다 — 그래도 **아군 질의는 자기를 제외**하므로 동료가 뽑혀야 한다.
			FakeCombatant healer = new() { CombatantId = 0, TeamId = 0, Hp = 5, HpMax = 100 };
			FakeCombatant woundedAlly = new() { CombatantId = 1, TeamId = 0, Hp = 30, HpMax = 100, Position = new Vector3(2f, 0f, 0f) };
			FakeCombatant healthyAlly = new() { CombatantId = 2, TeamId = 0, Hp = 100, HpMax = 100, Position = new Vector3(3f, 0f, 0f) };
			FakeCombatant enemy = new() { CombatantId = 3, TeamId = 1, Hp = 1, HpMax = 100, Position = new Vector3(4f, 0f, 0f) };

			RecordingActuator actuator = RunOneTick(program, healer, woundedAlly, healthyAlly, enemy);

			Assert.AreEqual("UseSkill", actuator.LastAction);
			Assert.AreEqual(1, actuator.LastSkillSlot, "행동 슬롯이 전달되지 않았다");
			Assert.AreSame(woundedAlly, actuator.LastTarget,
				"가장 다친 **아군**이 아니다 — 자기 자신(hp5)이나 적(hp1)이 뽑혔다면 진영 필터가 안 걸린 것");
		}

		// ── ③ 사거리 칸 — 「밖이면 아예 후보가 없다」가 성립해야 fallback 으로 떨어진다 ──────
		[Test]
		public void 사거리_칸이_후보를_실제로_자른다()
		{
			TacticProgram program = new();
			program.Rules.Add(Rule(
				new[] { new TacticCondition { Kind = ConditionKind.EnemyInRange } },
				new TargetQuery(TargetSide.Enemy, TargetPriority.Nearest, 3f), ActionKind.UseSkill, skillSlot: 7));
			program.Rules.Add(Rule(
				new TacticCondition[0],
				new TargetQuery(TargetSide.Enemy, TargetPriority.Nearest, 0f), ActionKind.MoveToTarget));

			FakeCombatant self = new() { CombatantId = 0, TeamId = 0 };
			FakeCombatant near = new() { CombatantId = 1, TeamId = 1, Position = new Vector3(2f, 0f, 0f) };
			RecordingActuator inRange = RunOneTick(program, self, near);
			Assert.AreEqual("UseSkill", inRange.LastAction);
			Assert.AreEqual(7, inRange.LastSkillSlot, "슬롯 7 을 골랐는데 다른 게 시전됐다");

			FakeCombatant self2 = new() { CombatantId = 0, TeamId = 0 };
			FakeCombatant far = new() { CombatantId = 1, TeamId = 1, Position = new Vector3(9f, 0f, 0f) };
			Assert.AreEqual("MoveToward", RunOneTick(program, self2, far).LastAction,
				"사거리 3 밖인데 시전했다 — MaxRange 가 안 걸린 것");
		}

		// ── ④ 조건 슬롯 — SkillReady 가 **어느 슬롯을 보는지**가 실제로 반영되나 ────────────
		//    편집기가 조건 슬롯을 못 고르던 시절엔 항상 0번만 봤다.
		[Test]
		public void 조건_슬롯이_실제로_반영된다()
		{
			TacticProgram program = new();
			program.Rules.Add(Rule(
				new[] { new TacticCondition { Kind = ConditionKind.SkillReady, SkillSlot = 3 } },
				new TargetQuery(TargetSide.Enemy, TargetPriority.Nearest, 0f), ActionKind.UseSkill, skillSlot: 3));
			program.Rules.Add(Rule(
				new TacticCondition[0],
				new TargetQuery(TargetSide.Enemy, TargetPriority.Nearest, 0f), ActionKind.MoveToTarget));

			FakeCombatant self = new() { CombatantId = 0, TeamId = 0 };
			FakeCombatant enemy = new() { CombatantId = 1, TeamId = 1, Position = new Vector3(2f, 0f, 0f) };

			TargetingSystem targeting = new();
			targeting.Register(self);
			targeting.Register(enemy);

			// 3번만 준비됨 → 조건 통과
			RecordingActuator ready = new();
			new TacticBTRunner(new TacticContext(self, targeting, ready, slot => slot == 3), program).UpdateBT();
			Assert.AreEqual("UseSkill", ready.LastAction, "3번이 준비됐는데 시전 안 함");

			// 0번만 준비됨 → 조건이 0번을 본다면 통과해버린다(옛 버그). 3번을 봐야 하므로 fallback 이어야 한다.
			RecordingActuator notReady = new();
			new TacticBTRunner(new TacticContext(self, targeting, notReady, slot => slot == 0), program).UpdateBT();
			Assert.AreEqual("MoveToward", notReady.LastAction,
				"조건이 0번 슬롯을 보고 있다 — SkillSlot 이 무시되는 것(배선 전 증상)");
		}
	}
}

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
// ★ 좌표는 판정 쪽 (TASK-WM-214) — 시험이 구현하는 ICombatant 가 판정 타입을 쓴다.
using Vector3 = WitchMendokusai.Numerics.Vector3;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 포탑 몸의 재사용 회귀 — 포탑을 팔면 그 몸은 풀로 돌아가고 다음 포탑이 *같은 몸*을 받는다
	/// (TowerDefenseMatch 의 ReleaseUnit(pool, sold)). 앞 포탑이 꿰뚫은 횟수가 지워지지 않으면
	/// 아무도 안 꿰뚫는데 마수는 관통에 계속 적응한다 — 적응이 영구히 부풀어 벌칙이 된다.
	/// TASK-WM-194.
	/// </summary>
	public class TowerDefenseWeaponReuseTests
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

		private GameObject weaponObject;

		[TearDown]
		public void TearDown()
		{
			if (weaponObject != null)
				Object.DestroyImmediate(weaponObject);
		}

		private static TowerDefenseTowerArchetype Archetype(string name, int pierce)
		{
			return new TowerDefenseTowerArchetype(name, "시험용", attackRange: 12f, attackDamage: 5,
				attackCooldown: 0.1f, pierceCount: pierce, color: Color.white);
		}

		/// <summary> 한 줄로 늘어선 마수 — 관통이 실제로 셈을 올릴 수 있는 유일한 배치다. </summary>
		private TowerDefenseWeapon ArmWeapon(TowerDefenseTowerArchetype archetype)
		{
			weaponObject = new GameObject("시험용 포탑");
			TowerDefenseWeapon weapon = weaponObject.AddComponent<TowerDefenseWeapon>();

			FakeCombatant self = new() { CombatantId = 0, TeamId = 0, Position = Vector3.zero };
			List<ICombatant> enemies = new()
			{
				new FakeCombatant { CombatantId = 1, TeamId = 1, Position = new Vector3(2f, 0f, 0f) },
				new FakeCombatant { CombatantId = 2, TeamId = 1, Position = new Vector3(4f, 0f, 0f) },
				new FakeCombatant { CombatantId = 3, TeamId = 1, Position = new Vector3(6f, 0f, 0f) },
			};

			TargetingSystem targeting = new();
			targeting.Register(self);
			foreach (ICombatant enemy in enemies)
				targeting.Register(enemy);

			weapon.Configure(archetype, targeting, self, enemies);
			return weapon;
		}

		[Test]
		public void 관통_포탑은_줄지어_선_마수를_꿰뚫은_만큼_센다()
		{
			// 이 시험이 먼저 성립해야 아래 회귀가 「0 이라서 통과」하는 가짜가 되지 않는다.
			TowerDefenseWeapon weapon = ArmWeapon(Archetype("관통 포탑", pierce: 3));

			weapon.Tick(1f);

			Assert.AreEqual(2, weapon.PierceHits, "표적 너머 둘을 꿰뚫었어야 한다 — 안 세면 적응 규칙 자체가 죽는다.");
		}

		[Test]
		public void 다른_포탑으로_다시_세우면_앞_포탑의_관통_셈이_사라진다()
		{
			TowerDefenseWeapon weapon = ArmWeapon(Archetype("관통 포탑", pierce: 3));
			weapon.Tick(1f);
			Assume.That(weapon.PierceHits, Is.GreaterThan(0), "앞 포탑이 실제로 꿰뚫어야 회귀를 잴 수 있다.");

			// 판매 → 같은 몸으로 기본 포탑을 다시 세운다. Configure = 「이 몸은 이제 다른 포탑이다」.
			weapon.Configure(Archetype("기본 포탑", pierce: 1), new TargetingSystem(),
				new FakeCombatant { CombatantId = 0, TeamId = 0, Position = Vector3.zero },
				new List<ICombatant>());

			Assert.AreEqual(0, weapon.PierceHits,
				"아무도 안 꿰뚫는 기본 포탑이 앞 포탑의 셈을 물려받으면, 마수는 관통에 영원히 적응한 채로 남는다.");
		}

		[Test]
		public void 한_발_쏠_때마다_소리를_알린다()
		{
			// ★ 이 통로가 없으면 「둥지 옆에서 난사해도 조용하다」가 된다 — 소리 규칙의 절반이 죽는다.
			//   실제로 소리 규칙을 넣을 때 이 절반을 빠뜨린 채 「쏘는 소리」라고 적었다.
			TowerDefenseWeapon weapon = ArmWeapon(Archetype("기본 포탑", pierce: 1));
			int shouts = 0;
			weapon.ReportNoise = _ => shouts++;

			weapon.Tick(1f);

			Assert.AreEqual(1, shouts, "한 발 쐈으면 소리도 한 번이어야 한다.");
		}

		[Test]
		public void 쏠_대상이_없으면_소리도_안_난다()
		{
			// 가만히 서 있는 포탑이 소리를 내면, 아무것도 안 하는데 마수가 깨어난다.
			weaponObject = new GameObject("빈 시험용 포탑");
			TowerDefenseWeapon weapon = weaponObject.AddComponent<TowerDefenseWeapon>();
			FakeCombatant self = new() { CombatantId = 0, TeamId = 0, Position = Vector3.zero };
			weapon.Configure(Archetype("기본 포탑", pierce: 1), new TargetingSystem(), self, new List<ICombatant>());

			int shouts = 0;
			weapon.ReportNoise = _ => shouts++;
			weapon.Tick(1f);

			Assert.AreEqual(0, shouts);
		}

		[Test]
		public void 소리를_듣는_이가_없어도_안_터진다()
		{
			// 판이 아직 안 붙었을 때(통로 null) 사격이 예외로 죽으면 방어 전체가 멈춘다.
			TowerDefenseWeapon weapon = ArmWeapon(Archetype("기본 포탑", pierce: 1));
			weapon.ReportNoise = null;

			Assert.DoesNotThrow(() => weapon.Tick(1f));
		}
	}
}

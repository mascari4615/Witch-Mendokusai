using System.Text;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 14구역 벽 재기 (기능 감사 2026-09-01: 손으로 눌러도 14구역, 공격력 4배로도 안 뚫림, 원인 미상)
	/// 판정 아님. 사람 흉내 (수치 7종 다 올림, 합성, 장비) 로 살아 있는 전투를 굴리며 구역마다 수치를 찍음
	/// </summary>
	public sealed class IdleStageWallProbe
	{
		private const double TICK = 1d;
		private const double PLAY_SECONDS = 40d * 60d;
		private const double PRESS_EVERY = 5d;

		[Test]
		public void PrintStageCurve_ForSomeoneWhoPressesEverything() => Probe(AllStats, "수치 7종 다 올림");

		/// <summary>09-01 감사가 누른 것에 가까운 손: 공격력만 (그때는 공격력과 속도 둘뿐)</summary>
		[Test]
		public void PrintStageCurve_DamageOnly() => Probe(new[] { IdleUpgradeKind.Damage }, "공격력만");

		[Test]
		public void PrintStageCurve_DamageAndSpeed() => Probe(new[] { IdleUpgradeKind.Damage, IdleUpgradeKind.AttackSpeed }, "공격력과 속도");

		private static readonly IdleUpgradeKind[] AllStats =
		{
			IdleUpgradeKind.Damage, IdleUpgradeKind.AttackSpeed, IdleUpgradeKind.MaxHealth, IdleUpgradeKind.Defense,
			IdleUpgradeKind.CriticalChance, IdleUpgradeKind.CriticalDamage, IdleUpgradeKind.Recovery,
		};

		private static void Probe(IdleUpgradeKind[] kinds, string label)
		{
			IdleTuning tuning = new IdleTuning();
			IdleSession session = new IdleSession(tuning);
			IdleState state = session.State;
			TestContext.WriteLine("[벽] 손: " + label);

			StringBuilder table = new StringBuilder();
			table.AppendLine("[벽] 구역 | 도달(분) | 적체력 | 보스체력 | 우리DPS | 적DPS | 자리체력 | 공격력 | 누적전멸 | 골드");

			double elapsed = 0d;
			double sincePress = 0d;
			int lastStage = 0;
			int wipes = 0;
			bool wasRepeating = false;

			while (elapsed < PLAY_SECONDS)
			{
				session.AdvanceLive(TICK);
				elapsed += TICK;
				sincePress += TICK;

				if (state.Repeating && wasRepeating == false)
				{
					wipes++;
				}
				wasRepeating = state.Repeating;

				if (sincePress >= PRESS_EVERY)
				{
					sincePress = 0d;
					PressEverything(state, tuning, kinds);
				}

				if (state.Stage > lastStage)
				{
					lastStage = state.Stage;
					table.AppendLine(Row(state, tuning, elapsed, wipes));
				}
			}

			table.AppendLine("[벽] 끝: 구역 " + state.Stage + " (최고 " + state.BestStage + ") 반복중=" + state.Repeating + " 전멸 " + wipes + "회");
			TestContext.WriteLine(table.ToString());
			Assert.Pass(table.ToString());
		}

		private static string Row(IdleState state, IdleTuning tuning, double elapsed, int wipes)
		{
			double foe = IdleModel.TargetHealthAt(state.Stage, tuning);
			double seat = IdleSquad.MaxHealthOf(state, tuning, 0);
			return state.Stage + " | " + (elapsed / 60d).ToString("0.0") + " | " + foe.ToString("0") + " | "
				+ (foe * tuning.BossHealthMultiplier).ToString("0") + " | "
				+ IdleModel.DamagePerSecond(state, tuning).ToString("0.0") + " | "
				+ IdleSquad.EnemyDamagePerSecond(state, tuning).ToString("0.00") + " | "
				+ seat.ToString("0") + " | " + IdleModel.DamageOf(state, tuning).ToString("0.0") + " | "
				+ wipes + " | " + state.Resource.ToString("0");
		}

		/// <summary>사람이 5초마다 화면을 훑고 누를 수 있는 것을 다 누른다는 가정</summary>
		private static void PressEverything(IdleState state, IdleTuning tuning, IdleUpgradeKind[] kinds)
		{
			IdlePlay.BuyProducers(state, tuning);
			RaiseCheapestStats(state, tuning, kinds);
			MergeAndWear(state, tuning);
			IdlePlay.PushOnAfterFailure(state, tuning);
		}

		private static void RaiseCheapestStats(IdleState state, IdleTuning tuning, IdleUpgradeKind[] kinds)
		{
			for (int guard = 0; guard < 200; guard++)
			{
				double bestCost = double.MaxValue;
				IdleUpgradeKind best = IdleUpgradeKind.Damage;
				for (int index = 0; index < kinds.Length; index++)
				{
					if (IdleModel.TryGetCost(state, tuning, IdleHeroes.STARTER_ID, kinds[index], 1, out double cost) && cost < bestCost)
					{
						bestCost = cost;
						best = kinds[index];
					}
				}

				if (bestCost > state.Resource)
				{
					return;
				}

				if (IdleModel.TryRaise(state, tuning, IdleHeroes.STARTER_ID, best, 1) == false)
				{
					return;
				}
			}
		}

		private static void MergeAndWear(IdleState state, IdleTuning tuning)
		{
			for (int tier = 1; tier < state.DroppedByTier.Length; tier++)
			{
				for (int slot = 0; slot < IdleGear.SLOT_COUNT; slot++)
				{
					while (IdleGear.TryMerge(state, tuning, tier, (IdleItemSlot)slot, out IdleItem _))
					{
					}
				}
			}

			for (int bag = 0; bag < state.Bag.Count; bag++)
			{
				IdleItem item = state.Bag[bag];
				IdleItem worn = state.Worn[IdleGear.WornAt(IdleHeroes.STARTER_ID, (int)item.Slot)];
				if (worn.Tier < item.Tier && IdleGear.TryEquip(state, IdleHeroes.STARTER_ID, bag))
				{
					bag = -1;
				}
			}
		}
	}
}

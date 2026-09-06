using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 유닛 능력치가 <b>엔진 없이도</b> 같은 답을 낸다 (TASK-WM-215 첫 증분).
	///
	/// 왜 이 시험이 필요한가: 전투 판정의 상태(체력·마나·치명타)가 씬 컴포넌트 안에 갇혀 있으면
	/// 헤드리스 서버가 같은 전투를 재현할 수 없다. 능력치를 DomainSDK 로 내린 뒤,
	/// 그 사실을 <b>엔진 밖 시험</b>이 매번 증명한다(같은 파일이 유니티 안에서도 돈다).
	/// </summary>
	public sealed class UnitStatEngineFreeTests
	{
		[Test]
		public void 새_능력치는_치명타_기본값을_갖는다()
		{
			UnitStat stat = new UnitStat();

			Assert.AreEqual(5, stat[UnitStatType.CRITICAL_CHANCE], "치명타 확률 기본값");
			Assert.AreEqual(30, stat[UnitStatType.CRITICAL_DAMAGE], "치명타 피해 기본값");
			Assert.AreEqual(0, stat[UnitStatType.HP_CUR], "채우기 전 체력은 0");
		}

		[Test]
		public void 기반체력을_넣고_Set_하면_현재체력이_최대치로_찬다()
		{
			UnitStat source = new UnitStat();
			source[UnitStatType.HP_MAX_STAT] = 120;
			source[UnitStatType.MANA_MAX_STAT] = 40;

			UnitStat target = new UnitStat();
			target.Set(source);

			Assert.AreEqual(120, target[UnitStatType.HP_MAX], "최대 체력 = 기반 체력");
			Assert.AreEqual(120, target[UnitStatType.HP_CUR], "현재 체력이 가득 찬다");
			Assert.AreEqual(40, target[UnitStatType.MANA_MAX]);
			Assert.AreEqual(40, target[UnitStatType.MANA_CUR]);
		}

		[Test]
		public void 능치_변화는_구독자에게_값과_함께_전달된다()
		{
			UnitStat stat = new UnitStat();
			int lastSeen = -1;
			stat.AddListener(UnitStatType.HP_CUR, value => lastSeen = value);

			stat[UnitStatType.HP_CUR] = 77;

			Assert.AreEqual(77, lastSeen, "체력이 바뀌면 그 값이 그대로 흘러가야 한다");
		}

		[Test]
		public void 피해를_받아도_체력은_0_아래로_안_내려간다()
		{
			UnitStat source = new UnitStat();
			source[UnitStatType.HP_MAX_STAT] = 30;
			UnitStat stat = new UnitStat();
			stat.Set(source);

			int afterHit = stat[UnitStatType.HP_CUR] - 100;
			int clamped = Numerics.Mathf.Clamp(afterHit, 0, stat[UnitStatType.HP_MAX]);
			stat[UnitStatType.HP_CUR] = clamped;

			Assert.AreEqual(0, stat[UnitStatType.HP_CUR], "과잉 피해는 0 에서 멈춘다");
		}

		[Test]
		public void 더하기는_같은_칸끼리_합산된다()
		{
			UnitStat baseStat = new UnitStat();
			baseStat[UnitStatType.HP_MAX_STAT] = 50;

			UnitStat bonus = new UnitStat();
			bonus[UnitStatType.HP_MAX_STAT] = 25;

			baseStat.Add(bonus);

			Assert.AreEqual(75, baseStat[UnitStatType.HP_MAX_STAT], "장비·버프가 얹히는 자리");
		}
	}
}

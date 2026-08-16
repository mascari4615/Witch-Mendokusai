using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Act;
using WitchMendokusai.DomainSDK.Life;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-408 — 행동 원장. WM 은 여러 게임이 한 세계에 동시에 사는 구조라,
	/// 코어가 특정 장르의 규칙(취침·기력)을 전 게임에 강제해서도, 지역 경계에서 규칙이
	/// 바뀌어서도 안 된다. 그래서 규칙은 <b>행동</b>이 쥐고 코어는 원장 판정만 한다.
	///
	/// 이 테스트의 핵심은 <b>반례 3종</b>이 같은 코어로 돈다는 것 — 특히 ③ 아무것도
	/// 선언 안 한 행동이 세계를 1도 안 건드리는 것이 「강제 0」의 구조적 증명이다.
	/// </summary>
	public sealed class ActLedgerTest
	{
		private const int HOURS_PER_DAY = 24;
		private const int DAYS_PER_SEASON = 28;
		private const int SEASONS_PER_YEAR = 4;
		private const float NEED_MAX = 100f;

		private static readonly ResourceId SEED = new ResourceId(900);
		private static readonly ResourceId CROP = new ResourceId(901);

		private sealed class FakePool : IActResourcePool
		{
			private readonly Dictionary<ResourceId, int> amountById = new();

			public FakePool(params (ResourceId Resource, int Amount)[] initial)
			{
				foreach ((ResourceId resource, int amount) in initial)
				{
					amountById[resource] = amount;
				}
			}

			public int AmountOf(ResourceId resource) => amountById.TryGetValue(resource, out int amount) ? amount : 0;

			public void Add(ResourceId resource, int amount) => amountById[resource] = AmountOf(resource) + amount;
		}

		private static NeedProfile Profile()
		{
			Dictionary<NeedKind, NeedSpec> specs = new()
			{
				{ NeedKind.Hunger, new NeedSpec(1f, 50f, NEED_MAX) },
				{ NeedKind.Energy, new NeedSpec(1f, 50f, NEED_MAX) },
				{ NeedKind.Mood, new NeedSpec(1f, 50f, NEED_MAX) },
				{ NeedKind.Social, new NeedSpec(1f, 50f, NEED_MAX) },
			};
			return new NeedProfile(specs);
		}

		private static NeedState Body(float energy = 100f, float hunger = 100f)
		{
			return new NeedState(new Dictionary<NeedKind, float>
			{
				{ NeedKind.Hunger, hunger }, { NeedKind.Energy, energy }, { NeedKind.Mood, 100f }, { NeedKind.Social, 100f },
			});
		}

		private static WorldCalendar Calendar(int startHour = 6, int startMinute = 0)
		{
			return new WorldCalendar(HOURS_PER_DAY, DAYS_PER_SEASON, SEASONS_PER_YEAR, startHour, startMinute);
		}

		// 반례 1 — 밭 갈기 = 시간 늘고 기운 든다.

		[Test]
		public void TillField_SpendsTimeAndEnergy()
		{
			NeedState body = Body(energy: 80f);
			WorldCalendar calendar = Calendar(startHour: 6);
			ActSpec till = new(60, new[] { new ActNeedDelta(NeedKind.Energy, -10f) });

			bool applied = ActLedger.TryApply(till, new ActContext(body, Profile(), null, calendar), out ActOutcome outcome);

			Assert.That(applied, Is.True);
			Assert.That(outcome.Applied, Is.True);
			Assert.That(body.Get(NeedKind.Energy), Is.EqualTo(70f), "선언한 만큼 기운이 든다");
			Assert.That(calendar.Hour, Is.EqualTo(7), "선언한 만큼 하늘이 흐른다");
			Assert.That(outcome.DayChanged, Is.False);
		}

		[Test]
		public void Planting_ConsumesSeed_AndProducesCrop()
		{
			FakePool pool = new((SEED, 3), (CROP, 0));
			ActSpec plant = new(10, null, new[] { new ActResourceDelta(SEED, -1), new ActResourceDelta(CROP, 1) });

			bool applied = ActLedger.TryApply(plant, new ActContext(Body(), Profile(), pool, Calendar()), out _);

			Assert.That(applied, Is.True);
			Assert.That(pool.AmountOf(SEED), Is.EqualTo(2));
			Assert.That(pool.AmountOf(CROP), Is.EqualTo(1));
		}

		// 반례 2 — 잠자기 = 하루가 넘어가고 기운이 찬다.

		[Test]
		public void Sleep_CrossesMidnight_AndRestoresEnergy()
		{
			NeedState body = Body(energy: 15f);
			WorldCalendar calendar = Calendar(startHour: 22);
			int dayBefore = calendar.TotalDays();
			ActSpec sleep = new(8 * 60, new[] { new ActNeedDelta(NeedKind.Energy, +100f) });

			ActLedger.TryApply(sleep, new ActContext(body, Profile(), null, calendar), out ActOutcome outcome);

			Assert.That(outcome.DayChanged, Is.True, "자정을 넘었으면 하루가 바뀌었다고 알려야 정산이 걸린다");
			Assert.That(calendar.TotalDays(), Is.EqualTo(dayBefore + 1));
			Assert.That(calendar.Hour, Is.EqualTo(6));
			Assert.That(body.Get(NeedKind.Energy), Is.EqualTo(NEED_MAX), "회복은 상한에서 멈춘다");
		}

		[Test]
		public void Sleep_IsJustAnAct_NotAWorldRule()
		{
			// 「취침 정산」이 코어의 강제가 아니라 행동 하나임을 못박는다 —
			// 안 걸면 세계는 취침을 모른다.
			NeedState body = Body(energy: 15f);
			WorldCalendar calendar = Calendar(startHour: 22);

			ActLedger.TryApply(ActSpec.Free, new ActContext(body, Profile(), null, calendar), out _);

			Assert.That(calendar.Hour, Is.EqualTo(22), "잠을 안 자면 밤은 그냥 밤이다 — 코어가 재우지 않는다");
			Assert.That(body.Get(NeedKind.Energy), Is.EqualTo(15f));
		}

		// 반례 3 — 소모 0 행동(캐비닛 게임) = 세계 불변 = 강제 0.

		[Test]
		public void FreeAct_ChangesNothing_ProvingZeroCoercion()
		{
			NeedState body = Body(energy: 42f, hunger: 37f);
			WorldCalendar calendar = Calendar(startHour: 13, startMinute: 30);
			int minutesBefore = calendar.TotalMinutes();

			bool applied = ActLedger.TryApply(ActSpec.Free, new ActContext(body, Profile(), null, calendar), out ActOutcome outcome);

			Assert.That(applied, Is.True, "대가 없는 행동도 실패가 아니다 — 그냥 아무 일도 안 일어난다");
			Assert.That(outcome.DayChanged, Is.False);
			Assert.That(calendar.TotalMinutes(), Is.EqualTo(minutesBefore), "시계가 한 톨도 안 움직인다");
			Assert.That(body.Get(NeedKind.Energy), Is.EqualTo(42f));
			Assert.That(body.Get(NeedKind.Hunger), Is.EqualTo(37f));
			Assert.That(ActSpec.Free.IsFree, Is.True);
		}

		[Test]
		public void FreeAct_NeedsNoWorldAtAll()
		{
			// 시계도 몸도 창고도 없는 자리(순수 미니게임)에서도 원장은 그냥 통과한다.
			bool applied = ActLedger.TryApply(ActSpec.Free, new ActContext(), out ActOutcome outcome);

			Assert.That(applied, Is.True);
			Assert.That(outcome.Rejection, Is.EqualTo(ActRejection.None));
		}

		// 원장 불변식.

		[Test]
		public void Rejects_WhenEnergyShort_AndTouchesNothing()
		{
			NeedState body = Body(energy: 5f);
			WorldCalendar calendar = Calendar(startHour: 6);
			FakePool pool = new((SEED, 3));
			ActSpec heavy = new(60, new[] { new ActNeedDelta(NeedKind.Energy, -10f) }, new[] { new ActResourceDelta(SEED, -1) });

			bool applied = ActLedger.TryApply(heavy, new ActContext(body, Profile(), pool, calendar), out ActOutcome outcome);

			Assert.That(applied, Is.False);
			Assert.That(outcome.Rejection, Is.EqualTo(ActRejection.Need));
			Assert.That(outcome.RejectedNeed, Is.EqualTo(NeedKind.Energy), "무엇이 모자랐는지 표현층이 다시 추측하지 않게 알려준다");
			Assert.That(body.Get(NeedKind.Energy), Is.EqualTo(5f), "거절이면 몸은 그대로");
			Assert.That(pool.AmountOf(SEED), Is.EqualTo(3), "거절이면 창고도 그대로");
			Assert.That(calendar.Hour, Is.EqualTo(6), "거절이면 시계도 그대로");
		}

		[Test]
		public void Rejects_WhenResourceShort_BeforeSpendingEnergy()
		{
			// 전부-또는-전무: 기운만 빠지고 씨앗은 안 심긴 절반의 세계를 만들지 않는다.
			NeedState body = Body(energy: 100f);
			FakePool pool = new((SEED, 0));
			ActSpec plant = new(10, new[] { new ActNeedDelta(NeedKind.Energy, -10f) }, new[] { new ActResourceDelta(SEED, -1) });

			bool applied = ActLedger.TryApply(plant, new ActContext(body, Profile(), pool, Calendar()), out ActOutcome outcome);

			Assert.That(applied, Is.False);
			Assert.That(outcome.Rejection, Is.EqualTo(ActRejection.Resource));
			Assert.That(outcome.RejectedResource, Is.EqualTo(SEED));
			Assert.That(body.Get(NeedKind.Energy), Is.EqualTo(100f), "자원이 모자라면 기운도 안 든다");
		}

		[Test]
		public void AppliesOnlyWhatIsDeclared_NoAmbientDecay()
		{
			// 시간이 흐르는 동안의 자연 감소는 세계(NeedModel.Step)의 일이다.
			// 원장까지 그걸 걸면 같은 감소가 두 번 걸린다.
			NeedState body = Body(hunger: 80f);
			ActSpec walk = new(120, new[] { new ActNeedDelta(NeedKind.Energy, -5f) });

			ActLedger.TryApply(walk, new ActContext(body, Profile(), null, Calendar()), out _);

			Assert.That(body.Get(NeedKind.Hunger), Is.EqualTo(80f), "선언 안 한 욕구는 원장이 안 건드린다");
		}

		[Test]
		public void CanAfford_PreviewsWithoutTouchingTheWorld()
		{
			NeedState body = Body(energy: 5f);
			WorldCalendar calendar = Calendar(startHour: 6);
			ActSpec heavy = new(60, new[] { new ActNeedDelta(NeedKind.Energy, -10f) });
			ActContext context = new(body, Profile(), null, calendar);

			bool affordable = ActLedger.CanAfford(heavy, context, out ActOutcome rejection);

			Assert.That(affordable, Is.False);
			Assert.That(rejection.Rejection, Is.EqualTo(ActRejection.Need));
			Assert.That(body.Get(NeedKind.Energy), Is.EqualTo(5f));
			Assert.That(calendar.Hour, Is.EqualTo(6));
		}

		[Test]
		public void Ledger_HasNoRegionOrGameBranch()
		{
			// 문서가 아니라 타입으로 못박는다 — 원장이 아는 것은 몸·창고·하늘뿐이고,
			// 「어느 지역인가」·「어느 게임인가」를 물을 자리가 애초에 없다.
			System.Reflection.PropertyInfo[] properties = typeof(ActContext).GetProperties();
			foreach (System.Reflection.PropertyInfo property in properties)
			{
				Assert.That(property.Name.Contains("Region"), Is.False, "코어에 지역 개념이 새어 들어옴: " + property.Name);
				Assert.That(property.Name.Contains("Game"), Is.False, "코어에 게임 종류 개념이 새어 들어옴: " + property.Name);
				Assert.That(property.Name.Contains("Zone"), Is.False, "코어에 구역 개념이 새어 들어옴: " + property.Name);
			}
		}
	}
}

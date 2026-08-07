using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 뚫린 자리 규칙 회귀 — 「지킬 수 있는 만큼만 넓혀라」를 말이 아니라 규칙으로 만드는 부분.
	/// 한 번 실수가 영원한 벌이 되지 않게(식는다) · 사방이 뚫리면 방향이 없어야(상쇄) 한다. TASK-WM-194.
	/// </summary>
	public class TowerDefenseBreachTests
	{
		[Test]
		public void 아무것도_안_잃었으면_방향이_없다()
		{
			TowerDefenseBreach breach = new();

			Assert.IsFalse(breach.TryGetBiasAngle(Vector3.zero, out _));
			Assert.AreEqual(0, breach.HotCount);
		}

		[Test]
		public void 가까운_자리끼리는_한_곳으로_합쳐진다()
		{
			// 한 구역이 무너지는 것과 사방에서 하나씩 잃는 것은 다른 사건이다 — 같이 세면 구별이 사라진다.
			TowerDefenseBreach breach = new();

			breach.Add(new Vector3(10f, 0f, 0f), mergeDistance: 6f, heatPerLoss: 1f);
			breach.Add(new Vector3(13f, 0f, 0f), mergeDistance: 6f, heatPerLoss: 1f);

			Assert.AreEqual(1, breach.SiteCount);
			Assert.AreEqual(2f, breach.SiteAt(0).Heat, 0.001f);
		}

		[Test]
		public void 먼_자리는_따로_센다()
		{
			TowerDefenseBreach breach = new();

			breach.Add(new Vector3(10f, 0f, 0f), mergeDistance: 6f, heatPerLoss: 1f);
			breach.Add(new Vector3(-40f, 0f, 0f), mergeDistance: 6f, heatPerLoss: 1f);

			Assert.AreEqual(2, breach.SiteCount);
		}

		[Test]
		public void 처음_뜨거워질_때만_알린다()
		{
			// 잃을 때마다 외치면 정작 급한 알림을 덮고, 한 번도 안 외치면 규칙이 안 보인다.
			TowerDefenseBreach breach = new();

			bool first = breach.Add(Vector3.right * 10f, mergeDistance: 6f, heatPerLoss: 1f);
			bool second = breach.Add(Vector3.right * 11f, mergeDistance: 6f, heatPerLoss: 1f);
			bool third = breach.Add(Vector3.right * 12f, mergeDistance: 6f, heatPerLoss: 1f);

			Assert.IsTrue(first, "처음 뜨거워진 순간은 알려야 한다.");
			Assert.IsFalse(second);
			Assert.IsFalse(third, "같은 자리를 또 잃었다고 매번 외치면 알림 칸이 이걸로 꽉 찬다.");
		}

		[Test]
		public void 아직_안_뜨거우면_안_알리고_넘어설_때_알린다()
		{
			// 열기가 문턱 아래인 동안은 「잊힐 자리」다 — 그걸 알리면 알림이 늑대소년이 된다.
			TowerDefenseBreach breach = new();

			bool below = breach.Add(Vector3.right * 10f, mergeDistance: 6f, heatPerLoss: 0.2f);
			bool crossing = breach.Add(Vector3.right * 10f, mergeDistance: 6f, heatPerLoss: 0.4f);

			Assert.IsFalse(below);
			Assert.IsTrue(crossing, "문턱을 넘는 그 순간이 알릴 때다.");
		}

		[Test]
		public void 다른_자리는_각각_한_번씩_알린다()
		{
			TowerDefenseBreach breach = new();

			Assert.IsTrue(breach.Add(new Vector3(30f, 0f, 0f), mergeDistance: 6f, heatPerLoss: 1f));
			Assert.IsTrue(breach.Add(new Vector3(-30f, 0f, 0f), mergeDistance: 6f, heatPerLoss: 1f),
				"멀리 떨어진 두 번째 구역이 뚫린 것은 첫 구역과 다른 사건이라 따로 알려야 한다.");
		}

		[Test]
		public void 열기는_상한을_안_넘는다()
		{
			// 한 곳을 열 번 잃었다고 영영 지옥이 되면 벌칙이지 규칙이 아니다.
			TowerDefenseBreach breach = new();

			for (int loss = 0; loss < 20; loss++)
				breach.Add(Vector3.right * 10f, mergeDistance: 6f, heatPerLoss: 1f);

			Assert.AreEqual(TowerDefenseBreach.MAX_HEAT, breach.SiteAt(0).Heat, 0.001f);
		}

		[Test]
		public void 시간이_지나면_식어서_사라진다()
		{
			TowerDefenseBreach breach = new();
			breach.Add(Vector3.right * 10f, mergeDistance: 6f, heatPerLoss: 1f);

			breach.Tick(deltaTime: 10f, coolPerSecond: 0.2f); // 2 만큼 식는다 = 다 식는다

			Assert.AreEqual(0, breach.SiteCount, "다 식은 자리가 목록에 남으면 목록이 무한히 자란다.");
			Assert.IsFalse(breach.TryGetBiasAngle(Vector3.zero, out _));
		}

		[Test]
		public void 뚫린_쪽을_가리킨다()
		{
			TowerDefenseBreach breach = new();
			breach.Add(new Vector3(0f, 0f, 25f), mergeDistance: 6f, heatPerLoss: 2f); // 북쪽

			Assert.IsTrue(breach.TryGetBiasAngle(Vector3.zero, out float north));
			Assert.AreEqual(0f, north, 0.5f, "북(+z) 은 0 도여야 파도 원점과 같은 셈법이 된다.");

			TowerDefenseBreach east = new();
			east.Add(new Vector3(25f, 0f, 0f), mergeDistance: 6f, heatPerLoss: 2f);

			Assert.IsTrue(east.TryGetBiasAngle(Vector3.zero, out float eastAngle));
			Assert.AreEqual(90f, eastAngle, 0.5f);
		}

		[Test]
		public void 사방이_뚫리면_방향이_없다()
		{
			// 서로 반대쪽이 똑같이 뜨거우면 치우칠 이유가 없다 — 억지로 한쪽을 고르면 거짓말이다.
			TowerDefenseBreach breach = new();
			breach.Add(new Vector3(0f, 0f, 25f), mergeDistance: 6f, heatPerLoss: 2f);
			breach.Add(new Vector3(0f, 0f, -25f), mergeDistance: 6f, heatPerLoss: 2f);

			Assert.IsFalse(breach.TryGetBiasAngle(Vector3.zero, out _));
		}

		[Test]
		public void 식어버린_자리는_방향에_안_낀다()
		{
			TowerDefenseBreach breach = new();
			breach.Add(new Vector3(0f, 0f, 25f), mergeDistance: 6f, heatPerLoss: 2f);   // 북 — 뜨겁다
			breach.Add(new Vector3(25f, 0f, 0f), mergeDistance: 6f, heatPerLoss: 0.2f); // 동 — 이미 식은 축

			Assert.IsTrue(breach.TryGetBiasAngle(Vector3.zero, out float angle));
			Assert.AreEqual(0f, angle, 0.5f, "식은 자리가 방향을 끌면 잊힌 실수가 계속 판을 흔든다.");
			Assert.AreEqual(1, breach.HotCount);
		}
	}
}

using NUnit.Framework;
using UnityEngine;
using WitchMendokusai;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 알림 목록 — 「알림이 알림을 가리지 않는다」를 못 박는다 (TASK-WM-194).
	///
	/// ★ 알림 기능의 실패 방식은 「안 뜬다」가 아니라 **너무 떠서 못 읽는다**이다. 한 곳이 무너지는
	///   동안 같은 알림이 스무 개 쌓이면, 정작 *다른 곳*에서 난 일이 그 뒤에 묻힌다.
	///   묶기·개수 제한·밀어내기 규칙 셋이 그걸 막는다 — 전부 화면 없이 잴 수 있다.
	/// </summary>
	public class TowerDefenseAlertsTests
	{
		[Test]
		public void 알림이_뜬다()
		{
			TowerDefenseAlerts alerts = new();
			alerts.Raise("깨어났다", Vector3.zero, now: 0f, lifetime: 5f);

			Assert.AreEqual(1, alerts.Active.Count);
			Assert.AreEqual("깨어났다", alerts.Active[0].Label);
		}

		[Test]
		public void 시간이_지나면_사라진다()
		{
			TowerDefenseAlerts alerts = new();
			alerts.Raise("깨어났다", Vector3.zero, now: 0f, lifetime: 5f);

			alerts.Prune(now: 4f);
			Assert.AreEqual(1, alerts.Active.Count, "아직 살아 있어야 한다.");

			alerts.Prune(now: 5f);
			Assert.AreEqual(0, alerts.Active.Count, "안 사라지면 옛일이 화면에 영영 붙어 있다.");
		}

		[Test]
		public void 같은_자리_같은_일은_하나로_묶인다()
		{
			// 한 곳이 무너지는 동안 알림이 도배되면 다른 곳에서 난 일이 안 보인다.
			TowerDefenseAlerts alerts = new();
			for (int step = 0; step < 20; step++)
				alerts.Raise("부서졌다", new Vector3(step * 0.1f, 0f, 0f), now: step, lifetime: 5f);

			Assert.AreEqual(1, alerts.Active.Count, "같은 사건이 스무 개로 쌓이면 알림이 알림을 가린다.");
		}

		[Test]
		public void 묶이면_시간이_늘어난다()
		{
			TowerDefenseAlerts alerts = new();
			alerts.Raise("부서졌다", Vector3.zero, now: 0f, lifetime: 5f);
			alerts.Raise("부서졌다", Vector3.zero, now: 4f, lifetime: 5f);

			alerts.Prune(now: 6f);
			Assert.AreEqual(1, alerts.Active.Count, "계속 나는 일인데 처음 시간에 맞춰 사라지면 안 된다.");
		}

		[Test]
		public void 멀리_떨어진_같은_일은_따로_뜬다()
		{
			// 두 곳이 동시에 무너지는 것과 한 곳이 무너지는 것은 다른 상황이다.
			TowerDefenseAlerts alerts = new();
			alerts.Raise("부서졌다", Vector3.zero, now: 0f, lifetime: 5f);
			alerts.Raise("부서졌다", new Vector3(50f, 0f, 0f), now: 0f, lifetime: 5f);

			Assert.AreEqual(2, alerts.Active.Count);
		}

		[Test]
		public void 너무_많으면_먼저_사라질_것부터_버린다()
		{
			TowerDefenseAlerts alerts = new();
			for (int index = 0; index < TowerDefenseAlerts.MAX_ALERTS + 3; index++)
				alerts.Raise("일 " + index, new Vector3(index * 100f, 0f, 0f), now: index, lifetime: 5f);

			Assert.AreEqual(TowerDefenseAlerts.MAX_ALERTS, alerts.Active.Count,
				"개수 제한이 없으면 화면이 표식으로 덮인다.");

			// 새 사건이 살아남아야 한다 — 옛 사건에 밀려 안 뜨면 알림이 무의미하다.
			bool hasNewest = false;
			foreach (TowerDefenseAlerts.Alert alert in alerts.Active)
				hasNewest |= alert.Label == "일 " + (TowerDefenseAlerts.MAX_ALERTS + 2);

			Assert.IsTrue(hasNewest, "가장 최근에 난 일이 밀려나면 안 된다.");
		}

		[Test]
		public void 단계는_기준선부터_센다()
		{
			// 「이만큼 오를 때마다 한 번」 — 경계값에서 한 칸씩 정확히 올라야 알림이 겹치거나 빠지지 않는다.
			Assert.AreEqual(0, TowerDefenseAlerts.StepFor(1.00f, 1f, 0.5f), "판 시작은 0단계다.");
			Assert.AreEqual(0, TowerDefenseAlerts.StepFor(1.49f, 1f, 0.5f), "아직 한 칸 못 올랐다.");
			Assert.AreEqual(1, TowerDefenseAlerts.StepFor(1.50f, 1f, 0.5f), "경계에 닿으면 올라간다.");
			Assert.AreEqual(2, TowerDefenseAlerts.StepFor(2.00f, 1f, 0.5f));
			Assert.AreEqual(-1, TowerDefenseAlerts.StepFor(9.99f, 1f, 0f), "0 이면 아예 안 알린다.");
		}

		[Test]
		public void 빈_말은_안_뜬다()
		{
			TowerDefenseAlerts alerts = new();
			alerts.Raise(string.Empty, Vector3.zero, now: 0f, lifetime: 5f);
			alerts.Raise("깨어났다", Vector3.zero, now: 0f, lifetime: 0f);

			Assert.AreEqual(0, alerts.Active.Count, "말이 없거나 시간이 0 인 알림은 자리만 차지한다.");
		}
	}
}

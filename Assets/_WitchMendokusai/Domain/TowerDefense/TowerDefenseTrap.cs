using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 함정(TASK-WM-194) — 바닥에 까는 것. 포탑이 「어디를 쏘나」라면 함정은 **「어디를 지나가나」**라,
	/// 길목과 직결되고 벽(길 그리기)의 자연스러운 짝이 된다.
	///
	/// ★ 소모품인 게 핵심: 횟수를 다 쓰면 사라진다. 영구 설치면 「좋은 자리에 한 번 깔고 끝」이지만,
	///   닳으면 *이번 웨이브의 길목이 어디인가*를 매번 다시 판단하게 된다.
	/// ★ 시야와 무관하게 작동한다 — 밟는 것은 보이든 안 보이든 밟는 것이다(포탑의 조준과는 다른 층).
	/// </summary>
	public sealed class TowerDefenseTrap : MonoBehaviour
	{
		private IReadOnlyList<ICombatant> enemyPool;
		private System.Action<TowerDefenseTrap> onSpent;
		private int damage;
		private int chargesLeft;
		private float radiusSqr;
		private float armDelayRemaining;

		public void Configure(
			IReadOnlyList<ICombatant> enemies,
			int trapDamage,
			int charges,
			float radius,
			System.Action<TowerDefenseTrap> spentCallback)
		{
			enemyPool = enemies;
			damage = trapDamage;
			chargesLeft = charges;
			radiusSqr = radius * radius;
			onSpent = spentCallback;
			// 깔자마자 이미 그 자리에 서 있던 마수에게 터지면 「깔았더니 즉발」이라 배치 감각이 무너진다.
			armDelayRemaining = 0.25f;
		}

		public int ChargesLeft => chargesLeft;

		private void Update()
		{
			if (enemyPool == null || chargesLeft <= 0)
				return;

			if (armDelayRemaining > 0f)
			{
				armDelayRemaining -= Time.deltaTime;
				return;
			}

			Vector3 position = transform.position;
			bool triggered = false;

			foreach (ICombatant candidate in enemyPool)
			{
				if (candidate == null || candidate.IsAlive == false)
					continue;
				if ((candidate.Position - position).sqrMagnitude > radiusSqr)
					continue;

				ArenaCombatant combatant = candidate as ArenaCombatant;
				if (combatant == null || combatant.UnitObject == null)
					continue;

				combatant.UnitObject.Health.ReceiveDamage(new DamageInfo
				{
					damage = damage,
					type = DamageType.Normal,
					equipmentDataId = DamageInfo.NO_DATA_ID,
					skillDataId = DamageInfo.NO_DATA_ID,
				});
				triggered = true;
			}

			if (triggered == false)
				return;

			chargesLeft--;
			// 한 번 터지면 잠깐 쉰다 — 안 그러면 한 프레임에 전량 소진돼 「6회」가 사실상 1회가 된다.
			armDelayRemaining = 0.4f;

			if (chargesLeft <= 0)
				onSpent?.Invoke(this);
		}
	}
}

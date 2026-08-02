using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 둔화(TASK-WM-194) — 맞은 마수의 발을 묶는다. 둔화 포탑이 「빠른 마수」의 대답이 되는 지점.
	///
	/// ★ 원래 속도를 *처음 한 번만* 기억한다: 둔화가 겹칠 때마다 현재 속도를 원본으로 착각하면
	///   해제될 때 점점 빨라지거나 영영 느린 채로 굳는다(누적 버그의 전형).
	/// ★ 풀에서 재사용되는 몸이므로 꺼질 때 반드시 원속도로 되돌린다 — 안 그러면 다음 판의
	///   멀쩡한 마수가 이유 없이 느리다.
	/// </summary>
	public sealed class TowerDefenseSlow : MonoBehaviour
	{
		private UnitObject unitObject;
		private int originalSpeed = -1;
		private float remaining;

		/// <summary> 지금 둔화가 걸려 있나 — 다른 포탑의 「조합 보너스」 판정 기준. </summary>
		public bool IsActive => originalSpeed >= 0 && remaining > 0f;

		/// <summary> 둔화를 건다(이미 걸려 있으면 더 강한 쪽/더 긴 쪽으로 갱신). </summary>
		public static void Apply(UnitObject target, float slowFactor, float seconds)
		{
			if (target == null || slowFactor <= 0f || seconds <= 0f)
				return;

			TowerDefenseSlow slow = target.GetComponent<TowerDefenseSlow>();
			if (slow == null)
				slow = target.gameObject.AddComponent<TowerDefenseSlow>();

			slow.Engage(target, slowFactor, seconds);
		}

		private void Engage(UnitObject target, float slowFactor, float seconds)
		{
			unitObject = target;

			if (originalSpeed < 0)
				originalSpeed = unitObject.UnitStat[UnitStatType.MOVEMENT_SPEED];

			int slowedSpeed = Mathf.Max(1, Mathf.RoundToInt(originalSpeed * (1f - slowFactor)));
			if (slowedSpeed < unitObject.UnitStat[UnitStatType.MOVEMENT_SPEED])
				unitObject.UnitStat[UnitStatType.MOVEMENT_SPEED] = slowedSpeed;

			if (seconds > remaining)
				remaining = seconds;
		}

		private void Update()
		{
			if (originalSpeed < 0 || unitObject == null)
				return;

			remaining -= Time.deltaTime;
			if (remaining > 0f)
				return;

			Restore();
		}

		private void OnDisable()
		{
			Restore();
		}

		private void Restore()
		{
			if (originalSpeed >= 0 && unitObject != null)
				unitObject.UnitStat[UnitStatType.MOVEMENT_SPEED] = originalSpeed;

			originalSpeed = -1;
			remaining = 0f;
		}
	}
}

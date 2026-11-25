using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 둔화(TASK-WM-194) — 맞은 마수의 발을 묶는다. 둔화 포탑이 「빠른 마수」의 대답이 되는 지점.
	///
	/// - 이동 속도 스탯은 안 바꾸고 남는 비율 (UnitObject.SlowMultiplier) 만 정함. 이동 강화분까지 같이 줄고, 걸린 동안 스탯이 바뀌어도 잃지 않음
	/// - 세기마다 남은 시간을 따로 셈. 약한 둔화의 갱신이 강한 둔화를 늘리면 웅덩이 위에서 묶인 유닛이 안 풀림
	/// - 풀에서 재사용되는 몸이라 꺼질 때 반드시 되돌림
	/// </summary>
	public sealed class UnitSlow : MonoBehaviour
	{
		// 둔화 하나. 세기 (이동 속도를 줄이는 비율) 와 남은 초
		private struct Slow
		{
			public float Factor;
			public float Remaining;
		}

		private readonly List<Slow> slows = new();
		private UnitObject unitObject;

		/// <summary> 지금 둔화가 걸려 있나 — 다른 포탑의 「조합 보너스」 판정 기준. </summary>
		public bool IsActive => slows.Count > 0;

		/// <summary> 둔화를 건다. 걸린 것 중 가장 강한 세기가 이기고, 같은 세기는 더 긴 쪽으로 갱신. </summary>
		public static void Apply(UnitObject target, float slowFactor, float seconds)
		{
			if (target == null || slowFactor <= 0f || seconds <= 0f)
				return;

			UnitSlow slow = target.GetComponent<UnitSlow>();
			if (slow == null)
				slow = target.gameObject.AddComponent<UnitSlow>();

			slow.Engage(target, slowFactor, seconds);
		}

		private void Engage(UnitObject target, float slowFactor, float seconds)
		{
			unitObject = target;

			int index = slows.FindIndex(slow => Mathf.Approximately(slow.Factor, slowFactor));
			if (index < 0)
				slows.Add(new Slow { Factor = slowFactor, Remaining = seconds });
			else
				slows[index] = new Slow { Factor = slowFactor, Remaining = Mathf.Max(slows[index].Remaining, seconds) };
			ApplyStrongest();
		}

		private void Update()
		{
			if (slows.Count == 0)
				return;

			bool expired = false;
			for (int i = slows.Count - 1; i >= 0; i--)
			{
				Slow slow = slows[i];
				slow.Remaining -= Time.deltaTime;
				slows[i] = slow;
				if (slow.Remaining > 0f)
					continue;
				slows.RemoveAt(i);
				expired = true;
			}
			if (slows.Count == 0)
				Restore();
			else if (expired)
				ApplyStrongest();
		}

		private void ApplyStrongest()
		{
			float strongest = 0f;
			foreach (Slow slow in slows)
				strongest = Mathf.Max(strongest, slow.Factor);
			unitObject.SlowMultiplier = 1f - strongest;
		}

		private void OnDisable()
		{
			Restore();
		}

		private void Restore()
		{
			if (unitObject != null)
				unitObject.SlowMultiplier = 1f;
			slows.Clear();
		}
	}
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>후보 중 하나를 매번 고르는 묶음. 무게가 클수록 자주. 묶음 안 묶음은 넣을 수 없음</summary>
	[CreateAssetMenu(fileName = "IdleFx_0_Mix", menuName = "WM/UI/Idle Effect/Mix")]
	public sealed class MixIdleEffect : UIIdleEffect
	{
		[Serializable]
		public struct Entry
		{
			public UIIdleEffect effect;
			[Min(0f)] public float weight;
		}

		public Entry[] pool = new Entry[0];

		public override UIIdleEffect Pick()
		{
			float total = 0f;
			foreach (Entry entry in pool)
			{
				if (entry.effect == null || entry.effect is MixIdleEffect)
					throw new InvalidOperationException($"{name}: 후보에 빈 칸이나 묶음이 있음");
				total += entry.weight;
			}
			if (total <= 0f)
				throw new InvalidOperationException($"{name}: 후보가 없거나 무게 합이 0");
			float roll = UnityEngine.Random.value * total;
			foreach (Entry entry in pool)
			{
				if (entry.weight <= 0f)
					continue;
				if (roll < entry.weight)
					return entry.effect.Pick();
				roll -= entry.weight;
			}
			// 부동소수 오차로 끝까지 간 경우 마지막 유효 후보
			for (int i = pool.Length - 1; i >= 0; i--)
			{
				if (pool[i].weight > 0f)
					return pool[i].effect.Pick();
			}
			throw new InvalidOperationException($"{name}: 고를 후보 없음");
		}

		public override int Play(VisualElement overlay, VisualElement target, List<VisualElement> targets, Action onDone) =>
			throw new InvalidOperationException($"{name}: 묶음은 Pick 으로 고른 뒤 그림");
	}
}

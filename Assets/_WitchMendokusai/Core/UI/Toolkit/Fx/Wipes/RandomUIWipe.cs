using System;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>후보 중 하나를 매번 고르는 묶음. 같은 에셋을 여러 번 넣으면 더 자주. 묶음 안 묶음은 넣을 수 없음</summary>
	[CreateAssetMenu(fileName = "Wipe_0_Random", menuName = "WM/UI/Wipe/Random")]
	public sealed class RandomUIWipe : UIWipe
	{
		public SingleUIWipe[] pool = new SingleUIWipe[0];

		public override SingleUIWipe Pick()
		{
			if (pool == null || pool.Length == 0)
				throw new InvalidOperationException($"{name}: 후보가 비어 있음");
			SingleUIWipe picked = pool[UnityEngine.Random.Range(0, pool.Length)];
			if (picked == null)
				throw new InvalidOperationException($"{name}: 후보에 빈 칸이 있음");
			return picked;
		}
	}
}

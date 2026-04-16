using System;
using System.Collections;
using UnityEngine;

namespace WitchMendokusai
{
	public struct FishingContext
	{
		public Transform Fisherman;
		public FishingSpotData Data;
	}

	public interface IFishingMiniGame
	{
		/// <summary>
		/// 미니게임을 실행한다. onResult(true) = 낚시 성공, onResult(false) = 실패.
		/// </summary>
		IEnumerator Play(FishingContext context, Action<bool> onResult);
	}
}

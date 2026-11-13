using DG.Tweening;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// DOTween 동시 트윈 칸을 첫 씬 전에 확보.
	/// 기본 200 칸은 생존 판 중반에 넘쳐 DOTween 이 판 도중 500 으로 다시 할당함 (2026-11-13 MOIS2 10분 판 실측, 경고 1회).
	/// 판 도중 재할당 대신 시작 때 한 번.
	/// </summary>
	public static class TweenCapacityBootstrap
	{
		private const int TWEENER_CAPACITY = 500;
		private const int SEQUENCE_CAPACITY = 50;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void Apply() => DOTween.SetTweensCapacity(TWEENER_CAPACITY, SEQUENCE_CAPACITY);
	}
}

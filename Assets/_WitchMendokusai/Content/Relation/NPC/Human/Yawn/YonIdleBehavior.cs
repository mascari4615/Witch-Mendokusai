using System.Collections;
using UnityEngine;

namespace WitchMendokusai
{
	public class YonIdleBehavior : MonoBehaviour
	{
		[SerializeField] private float minIdleSeconds = 30f;
		[SerializeField] private float maxIdleSeconds = 120f;

		public YonIdleState CurrentState { get; private set; } = YonIdleState.Zoning;

		private void OnEnable() => StartCoroutine(IdleRoutine());
		private void OnDisable() => StopAllCoroutines();

		private IEnumerator IdleRoutine()
		{
			while (true)
			{
				yield return new WaitForSeconds(Random.Range(minIdleSeconds, maxIdleSeconds));
				CurrentState = (YonIdleState)Random.Range(0, System.Enum.GetValues(typeof(YonIdleState)).Length);
			}
		}
	}
}

using System.Collections;
using UnityEngine;

namespace WitchMendokusai
{
	public class IdleStateBehavior : MonoBehaviour
	{
		[SerializeField] private int stateCount = 4;
		[SerializeField] private float minIdleSeconds = 30f;
		[SerializeField] private float maxIdleSeconds = 120f;

		public int CurrentStateIndex { get; private set; }

		private void OnEnable() => StartCoroutine(IdleRoutine());
		private void OnDisable() => StopAllCoroutines();

		private IEnumerator IdleRoutine()
		{
			while (true)
			{
				yield return new WaitForSeconds(Random.Range(minIdleSeconds, maxIdleSeconds));
				CurrentStateIndex = Random.Range(0, stateCount);
			}
		}
	}
}

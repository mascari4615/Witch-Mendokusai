using System.Collections;
using UnityEngine;

namespace WitchMendokusai
{
	public abstract class IdleStateBehavior : MonoBehaviour
	{
		[SerializeField] private float minIdleSeconds = 30f;
		[SerializeField] private float maxIdleSeconds = 120f;

		public int CurrentStateIndex { get; private set; }

		protected abstract int StateCount { get; }

		private void OnEnable() => StartCoroutine(IdleRoutine());
		private void OnDisable() => StopAllCoroutines();

		private IEnumerator IdleRoutine()
		{
			while (true)
			{
				yield return new WaitForSeconds(Random.Range(minIdleSeconds, maxIdleSeconds));
				CurrentStateIndex = Random.Range(0, StateCount);
				OnStateChanged(CurrentStateIndex);
			}
		}

		protected virtual void OnStateChanged(int stateIndex) { }
	}
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	public class UITransition : MonoBehaviour
	{
		private CanvasGroup canvasGroup;
		private Animator[] transitionAnimators;

		private void Awake()
		{
			canvasGroup = GetComponent<CanvasGroup>();
			transitionAnimators = GetComponentsInChildren<Animator>(true);
		}

		private void Start()
		{
			canvasGroup.alpha = 1;
		}

		// 함수를 전달받아 처리
		public void Transition(Action aWhenStart = null, Action aDuringTransition = null, Action aWhenEnd = null)
		{
			// Debug.Log(nameof(Transition) + " " + actionDuringTransition);
			Transition(aWhenStart, ActionToCoroutine(aDuringTransition), aWhenEnd);
		}

		public void Transition(Action aWhenStart = null, IEnumerator cDuringTransition = null, Action aWhenEnd = null)
		{
			// Debug.Log(nameof(Transition) + " " + corountineDuringTransition);
			StartCoroutine(TransitionCoroutine(aWhenStart, cDuringTransition, aWhenEnd));
		}

		private IEnumerator TransitionCoroutine(Action aWhenStart = null, IEnumerator cDuringTransition = null, Action aWhenEnd = null)
		{
			// Debug.Log(nameof(TransitionCoroutine));
			
			// HACK:
			Animator transitionAnimator = transitionAnimators[Random.Range(0, transitionAnimators.Length)];

			// Start
			aWhenStart?.Invoke();
			TimeManager.Instance.Pause();
			canvasGroup.blocksRaycasts = true;
			transitionAnimator.SetTrigger("OUT");

			// During
			AnimatorStateInfo animatorStateInfo = transitionAnimator.GetCurrentAnimatorStateInfo(0);
			float duration = animatorStateInfo.length / animatorStateInfo.speedMultiplier;

			yield return new WaitForSecondsRealtime(duration + .2f);
			if (cDuringTransition != null)
				yield return StartCoroutine(cDuringTransition);
			yield return new WaitForSecondsRealtime(.2f);

			// End
			transitionAnimator.SetTrigger("IN");
			canvasGroup.blocksRaycasts = false;
			TimeManager.Instance.Resume();
			aWhenEnd?.Invoke();
		}

			private static IEnumerator ActionToCoroutine(Action action)
			{
				action();
				yield return null;
			}
	}
}
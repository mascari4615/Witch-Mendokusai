using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// https://twitter.com/BinaryImpactG/status/1686306273061482496
	// Mathf.Epsilon
	public class TimeManager : Singleton<TimeManager>
	{
		private bool isPaused;
		public bool IsPaused
		{
			get => isPaused;
			set
			{
				isPaused = value;
				UpdateTimeScale();
			}
		}

		public const float TICK = 0.05f;

		public float slowFactor = 0.05f;
		public float slowTime = .5f;
		public float returnSpeed = 4f;

		private Action callback;
		private Coroutine timeLoop;

		private void OnEnable()
		{
			RegisterCallback(UpdateTimeScale);
			timeLoop = StartCoroutine(UpdateTime());
		}

		private IEnumerator UpdateTime()
		{
			WaitForSeconds wait = new(TICK);

			while (true)
			{
				GameEventManager.Instance.Raise(GameEventType.OnTick);
				callback?.Invoke();
				yield return wait;
			}
		}

		public void RegisterCallback(Action callback)
		{
			if (this.callback != null)
			{
				foreach (Delegate existingCallback in this.callback.GetInvocationList())
					if (existingCallback.Equals(callback))
						return; // 이미 등록된 이벤트는 추가하지 않습니다.
			}

			this.callback += callback;
		}

		public void RemoveCallback(Action callback)
		{
			this.callback -= callback;
		}

		public void UpdateTimeScale()
		{
			Time.timeScale = IsPaused ? Mathf.Epsilon : 1;
		}

		public void Pause()
		{
			IsPaused = true;
		}

		public void Resume()
		{
			IsPaused = false;
		}

		[ContextMenu(nameof(DoSlowMotion))]
		public void DoSlowMotion()
		{
			if (slowMotion != null)
				StopCoroutine(slowMotion);
			slowMotion = StartCoroutine(SlowMotion());
		}

		private Coroutine slowMotion;

		// Timescale does not affect coroutines
		private IEnumerator SlowMotion()
		{
			yield return new WaitForSecondsRealtime(.05f);

			Time.timeScale = slowFactor;
			// Time.fixedDeltaTime = Time.timeScale * 0.02f;


			// Timescale affects WaitForSeconds
			// yield return new WaitForSeconds(slowTime);
			yield return new WaitForSecondsRealtime(slowTime);

			while (true)
			{
				Time.timeScale += Time.unscaledDeltaTime * returnSpeed;
				if (Time.timeScale > 1)
				{
					Time.timeScale = Mathf.Clamp(Time.timeScale, 0f, 1f);
					break;
				}

				yield return null;
			}
		}
	}
}
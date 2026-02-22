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
		public const float TICK = 0.05f;

		[SerializeField] private float slowFactor = 0.05f;
		[SerializeField] private float slowTime = .5f;
		[SerializeField] private float returnSpeed = 4f;

		private readonly List<GameObject> pausers = new();
		private Action callback;
		private Coroutine timeLoop;
		private Coroutine slowMotion;

		public bool IsPaused => pausers.Count > 0;

		private void OnEnable()
		{
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

		private void UpdateTimeScale()
		{
			Time.timeScale = IsPaused ? Mathf.Epsilon : 1;
		}

		public void Pause(GameObject pauser)
		{
			if (pauser == null)
				return;

			if (pausers.Contains(pauser))
				return; // 이미 등록된 pauser는 추가하지 않습니다.

			Debug.Log($"[TimeManager] Paused by {pauser.name}");

			pausers.Add(pauser);
			UpdateTimeScale();
		}

		public void Resume(GameObject pauser)
		{
			if (pauser == null)
				return;

			if (pausers.Contains(pauser) == false)
				return; // 등록되지 않은 pauser는 무시합니다.

			Debug.Log($"[TimeManager] Resumed by {pauser.name}");	

			pausers.Remove(pauser);
			UpdateTimeScale();
		}

		[ContextMenu(nameof(DoSlowMotion))]
		public void DoSlowMotion()
		{
			if (slowMotion != null)
				StopCoroutine(slowMotion);
			slowMotion = StartCoroutine(SlowMotion());
		}

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
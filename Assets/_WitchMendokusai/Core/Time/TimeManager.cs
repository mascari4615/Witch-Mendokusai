using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	// https://twitter.com/BinaryImpactG/status/1686306273061482496
	// Mathf.Epsilon
	public class TimeManager : MonoBehaviour
	{
		public static TimeManager Instance { get; private set; }

		public static bool TryGetExistingInstance(out TimeManager mgr)
		{
			mgr = Instance;
			return mgr != null;
		}

		private GameEventManager gameEventManager;

		[Inject]
		public void Construct(GameEventManager gameEventManager)
		{
			this.gameEventManager = gameEventManager;
		}

		public const float TICK = 0.05f;

		[SerializeField] private float slowFactor = 0.05f;
		[SerializeField] private float slowTime = .5f;
		[SerializeField] private float returnSpeed = 4f;

		private readonly List<GameObject> pausers = new();
		private Action callback;
		private Coroutine timeLoop;
		private Coroutine slowMotion;
		private bool isSlowMotion;
		private Coroutine hitStop;
		private float hitStopEnd;
		private float hitStopScale = 1f;

		public bool IsPaused => pausers.Count > 0;

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}
			Instance = this;
		}

		private void OnDestroy()
		{
			if (Instance == this)
				Instance = null;
		}

		private void OnEnable()
		{
			timeLoop = StartCoroutine(UpdateTime());
		}

		private IEnumerator UpdateTime()
		{
			WaitForSeconds wait = new(TICK);

			while (true)
			{
				gameEventManager.Raise(GameEventType.OnTick);
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
			Time.timeScale = IsPaused ? Mathf.Epsilon : hitStop != null ? hitStopScale : 1;
		}

		// 전역 짧은 정지. 정지 중 더 오면 길이는 늘리고 배율은 더 낮은 쪽. 사망 슬로모션 중에는 무시
		public void HitStop(float duration, float scale)
		{
			if (isSlowMotion || duration <= 0f)
				return;
			hitStopEnd = Mathf.Max(hitStopEnd, Time.unscaledTime + duration);
			hitStopScale = hitStop != null ? Mathf.Min(hitStopScale, scale) : scale;
			if (hitStop == null)
				hitStop = StartCoroutine(HitStopRoutine());
			UpdateTimeScale();
		}

		private IEnumerator HitStopRoutine()
		{
			while (Time.unscaledTime < hitStopEnd)
				yield return null;
			hitStop = null;
			UpdateTimeScale();
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
			if (hitStop != null)
			{
				StopCoroutine(hitStop);
				hitStop = null;
			}
			isSlowMotion = true;
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
					isSlowMotion = false;
					break;
				}

				yield return null;
			}
		}
	}
}

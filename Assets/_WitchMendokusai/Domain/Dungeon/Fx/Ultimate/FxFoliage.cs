using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 충격파에 휘어지는 풀과 나무 한 그루. 충격파가 닿는 시각부터 바깥으로 기울었다가 감쇠 진동으로 돌아옴
	/// - 기울기: 중심에서 바깥쪽으로 위쪽 끝을 밀어냄. 유연도 (풀 1, 나무 낮게) 와 거리 감쇠를 곱함
	/// - 궁극기가 부르는 정적 함수 React 한 번으로 등록된 전부가 반응. 반응이 끝나면 원래 자세로 복귀하고 멈춤
	/// - 시간은 게임 시간 (히트스톱 중에는 같이 멈춤)
	/// </summary>
	public sealed class FxFoliage : MonoBehaviour
	{
		private static readonly List<FxFoliage> active = new();

		public static IReadOnlyList<FxFoliage> All => active;

		[SerializeField, Range(0f, 1f), Tooltip("얼마나 잘 휘는가. 풀 1, 나무 0.25")] private float flexibility = 1f;

		private Quaternion restRotation;
		private Vector3 restScale;
		private bool reacting;
		private Vector3 center;
		private float startTime;
		private float speed;
		private float reach;
		private float bendDegrees;
		private float decaySeconds;
		private float oscillationHz;

		public float Flexibility
		{
			get => flexibility;
			set => flexibility = value;
		}

		private void OnEnable()
		{
			restRotation = transform.localRotation;
			restScale = transform.localScale;
			active.Add(this);
		}

		private void OnDisable() => active.Remove(this);

		public static int Count => active.Count;

		/// <summary> 중심에서 퍼지는 충격파 한 번. 등록된 전부에 반응 시작 시각을 매김 </summary>
		public static void React(Vector3 center, float speed, float reach, float bendDegrees, float decaySeconds, float oscillationHz)
		{
			foreach (FxFoliage foliage in active)
				foliage.StartReaction(center, speed, reach, bendDegrees, decaySeconds, oscillationHz);
		}

		private void StartReaction(Vector3 reactionCenter, float waveSpeed, float waveReach, float bend, float decay, float hertz)
		{
			center = reactionCenter;
			speed = waveSpeed;
			reach = waveReach;
			bendDegrees = bend;
			decaySeconds = decay;
			oscillationHz = hertz;
			startTime = Time.time;
			reacting = true;
		}

		private void LateUpdate()
		{
			if (reacting == false)
				return;
			Vector3 offset = transform.position - center;
			offset.y = 0f;
			float distance = offset.magnitude;
			float elapsed = Time.time - startTime - distance / Mathf.Max(0.01f, speed);
			if (elapsed < 0f)
				return;
			if (elapsed > decaySeconds * 4f)
			{
				transform.localRotation = restRotation;
				transform.localScale = restScale;
				reacting = false;
				return;
			}

			float falloff = Mathf.Clamp01(1f - distance / Mathf.Max(0.01f, reach));
			falloff = Mathf.Lerp(0.25f, 1f, falloff);
			float swing = Mathf.Sin(elapsed * oscillationHz * Mathf.PI * 2f) * Mathf.Exp(-elapsed / decaySeconds);
			// 처음에 바깥으로 크게, 이후 감쇠 진동
			float angle = bendDegrees * flexibility * falloff * swing;
			Vector3 away = distance > 0.001f ? offset / distance : Vector3.forward;
			Vector3 axis = Vector3.Cross(Vector3.up, away);
			transform.localRotation = Quaternion.AngleAxis(angle, axis) * restRotation;
			// 눌릴 때 살짝 납작해지고 되돌 때 늘어남
			float squash = 1f - 0.12f * flexibility * falloff * Mathf.Abs(swing);
			transform.localScale = new Vector3(restScale.x, restScale.y * squash, restScale.z);
		}
	}
}

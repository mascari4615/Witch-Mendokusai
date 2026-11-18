using System;
using System.Collections;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 세기가 0 에서 1 로 들어왔다가 머물고 다시 0 으로 나가는 시간표. 화면 색, 카메라 당김이 같이 씀
	/// - 실제 시간 (전역 정지와 사망 슬로모션 중에도 같은 속도)
	/// - 도중에 멈추면 그 순간의 세기에서 나가는 시간에 걸쳐 걷음 (뚝 끊기지 않게)
	/// </summary>
	[Serializable]
	public sealed class FlowEnvelope
	{
		[SerializeField, Min(0f), Tooltip("0 에서 최대까지 들어오는 시간 (초)")] private float inSeconds = 0.1f;
		[SerializeField, Min(0f), Tooltip("최대에서 머무는 시간 (초)")] private float holdSeconds;
		[SerializeField, Tooltip("켜면 멈춤 함수가 불릴 때까지 머묾 (결과창이 뜰 때 걷는 물들임)")] private bool holdUntilStopped;
		[SerializeField, Min(0f), Tooltip("최대에서 0 까지 나가는 시간 (초). 도중에 멈출 때도 이 시간")] private float outSeconds = 0.3f;
		[SerializeField, Tooltip("들어오고 나가는 곡선. 가로 진행 0~1, 세로 세기 0~1")] private AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

		public float InSeconds => inSeconds;
		public float HoldSeconds => holdUntilStopped ? float.PositiveInfinity : holdSeconds;
		public float OutSeconds => outSeconds;
		public AnimationCurve Curve => curve;

		/// <summary> 재생. apply 는 매 프레임 세기 (0~1), onEnd 는 다 나간 뒤 한 번. 돌려받은 함수를 부르면 지금 세기에서 걷음 </summary>
		public Action Play(CombatFxRunner runner, Action<float> apply, Action onEnd)
		{
			float weight = 0f;
			bool ended = false;
			bool releasing = false;

			void End()
			{
				if (ended)
					return;
				ended = true;
				onEnd();
			}

			IEnumerator Ramp(float from, float to, float seconds)
			{
				float elapsed = 0f;
				while (elapsed < seconds)
				{
					weight = Mathf.LerpUnclamped(from, to, curve.Evaluate(elapsed / seconds));
					apply(weight);
					yield return null;
					elapsed += Time.unscaledDeltaTime;
				}
				weight = to;
				apply(weight);
			}

			IEnumerator Main()
			{
				yield return Ramp(0f, 1f, inSeconds);
				while (holdUntilStopped)
					yield return null;
				if (holdSeconds > 0f)
					yield return new WaitForSecondsRealtime(holdSeconds);
				yield return Ramp(1f, 0f, outSeconds);
				End();
			}

			IEnumerator Release()
			{
				yield return Ramp(weight, 0f, outSeconds);
				End();
			}

			Action stopMain = runner.Run(Main());
			return () =>
			{
				if (ended || releasing)
					return;
				releasing = true;
				stopMain();
				// 씬이 내려가 실행기가 사라진 뒤면 걷는 연출 없이 바로 치움
				if (runner == null)
				{
					End();
					return;
				}
				runner.Run(Release());
			};
		}
	}
}

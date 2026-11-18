using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[Serializable]
	public sealed class SequenceFxStep
	{
		[Tooltip("이 순서에 재생할 부품. 비우면 건너뜀")] public CombatFx Fx;
		[Min(0f), Tooltip("묶음 시작부터 이 부품이 나올 때까지 실제 시간 (초)")] public float DelaySeconds;
		[Tooltip("띠에 쓸 글 덮어쓰기. 비우면 부른 쪽이 넘긴 글")] public string Title;
		[Tooltip("띠 아랫줄 덮어쓰기. 비우면 부른 쪽이 넘긴 글")] public string Subtitle;
	}

	/// <summary>
	/// 여러 부품을 시간차로 재생하는 묶음 (승리: 번쩍임, 카메라 당김, 띠)
	/// - 항목은 위에서부터 차례로, 각 항목의 지연은 묶음 시작 기준. 실제 시간
	/// - 멈춤 함수를 부르면 아직 안 나온 항목은 취소, 나온 항목은 각자의 멈춤
	/// </summary>
	[CreateAssetMenu(fileName = "Sequence_0_New", menuName = "WM/CombatFx/" + nameof(SequenceFx))]
	public class SequenceFx : CombatFx
	{
		[SerializeField, Tooltip("재생할 부품과 시간차. 지연이 짧은 것부터 적음")] private List<SequenceFxStep> steps = new();

		protected override Action Play(CombatFxRunner runner, CombatFxContext context)
		{
			List<Action> stops = new();
			Action stopRoutine = runner.Run(PlaySteps(context, stops));
			return () =>
			{
				stopRoutine();
				foreach (Action stop in stops)
					stop();
				stops.Clear();
			};
		}

		private IEnumerator PlaySteps(CombatFxContext context, List<Action> stops)
		{
			float elapsed = 0f;
			foreach (SequenceFxStep step in steps)
			{
				while (elapsed < step.DelaySeconds)
				{
					yield return null;
					elapsed += Time.unscaledDeltaTime;
				}

				if (step.Fx == null || step.Fx == this)
					continue;

				CombatFxContext stepContext = context;
				if (string.IsNullOrEmpty(step.Title) == false)
					stepContext.Title = step.Title;
				if (string.IsNullOrEmpty(step.Subtitle) == false)
					stepContext.Subtitle = step.Subtitle;
				stops.Add(step.Fx.Play(stepContext));
			}
		}
	}
}

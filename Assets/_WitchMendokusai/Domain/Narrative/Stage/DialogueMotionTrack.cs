using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 움직임 키. 시간은 0~1 비율, 위치는 패널 픽셀
	[Serializable]
	public struct DialogueMotionKey
	{
		public float Time;
		public float X;
		public float Y;
		public float Scale;

		public DialogueMotionKey(float time, float x, float y, float scale = 1f)
		{
			Time = time;
			X = x;
			Y = y;
			Scale = scale;
		}
	}

	public static class DialogueMotionSampler
	{
		public static DialogueMotionKey Sample(IReadOnlyList<DialogueMotionKey> keys, float normalizedTime)
		{
			if (keys == null || keys.Count == 0)
			{
				return new DialogueMotionKey(normalizedTime, 0f, 0f);
			}
			if (normalizedTime <= keys[0].Time)
			{
				return keys[0];
			}
			for (int i = 1; i < keys.Count; i++)
			{
				if (normalizedTime > keys[i].Time)
				{
					continue;
				}
				DialogueMotionKey from = keys[i - 1];
				DialogueMotionKey to = keys[i];
				float span = to.Time - from.Time;
				float blend = span <= 0f ? 1f : (normalizedTime - from.Time) / span;
				return new DialogueMotionKey(
					normalizedTime,
					from.X + (to.X - from.X) * blend,
					from.Y + (to.Y - from.Y) * blend,
					from.Scale + (to.Scale - from.Scale) * blend);
			}
			return keys[keys.Count - 1];
		}
	}
}

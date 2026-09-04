#if UNITY_EDITOR
using System;
using UnityEngine;

namespace WitchMendokusai.Idle
{
	/// <summary>에디트 모드 전투 화면의 시계와 다시 그리기</summary>
	internal sealed class BattlePreviewDriver : IDisposable
	{
		private readonly Action<float> tick;
		private readonly Func<bool> isRunning;
		private double clock;
		private bool ticked;

		public BattlePreviewDriver(Action<float> tick, Func<bool> isRunning)
		{
			this.tick = tick;
			this.isRunning = isRunning;
			clock = UnityEditor.EditorApplication.timeSinceStartup;
			UnityEditor.EditorApplication.update -= Advance;
			UnityEditor.EditorApplication.update += Advance;
		}

		public void Dispose()
		{
			UnityEditor.EditorApplication.update -= Advance;
		}

		private void Advance()
		{
			double now = UnityEditor.EditorApplication.timeSinceStartup;
			float delta = Mathf.Min(0.25f, (float)(now - clock));
			clock = now;

			if (ticked && isRunning() == false)
			{
				return;
			}

			ticked = true;
			tick(delta);
			UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
		}
	}
}
#endif

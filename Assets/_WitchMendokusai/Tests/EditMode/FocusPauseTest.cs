using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	public class FocusPauseTest
	{
		[TestCase(false, false, true)]
		[TestCase(false, true, false)]
		[TestCase(true, false, false)]
		[TestCase(true, true, false)]
		public void PausesOnlyWhenUnfocusedAndAttended(bool hasFocus, bool exempt, bool expected)
			=> Assert.AreEqual(expected, FocusPause.ShouldPause(hasFocus, exempt));

		// 릴리스 플레이어 (사람이 하는 판) 만 멈춤. 무인 신호 하나라도 있으면 면제
		[TestCase(false, false, false, false, false)]
		[TestCase(true, false, false, false, true)]
		[TestCase(false, true, false, false, true)]
		[TestCase(false, false, true, false, true)]
		[TestCase(false, false, false, true, true)]
		public void ExemptWhenAnyUnattendedSignal(bool isBatchMode, bool isDeterministic, bool isSilentRun, bool isEditor, bool expected)
			=> Assert.AreEqual(expected, FocusPause.IsExempt(isBatchMode, isDeterministic, isSilentRun, isEditor));

		// 포커스 정지는 소유자 하나로 들어가 메뉴 정지를 풀지 않음 (FocusPause 가 기대는 TimeManager 계약)
		[Test]
		public void FocusResumeKeepsMenuPause()
		{
			float previousScale = Time.timeScale;
			GameObject managerHost = new("TimeManagerTest");
			GameObject menu = new("MenuPauser");
			GameObject focus = new("FocusPauser");
			try
			{
				TimeManager timeManager = managerHost.AddComponent<TimeManager>();
				timeManager.Pause(menu);
				timeManager.Pause(focus);
				timeManager.Resume(focus);
				Assert.IsTrue(timeManager.IsPaused);
				timeManager.Resume(menu);
				Assert.IsFalse(timeManager.IsPaused);
			}
			finally
			{
				Object.DestroyImmediate(focus);
				Object.DestroyImmediate(menu);
				Object.DestroyImmediate(managerHost);
				Time.timeScale = previousScale;
			}
		}
	}
}

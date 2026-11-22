using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	// DungeonManager.cs 의 Return 조각. 같은 클래스의 partial. 상태(필드)는 원본 파일에. 판이 끝난 뒤 다시 하기, 마을로 귀환, 로비로 나갈 때의 판 포기
	public partial class DungeonManager
	{
		public void Retry()
		{
			if (CurDungeon == null || dungeonRecorder == null)
				return;
			StartDungeon(CurDungeon, restart: true, runMode: dataManager.RunLoadout.Mode, threatLevel: CurThreat.Level);
		}

		public void Continue()
		{
			if (IsDungeon || dungeonRecorder == null || isTransitioning)
				return;
			isTransitioning = true;
			StopFlowFx();
			// 집으로 돌아가기
			uiManager.Transition.Transition(
				aDuringTransition: () =>
				{
					stageManager.LoadStage(stageManager.LastStage, isBackToLastStage: true);
					ResetDungeonAndPlayer();
				},
				aWhenEnd: () =>
				{
					isTransitioning = false;
					gameEventManager.Raise(GameEventType.OnDungeonReturn);
				}).Forget();
		}

		/// <summary>
		/// 판을 보상 없이 버리고 마을 기준 상태로 되돌림. 로비로 나갈 때 (LobbyReturn). 결과창과 귀환 전환은 없음
		/// 결과창이 떠 있는 동안 (보상은 이미 받음) 불려도 귀환 초기화만 함
		/// </summary>
		public void AbandonRun()
		{
			if (isRunActive == false)
				return;
			if (IsDungeon)
			{
				IsDungeon = false;
				if (clearDelayRoutine != null)
				{
					StopCoroutine(clearDelayRoutine);
					clearDelayRoutine = null;
				}
				cardManager.CancelSelection();
				dungeonLoopSubscription?.Dispose();
				dungeonLoopSubscription = null;
				monsterSpawner.StopWave();
				resourceNodeSpawner.StopWave();
				Debug.Log($"{nameof(AbandonRun)} ({CurDungeon.Name})");
			}
			StopFlowFx();
			ResetDungeonAndPlayer();
		}

		private void ResetDungeonAndPlayer()
		{
			isRunActive = false;
			GamePresence.ReturnToTown();
			dungeonUI.ClosePanel();
			cameraManager.SetContentCameraMode(ContentCameraMode.Normal);
			SkyTimeOverride.Clear();

			gameManager.Init();
			dataManager.RunLoadout.End();
			CurThreat = DungeonThreatEffect.Identity;
			ConstraintEffect = DungeonConstraintRunEffect.Identity;
			dungeonQuest = null;
			expChecker.SetRequirementMultiplier(1f);
			expChecker.Init();
			cardManager.Reset();
		}
	}
}


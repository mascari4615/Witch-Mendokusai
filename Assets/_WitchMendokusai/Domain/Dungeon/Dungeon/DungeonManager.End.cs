using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	// DungeonManager.cs 의 End 조각. 같은 클래스의 partial. 상태(필드)는 원본 파일에. 판 끝: 패배와 클리어 연출, 결과창으로
	public partial class DungeonManager
	{
		// 사망 연출을 보인 뒤 결과창으로, 같은 판에서 클리어가 뒤따르면 클리어 우선
		private void EndDungeonByDefeat()
		{
			if (IsDungeon == false || clearDelayRoutine != null)
				return;
			SurvivalFeelConfig feel = resourceManager.SurvivalFeel;
			cardManager.CancelSelection();
			CombatSfx.Play(CombatSfxEvent.Defeat);
			AudioManager.RequestMusic(MusicState.Defeat);
			PlayFlowFx(feel.DefeatFx, feel.DefeatTitle, CurDungeon.Name);
			clearDelayRoutine = StartCoroutine(EndDungeonAfterDelay(feel.DefeatResultDelay, false));
		}

		// 클리어는 승리 연출을 보인 뒤 결과창으로. 보스가 아닌 목표 (시간 생존 등) 도 같음
		private void HandleClear()
		{
			if (clearDelayRoutine != null)
			{
				StopCoroutine(clearDelayRoutine);
				clearDelayRoutine = null;
			}
			cardManager.CancelSelection();
			SurvivalFeelConfig feel = resourceManager.SurvivalFeel;
			float delay = feel.BossClearResultDelay;
			AudioManager.RequestMusic(MusicState.Victory);
			if (delay <= 0f)
			{
				EndDungeon(isCleared: true);
				return;
			}
			// 패배 연출 중에 클리어가 뒤따르면 여기서 승리 연출로 바뀜
			CombatSfx.Play(CombatSfxEvent.Victory);
			PlayFlowFx(feel.VictoryFx, feel.VictoryTitle, CurDungeon.Name);
			playerProvider.CurrentObject.GrantInvincibility(delay);
			clearDelayRoutine = StartCoroutine(EndDungeonAfterDelay(delay, true));
			if (feel.VacuumDropsOnClear)
				StartCoroutine(VacuumDropsAfter(feel.ClearVacuumDelay));
		}

		// 남은 보상을 잃지 않고 한꺼번에. 보석, 냥, 재료 아이템. 보물 상자는 여는 효과 (카드 고르기) 가 있어서 뺌. 보스가 떨군 마도서 재료를 판이 끝나며 잃지 않게 아이템도 포함
		private IEnumerator VacuumDropsAfter(float delay)
		{
			yield return new WaitForSecondsRealtime(delay);
			bool pulled = false;
			foreach (GameObject drop in ObjectBufferManager.GetObjects(ObjectType.Drop).ToArray())
			{
				if (drop == null || drop.activeInHierarchy == false || drop.TryGetComponent(out LootObject loot) == false)
					continue;
				if (loot is ExpObject || loot is NyangObject || loot is ItemObject)
				{
					loot.Equip();
					pulled = true;
				}
			}
			if (pulled)
				CombatSfx.Play(CombatSfxEvent.MagnetPull);
		}

		private IEnumerator EndDungeonAfterDelay(float delay, bool isCleared)
		{
			yield return new WaitForSecondsRealtime(delay);
			clearDelayRoutine = null;
			EndDungeon(isCleared);
		}

		public void EndDungeon(bool isCleared = true)
		{
			if (IsDungeon == false)
				return;
			// 바깥 구간. 하위 구간 합과의 차가 재지 않은 단계 (냥 갱신, 기록, 연출 걷기, 음악) 의 비용
			using (CostLog.Measure("EndDungeon"))
			{
				FinishRun(isCleared);
			}
		}

		private void FinishRun(bool isCleared)
		{
			IsDungeon = false;
			// 폴링이 보기 전에 죽어도 클리어 보상을 이미 받았으면 클리어
			if (IsClearReached())
				isCleared = true;
			if (clearDelayRoutine != null)
			{
				StopCoroutine(clearDelayRoutine);
				clearDelayRoutine = null;
			}
			cardManager.CancelSelection();
			Debug.Log($"{nameof(EndDungeon)}");

			// Stop DungeonLoop
			dungeonLoopSubscription?.Dispose();
			dungeonLoopSubscription = null;
			using (CostLog.Measure("EndDungeon.StopWave"))
			{
				monsterSpawner.StopWave();
				resourceNodeSpawner.StopWave();
				SideRules.End();
			}

			if (isCleared == false)
				dataManager.GameStat[GameStatType.NYANG] += CurDungeon.CalculateDefeatNyang(dungeonRecorder.KillCountSinceStart);
			else if (CurDungeon.HasThreatLevels)
				CurDungeon.RecordThreatClear(CurThreat.Level);
			// 위협 배율과 냥 보너스, 클리어 보상과 패배 보상과 레벨업 전환을 포함한 이번 판 획득분 기준
			dataManager.GameStat[GameStatType.NYANG] += DungeonThreatRules.BonusNyang(dungeonRecorder.NyangSinceStart, CurThreat.RewardMultiplier);
			dataManager.GameStat[GameStatType.NYANG] += dungeonRecorder.NyangSinceStart * playerProvider.Current.UnitStat[UnitStatType.GOLD_BONUS] / UnitStat.PERCENT;
			// 위험도 단계 첫 클리어 보상. 위협 배율과 냥 보너스에 안 섞이게 그 뒤, 결과창 합계에는 들어가게 기록 전
			if (isCleared)
				GrantRiskRewards();
			DungeonRecord result = dungeonRecorder.CaptureResultRecord((int)playerProvider.Current.UnitStat[UnitStatType.LEVEL_CUR], isCleared);
			RecordRunResult(result);
			// 판이 끝난 자리에서 저장. 결과창에서 꺼지거나 죽어도 이번 판 냥과 기록이 남음
			using (CostLog.Measure("EndDungeon.Save"))
			{
				dataManager.SaveManager.SaveData();
			}

			// 결과창을 가리지 않게 남은 승패 연출 (물들임, 띠, 카메라 당김) 을 걷음
			StopFlowFx();
			using (CostLog.Measure("EndDungeon.ResultPanel"))
			{
				dungeonUI.SetPanel(DungeonPanelType.DungeonResult);
			}
			// 결과창부터는 실험실 음악. 승패 상태는 조용히 두는 자리라 여기서 넘김
			AudioManager.RequestMusic(MusicState.World);
		}
	}
}



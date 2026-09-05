using System.Collections;
using UnityEngine;
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using VContainer;

namespace WitchMendokusai
{
	// TowerDefenseModeController 의 창과 선택 조작 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefenseModeController.cs 를 본다.
	public partial class TowerDefenseModeController : MonoBehaviour
	{
		private readonly System.Collections.Generic.List<TowerDefenseBuildingPerk> perkOffers = new();
		private readonly System.Collections.Generic.List<TowerDefenseBoon> coreCards = new();

		/// <summary> 지금 고른 칸이 무엇인가 — 설치 대기 표시에 쓴다. </summary>
		private string DescribeSelectedSlot()
		{
			if (match == null || placement == null)
				return string.Empty;

			System.Collections.Generic.IReadOnlyList<TowerDefenseSlot> slots = match.AvailableSlots;
			int index = placement.SelectedSlot;
			if (index < 0 || index >= slots.Count)
				return string.Empty;

			return slots[index].Kind switch
			{
				TowerDefensePlaceableKind.Harvester => "채집 인형",
				TowerDefensePlaceableKind.Wall => "벽",
				TowerDefensePlaceableKind.Trap => "함정",
				TowerDefensePlaceableKind.Outpost => "전초기지",
				TowerDefensePlaceableKind.Generator => "발전 인형",
				TowerDefensePlaceableKind.Hero => "영웅 부르기",
				_ => "포탑 인형",
			};
		}

		/// <summary> X — 열린 창을 닫는다. 판을 나가지는 않는다(잘못 누르면 판이 통째로 끝난다). </summary>
		private void CancelPressed()
		{
			TowerDefenseHudView view = EnsureHud();
			if (view == null)
				return;

			// ★ 취소 키의 뜻 = 「지금 열린 것을 닫는다. 닫을 게 없으면 메뉴를 연다」 (TASK-WM-200).
			//   사용자 지시("X 로 게임 탈출 안 되게 · ESC 로 메뉴창")를 한 규칙으로 만족시킨다 —
			//   취소가 곧 판 끝내기였던 예전 동작은 되돌릴 수 없는 일이 가장 누르기 쉬운 자리에 있던 것이다.
			if (view.IsMenuOpen)
			{
				ToggleMenu();
				return;
			}

			// 성좌가 전체화면을 덮고 있으면 그것부터 닫는다 — 덮은 것을 두고 뒤의 것을 닫으면 안 된다.
			if (researchView != null && researchView.IsOpen)
			{
				CloseResearch();
				return;
			}

			if (view.IsMapOpen)
			{
				view.ToggleMap();
				return;
			}

			// ★ 짓기를 무르는 자리 (사용자 실측: "건물 짓기 취소는 어케함? 할 구가 없네").
			//   칸을 고르면 「설치 대기」가 켜지는데, 그걸 *끄는 손잡이가 어디에도 없었다* —
			//   마음이 바뀌면 아무 데나 지어서 부수거나 판을 나가는 수밖에 없었다.
			//   지도·메뉴보다 뒤, 고른 건물 닫기보다 앞 — 「지금 손에 든 것」이 가장 먼저 놓여야 한다.
			if (placement != null && placement.IsArmed)
			{
				placement.Disarm();
				hud?.SetArmed(false, DescribeSelectedSlot());
				return;
			}

			if (placement != null && placement.SelectedBuilding != null)
			{
				CloseSelection();
				return;
			}

			// 닫을 것이 없다 — 메뉴를 연다.
			ToggleMenu();
		}

		/// <summary>
		/// 메뉴 여닫기 단일 창구 — 메뉴와 멈춤은 한 몸이라 한 곳에서만 다룬다
		/// (따로 두면 「메뉴는 떠 있는데 판은 계속 돈다」가 생긴다).
		/// </summary>
		private void ToggleMenu()
		{
			TowerDefenseHudView view = EnsureHud();
			if (view == null)
				return;

			if (view.IsMenuOpen)
			{
				view.SetMenuOpen(false);
				ResumeFromMenu();
				return;
			}

			view.SetMenuOpen(true);
			// 메뉴를 보는 동안 코어가 깨지면 안 된다.
			if (match != null && match.IsPaused == false)
			{
				pausedByMenu = true;
				match.TogglePause();
			}
		}

		/// <summary>
		/// 판을 덮고 있는 창을 전부 닫는다 — 성좌·지도·메뉴.
		///
		/// ★ 왜 한 손으로 모으나: 판이 끝나거나 모드를 나갈 때, 덮고 있던 창이 남으면 그 뒤의 결말
		///   화면·본편이 가려진다. 창이 늘 때마다 「끝날 때도 닫아야지」를 기억해야 하면 반드시 하나를
		///   빠뜨린다(실제로 성좌만 닫고 지도·메뉴를 빠뜨렸다). 닫는 자리를 하나로 두면 새 창은
		///   여기 한 줄만 더하면 된다.
		/// </summary>
		/// <summary> 덮고 있는 것(성좌·지도·메뉴)을 전부 닫는다 — ESC 가 부르는 그 문이다. </summary>
		public void CloseOverlays()
		{
			CloseResearch();

			TowerDefenseHudView view = hud;
			if (view == null)
				return;
			if (view.IsMapOpen)
				view.ToggleMap();
			if (view.IsMenuOpen)
			{
				view.SetMenuOpen(false);
				ResumeFromMenu();
			}
		}

		// 메뉴가 멈춘 판인지 — 메뉴 때문에 멈춘 것만 메뉴가 다시 풀어야 한다(사용자가 직접 멈춰 뒀으면 그대로).
		private bool pausedByMenu;

		private void ResumeFromMenu()
		{
			if (pausedByMenu == false)
				return;
			pausedByMenu = false;
			if (match != null && match.IsPaused)
				match.TogglePause();
		}

		/// <summary> 고른 건물을 판다 — 창에서 바로(손이 규칙을 기억하지 않게). </summary>
		private void SellSelected()
		{
			MatchCombatant selected = placement.SelectedBuilding;
			if (match == null || selected == null)
				return;

			placement.SuppressNextClick();
			match.TrySell(selected.Position, stage != null ? stage.SellRefundRatio : 0.6f);
			CloseSelection();
		}

		/// <summary> 창 닫기 — 고른 것을 놓는다. </summary>
		private void CloseSelection()
		{
			placement.SuppressNextClick();
			placement.SelectBuilding(null);
			RefreshSelectionPanel();
		}

		private int RelicBalance()
		{
			return DataManager.TryGetExistingInstance(out DataManager dataManager) ? dataManager.TowerDefenseRelics : 0;
		}

		private bool CanPull()
		{
			if (stage == null || DataManager.TryGetExistingInstance(out DataManager dataManager) == false)
				return false;

			return dataManager.TowerDefenseRelics >= stage.PullCost
				&& TowerDefenseMeta.HasLockedTower(
					stage.TowerArchetypes != null ? stage.TowerArchetypes.Length : 0,
					stage.DefaultUnlockedTowerCount,
					dataManager.TowerDefenseUnlockedTowers);
		}

		/// <summary>
		/// 인형 뽑기 — 결말 화면에서 바로. 별도 창을 새로 세우지 않는 이유: 뽑는 순간은 판이 끝난 직후이고,
		/// 그 자리에서 「다음 판엔 이게 있다」로 이어져야 다시 도전할 이유가 그 화면 안에서 닫힌다.
		/// </summary>
		private void PullTower()
		{
			if (stage == null || DataManager.TryGetExistingInstance(out DataManager dataManager) == false)
				return;

			int relics = dataManager.TowerDefenseRelics;
			bool pulled = TowerDefenseMeta.TryPull(
				stage.TowerArchetypes != null ? stage.TowerArchetypes.Length : 0,
				stage.DefaultUnlockedTowerCount,
				dataManager.TowerDefenseUnlockedTowers,
				ref relics,
				stage.PullCost,
				UnityEngine.Random.value,
				out int pulledIndex);

			if (pulled == false)
				return;

			dataManager.TowerDefenseRelics = relics;
			dataManager.SaveManager.SaveData();

			TowerDefenseTowerArchetype pulledTower = match.TowerArchetypeAt(pulledIndex);
			Debug.Log($"{nameof(TowerDefenseModeController)}: 인형 뽑기 — {(pulledTower != null ? pulledTower.DisplayName : pulledIndex.ToString())} 획득 (유물 {relics} 남음)");
			hud?.ShowPullResult(pulledTower, relics, CanPull());
		}

		/// <summary> 코어 레벨업 카드 선택 — 판 전체에 걸린다. </summary>
		private void ChooseCoreCard(int index)
		{
			placement.SuppressNextClick();
			match.ChooseCoreCard(index);
		}

		/// <summary>
		/// 고른 건물의 선택창 — 강화 선택지 / 코어 카드 / 연구 버튼이 여기서 뜬다.
		///
		/// ★ 이게 없어서 그 셋이 전부 *코드만 있고 한 번도 안 떴다*(라이브 측정으로 드러남 —
		///   화면 조각을 세어보니 선택창이 목록에 아예 없었다). 「건물 선택하면 그때 띄운다」가
		///   사용자가 요청한 모양이므로, 고른 대상이 바뀔 때마다 그 대상 기준으로 다시 그린다.
		/// ★ 매 프레임 부르지만 화면은 *개수가 바뀔 때만* 다시 그린다(그쪽에 못 박혀 있다) —
		///   여기서 미리 걸러내면 「무엇이 바뀌었나」 판정이 두 곳에 갈라진다.
		/// </summary>
		private void RefreshSelectionPanel()
		{
			if (hud == null)
				return;

			MatchCombatant selected = placement.SelectedBuilding;
			if (selected == null || selected.IsAlive == false)
			{
				hud.ShowSelection(null, canResearch: false, researchLevel: 0, researchCost: 0);
				return;
			}

			bool isCore = match.CoreCombatant != null && selected == match.CoreCombatant;

			perkOffers.Clear();
			TowerDefenseDollLabel doll = match.FindDoll(selected);
			if (doll != null && doll.Progress.PendingChoices > 0)
				TowerDefenseBuildingProgress.Offer(doll.BuildingId, doll.Progress.Level, doll.IsHarvester, perkOffers);

			coreCards.Clear();
			if (isCore)
				match.OfferCoreCards(coreCards);

			hud.ShowSelection(
				match.DescribeUnit(selected),
				canResearch: isCore,
				researchLevel: match.LabCount,
				researchCost: match.ResearchCost,
				researchUsesEssence: match.ResearchUsesEssence,
				perkOffers,
				coreCards);

			// 연구 길 — 값을 치르기 전에 무엇을 얻는지 보여준다(표는 규칙층이 준 것 그대로).
			if (isCore)
			{
				match.DescribeUnlockPath(unlockPath);
				hud.ShowUnlockPath(unlockPath, match.LabCount);
			}
		}

		private readonly System.Collections.Generic.List<TowerDefenseUnlockEntry> unlockPath = new();

		/// <summary> 고른 건물의 레벨업 선택 — 그 클릭이 설치로 새지 않게 한 번 삼킨다. </summary>
		private void ChoosePerk(TowerDefenseBuildingPerk perk)
		{
			placement.SuppressNextClick();
			match.ChooseBuildingPerk(placement.SelectedBuilding, perk);
		}

		/// <summary> 난이도 한 단계 — *다음 판*부터 걸린다(시작 조건이라 도는 판을 바꾸지 않는다). </summary>
		private void CycleDifficulty()
		{
			placement.SuppressNextClick();
			match.Difficulty = TowerDefenseDifficulty.Next(match.Difficulty);
			Debug.Log($"{nameof(TowerDefenseModeController)}: 난이도 → {TowerDefenseDifficulty.NameOf(match.Difficulty)} (다음 판부터)");
		}

		/// <summary> UI 배율 한 단계 — 그 클릭이 설치로 새지 않게 한 번 삼킨다. </summary>
		private void CycleUiScale()
		{
			placement.SuppressNextClick();
			hud?.CycleUiScale();
		}


		/// <summary> 전체 사거리 표시 토글(디버그) — 그 클릭이 설치로 새지 않게 한 번 삼킨다. </summary>
		private void ToggleAllRanges()
		{
			placement.SuppressNextClick();
			match.ToggleAllRanges();
		}

		/// <summary> 핫바 클릭 — 숫자키와 같은 경로. 그 클릭이 설치로도 새지 않게 한 번 삼킨다. </summary>
		private void SelectSlotFromUi(int slot)
		{
			placement.SuppressNextClick();
			placement.SelectSlot(slot);
		}

		/// <summary> 웨이브 진행 방식 전환(자동↔수동) — 진행 중인 매치에 즉시 반영된다. </summary>
		private void ToggleWaveMode()
		{
			placement.SuppressNextClick(); // 버튼 클릭이 배치로 새는 것 차단.
			match.AutoAdvanceWaves = match.AutoAdvanceWaves == false;
		}

		/// <summary> 다음 웨이브 호출 — 수동 진행의 진행 수단이자, 자동에서도 남은 건설 시간을 건너뛴다. </summary>
		private void CallNextWave()
		{
			placement.SuppressNextClick();
			match.RequestNextWave();
		}
	}
}

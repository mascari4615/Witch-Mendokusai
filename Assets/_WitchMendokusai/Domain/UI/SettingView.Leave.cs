using System;
using UnityEngine;

namespace WitchMendokusai
{
	// SettingView.cs 의 Leave 조각. 같은 클래스의 partial. 상태(필드)는 원본 파일에.
	// 전체 메뉴의 끼임 탈출, 던전 포기, 로비로, 게임 종료. 버튼은 메뉴에만 있음
	// 확인은 메뉴 위에 따로 뜨는 팝업 (MenuConfirmView). 설정창은 열지 않음 (2026-11-23 전에는 설정창의 확인 자리를 빌려 설정창이 같이 열렸음)
	// 취소하면 팝업만 닫혀 메뉴가 그대로, 수락하면 closeMenu 로 메뉴를 닫고 동작
	public partial class SettingView
	{
		/// <summary> 전체 메뉴와 같은 버튼에 붙는 툴팁. 버튼 글자는 짧게 </summary>
		public const string LOBBY_TOOLTIP = "저장하고 로비 (시작 화면) 로 갑니다";

		/// <summary> 전체 메뉴의 끼임 탈출. 지금 스테이지에 들어선 자리로 옮길지 묻는 창 </summary>
		public void RequestUnstuck(Action closeMenu)
			=> OpenConfirmFromMenu(closeMenu, "들어온 자리로 돌아갈까요?", "땅에 끼였거나 떨어졌을 때 지금 스테이지에 들어선 자리로 옮깁니다.", "돌아가기", OnUnstuck);

		/// <summary> 전체 메뉴의 던전 포기. 이번 판을 패배로 끝낼지 묻는 창 </summary>
		public void RequestDungeonExit(Action closeMenu)
			=> OpenConfirmFromMenu(closeMenu, "던전을 포기할까요?", "이번 판은 패배로 끝납니다.", "포기", OnDungeonExit);

		/// <summary> 전체 메뉴의 게임 종료 </summary>
		public void OpenQuitConfirm(Action closeMenu)
			=> OpenConfirmFromMenu(closeMenu, "게임을 종료할까요?", "진행 중인 내용은 저장 시점까지만 남습니다.", "종료", OnQuit);

		/// <summary>
		/// 전체 메뉴의 로비로. 던전 판이 진행 중이면 포기 확인 창을 띄움. 마을에서는 확인 없이 바로 이동
		/// </summary>
		public void RequestReturnToLobby(Action closeMenu)
		{
			if (LobbyManager.IsRunInProgress == false)
			{
				closeMenu();
				ReturnToLobby();
				return;
			}
			OpenConfirmFromMenu(closeMenu, "로비로 갈까요?", "진행 중인 던전은 포기합니다. 판에서 주운 것은 남고 끝날 때 받는 정산은 없습니다.", "포기하고 로비로", ReturnToLobby);
		}

		private void OpenConfirmFromMenu(Action closeMenu, string title, string description, string acceptLabel, Action action)
			=> uiRoot.MenuConfirmView.Ask(title, description, acceptLabel, () =>
			{
				closeMenu();
				action();
			});

		private void OnDungeonExit()
		{
			if (playerObject == null)
				return;
			playerObject.ReceiveDamage(new DamageInfo
			{
				damage = 9999,
				type = DamageType.Critical,
				damageSource = playerObject,
				equipmentDataId = DamageInfo.NO_DATA_ID,
				skillDataId = DamageInfo.NO_DATA_ID,
				ignoreInvincible = true,
			});
		}

		private void OnUnstuck()
		{
			if (StageManager.TryGetExistingInstance(out StageManager stageManager))
				stageManager.ReturnToEntry();
		}

		private void OnQuit() => Application.Quit();

		private void ReturnToLobby()
		{
			LobbyManager.ReturnFromWorld(dataManager, inputManager);
		}
	}
}

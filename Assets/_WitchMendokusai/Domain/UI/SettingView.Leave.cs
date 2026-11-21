using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace WitchMendokusai
{
	// SettingView.cs 의 Leave 조각. 같은 클래스의 partial. 상태(필드)는 원본 파일에. 설정창에서 게임을 떠나는 버튼. 던전 포기, 끼임 탈출, 로비로, 게임 종료
	public partial class SettingView
	{
		private void OnDungeonExit()
		{
			Close(false);
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
			Close(false);
			if (StageManager.TryGetExistingInstance(out StageManager stageManager))
				stageManager.ReturnToEntry();
		}

		private void OnQuit() => Application.Quit();

		/// <summary> 전체 메뉴의 게임 종료. 설정창을 열고 종료 확인 창을 바로 띄움. 취소하면 설정창도 닫혀 메뉴로 돌아감 </summary>
		public void OpenQuitConfirm()
		{
			Open();
			closeAfterConfirm = true;
			AskQuit();
		}

		private void AskQuit()
			=> AskConfirmWithSound("게임을 종료할까요?", "진행 중인 내용은 저장 시점까지만 남습니다.", "종료", OnQuit);

		/// <summary> 전체 메뉴와 설정창 버튼의 로비 이동에 붙는 툴팁. 버튼 글자는 짧게 </summary>
		public const string LOBBY_TOOLTIP = "저장하고 로비 (시작 화면) 로 갑니다";

		/// <summary>
		/// 전체 메뉴의 로비로. 던전 판이 진행 중이면 설정창을 열고 포기 확인 창을 띄움 (취소하면 메뉴로 돌아감).
		/// 마을에서는 확인 없이 바로 이동
		/// </summary>
		public void RequestReturnToLobby()
		{
			if (LobbyManager.IsRunInProgress == false)
			{
				ReturnToLobby();
				return;
			}
			Open();
			closeAfterConfirm = true;
			AskReturnToLobby();
		}

		private void AskReturnToLobby()
		{
			if (LobbyManager.IsRunInProgress == false)
			{
				UiSfx.Play(UiSfxEvent.Confirm);
				ReturnToLobby();
				return;
			}
			AskConfirmWithSound("로비로 갈까요?", "진행 중인 던전은 포기합니다. 판에서 주운 것은 남고 끝날 때 받는 정산은 없습니다.", "포기하고 로비로", ReturnToLobby);
		}

		private void ReturnToLobby()
		{
			Close(false);
			LobbyManager.ReturnFromWorld(dataManager, inputManager);
		}
	}
}


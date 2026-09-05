using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using Newtonsoft.Json;
using UnityEngine.SceneManagement;

namespace WitchMendokusai
{
	public class PlayFabManager : MonoBehaviour
	{
		public static bool Logged = false;

		// DataManager 와 같은 GameObject (DataManager.prefab). 같은 오브젝트 의존은 Awake GetComponent (SettingView 정본 패턴)
		private DataManager dataManager;

		private void Awake()
		{
			dataManager = GetComponent<DataManager>();
		}

		private void Start()
		{
			if (Logged == true)
				return;

			if (SceneManager.GetActiveScene().buildIndex != 0)
				Login();
		}

		public void Login()
		{
			Debug.Log($"{nameof(Login)}");
			
			if (AppSetting.Data.UseLocalData)
				return;

			LoginWithCustomIDRequest loginReq = new()
			{
				CustomId = SystemInfo.deviceUniqueIdentifier,
				CreateAccount = true,

				InfoRequestParameters = new GetPlayerCombinedInfoRequestParams
				{
					GetPlayerProfile = true
				}
			};

			PlayFabClientAPI.LoginWithCustomID(loginReq, result =>
			{
				Debug.Log("Successful login/account create!");

				Logged = true;
				string name = result.InfoResultPayload.PlayerProfile?.DisplayName;

				if (name != null)
				{
					dataManager.localDisplayName = name;
					SubmitNickname($"Temp_{SystemInfo.deviceUniqueIdentifier}"[0..10]);
				}
				else
				{
					SubmitNickname($"Temp_{SystemInfo.deviceUniqueIdentifier}"[0..10]);
				}

				LoadPlayerData();
				GetAppearance();
				GetTitleData();
				GetVirtualCurrencies();
			}, OnError);

			void LoadPlayerData()
			{
				PlayFabClientAPI.GetUserData(new GetUserDataRequest(), OnPlayerDataReceived, OnError);
			}
			void OnPlayerDataReceived(GetUserDataResult result)
			{
				Debug.Log("Received PlayerData!");

				if (result.Data?.ContainsKey("Player") == true)
				{
					GameData gameData = JsonConvert.DeserializeObject<GameData>(result.Data["Player"].Value);
					if (gameData != null)
					{
						dataManager.SaveManager.LoadData(gameData);
						return;
					}

					dataManager.CreateNewGameData();
				}
			}

			void GetAppearance()
			{
				return;
			}

			void GetTitleData()
			{
				PlayFabClientAPI.GetTitleData(new GetTitleDataRequest(), result =>
					{
						if (result.Data == null || result.Data.Count == 0)
						{
							Debug.Log("No TitleData!");
							return;
						}

						if (result.Data.ContainsKey("Message"))
							Debug.Log(result.Data["Message"]);

						if (result.Data.ContainsKey("Multiplier"))
							Debug.Log(result.Data["Multiplier"]);
					},
					OnError);
			}

			void GetVirtualCurrencies()
			{
				PlayFabClientAPI.GetUserInventory(new GetUserInventoryRequest(), OnGetUserInventorySuccess, OnError);
			}
			void OnGetUserInventorySuccess(GetUserInventoryResult result)
			{
			}
		}

		public void SubmitNickname(string name)
		{
			Debug.Log($"{nameof(SubmitNickname)} : ({name})");
			UpdateUserTitleDisplayNameRequest request = new()
			{
				DisplayName = name,
			};

			PlayFabClientAPI.UpdateUserTitleDisplayName(request, result =>
			{
				Debug.Log("Updated display name!");
				dataManager.localDisplayName = result.DisplayName;
			}, OnError);
		}

		public void SendLeaderboard(int playTime)
		{
			UpdatePlayerStatisticsRequest request = new()
			{
				Statistics = new List<StatisticUpdate>
			{
				new() {
					StatisticName = "�÷��̽ð�",
					Value = playTime
				}
			}
			};

			PlayFabClientAPI.UpdatePlayerStatistics(request, result =>
			{
			}, OnError);
		}

		public void GetLeaderboard()
		{
			GetLeaderboardRequest request = new()
			{
				StatisticName = "�÷��̽ð�",
				StartPosition = 0,
				MaxResultsCount = 10
			};

			PlayFabClientAPI.GetLeaderboard(request, result =>
			{
				foreach (PlayerLeaderboardEntry item in result.Leaderboard)
				{
					Debug.Log(item.Position + " " + item.PlayFabId + " " + item.StatValue + " " + item.DisplayName);
				}
			}, OnError);
		}

		public void GetLeaderboardAroundPlayer()
		{
			GetLeaderboardAroundPlayerRequest request = new()
			{
				StatisticName = "�÷��̽ð�",
				MaxResultsCount = 10
				// Ȧ���� �ϸ� ����� ��ġ
			};
			PlayFabClientAPI.GetLeaderboardAroundPlayer(request, result =>
			{
				foreach (PlayerLeaderboardEntry item in result.Leaderboard)
				{
					Debug.Log(item.Position + " " + item.PlayFabId + " " + item.StatValue + " " + item.DisplayName);
				}
			}, OnError);
		}

		public void SaveUserData(string _key, string _value)
		{
			UpdateUserDataRequest request = new()
			{
				Data = new Dictionary<string, string>
				{
					{ _key, _value }
				}
			};
			PlayFabClientAPI.UpdateUserData(request, OnDataSend, OnError);
		}

		[ContextMenu(nameof(GetTitleNewsData))]
		private void GetTitleNewsData()
		{
			PlayFabClientAPI.GetTitleNews(new GetTitleNewsRequest(),
				result =>
				{
					if (result.News == null || result.News.Count == 0)
					{
						Debug.Log("No News!");
						return;
					}

					foreach (TitleNewsItem item in result.News)
					{
						Debug.Log($"{item.Title} : {item.Body}");
					}
				},
				OnError);
		}

		public void BuyItem()
		{
			SubtractUserVirtualCurrencyRequest request = new()
			{
				VirtualCurrency = "AC",
				Amount = 10
			};
			PlayFabClientAPI.SubtractUserVirtualCurrency(request, result => { Debug.Log("Bought item! " + "itemName"); },
				OnError);
		}

		public void SavePlayerData(GameData gameData)
		{
			UpdateUserDataRequest request = new()
			{
				Data = new Dictionary<string, string>
				{
					{ "Player", JsonConvert.SerializeObject(gameData) },
				}
			};
			PlayFabClientAPI.UpdateUserData(request, OnDataSend, OnError);
		}

		private void OnDataSend(UpdateUserDataResult result)
		{
			Debug.Log("Successful user data send!");
		}

		public void CloudScriptTest()
		{
			ExecuteCloudScriptRequest request = new()
			{
				FunctionName = "hello",
				FunctionParameter = new
				{
					name = "Sans"
				}
			};
			PlayFabClientAPI.ExecuteCloudScript(request, OnExecuteSuccess, OnError);
		}

		private void OnExecuteSuccess(ExecuteCloudScriptResult result)
		{
			if (result.FunctionResult != null)
				Debug.Log(result.FunctionResult.ToString());
		}

		public void SendFeedback(string topic, string message)
		{
			ExecuteCloudScriptRequest request = new()
			{
				FunctionName = "sendFeedback",
				FunctionParameter = new
				{
					topic,
					message
				}
			};
			PlayFabClientAPI.ExecuteCloudScript(request, OnExecuteSuccess, OnError);
		}

		private void OnError(PlayFabError error)
		{
			Debug.LogError($"{nameof(PlayFabManager)} ERROR : {error.GenerateErrorReport()}");
		}
	}
}
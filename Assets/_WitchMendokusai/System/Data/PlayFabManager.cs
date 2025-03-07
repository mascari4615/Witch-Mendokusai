using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using Newtonsoft.Json;
// using GooglePlayGames;
// using GooglePlayGames.BasicApi;
using TMPro;
using UnityEngine.SceneManagement;

namespace WitchMendokusai
{
	public class PlayFabManager : MonoBehaviour
	{
		public static bool Logined = false;

		float secondsLeftToRefreshEnergy = 1;
		[SerializeField] private TextMeshProUGUI GoogleStatusText;

		private void Start()
		{
			if (Logined)
				return;

			/*PlayGamesClientConfiguration config = new PlayGamesClientConfiguration.Builder()
				.AddOauthScope("profile")
				.RequestServerAuthCode(false)
				.Build();
			PlayGamesPlatform.InitializeInstance(config);
			PlayGamesPlatform.DebugLogEnabled = true;
			PlayGamesPlatform.Activate();*/

			if (SceneManager.GetActiveScene().buildIndex != 0)
				Login();
		}

		public void Login()
		{
			if (GameSetting.UseLocalData)
				return;

			/*#if !UNITY_EDITOR
					Social.localUser.Authenticate((bool success) => {

						if (success)
						{
							GoogleStatusText.text = "Google Signed In";
							var serverAuthCode = PlayGamesPlatform.Instance.GetServerAuthCode();
							GoogleStatusText.text = "Server Auth Code: " + serverAuthCode;

							PlayFabClientAPI.LoginWithGoogleAccount(new LoginWithGoogleAccountRequest()
							{
								TitleId = PlayFabSettings.TitleId,
								ServerAuthCode = serverAuthCode,
								CreateAccount = true
							}, (result) =>
							{
								GoogleStatusText.text = "Signed In as " + result.PlayFabId;

								Debug.Log("Successful login/account create!");

								string name = result?.InfoResultPayload?.PlayerProfile?.DisplayName;
								Debug.Log("A");

								if (name != null)
								{
									DataManager.Instance.localDisplayName = name;
									// TODO : MainMenuManager.Instance.UpdateNickNameUI(name);
									SubmitNickname(result.PlayFabId);
								}
								else
								{
									// TODO : MainMenuManager.Instance.OpenNicknamePanel();
									SubmitNickname(result.PlayFabId);
								}
								Debug.Log("B");

								LoadPlayerData();
								Debug.Log("C");

								GetAppearance();
								Debug.Log("D");

								GetTitleData();
								Debug.Log("E");

								GetVirtualCurrencies();
								Debug.Log("F");

								SceneManager.LoadScene(1);
							}, OnError);
						}
						else
						{
							Debug.Log("Google Failed to Authorize your login");
						}
					});
			#else*/

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

				Logined = true;
				string name = result.InfoResultPayload.PlayerProfile?.DisplayName;

				if (name != null)
				{
					DataManager.Instance.localDisplayName = name;
					// TODO : MainMenuManager.Instance.UpdateNickNameUI(name);
					SubmitNickname($"Temp_{SystemInfo.deviceUniqueIdentifier}"[0..10]);
				}
				else
				{
					// TODO : MainMenuManager.Instance.OpenNicknamePanel();
					SubmitNickname($"Temp_{SystemInfo.deviceUniqueIdentifier}"[0..10]);
				}

				LoadPlayerData();
				GetAppearance();
				GetTitleData();
				GetVirtualCurrencies();

				// SceneManager.LoadScene(1);
			}, OnError);
			// #endif

			void LoadPlayerData()
			{
				PlayFabClientAPI.GetUserData(new GetUserDataRequest(), OnPlayerDataRecieved, OnError);
			}
			void OnPlayerDataRecieved(GetUserDataResult result)
			{
				Debug.Log("Recieved PlayerData!");

				if (result.Data?.ContainsKey("Player") == true)
				{
					GameData gameData = JsonConvert.DeserializeObject<GameData>(result.Data["Player"].Value);
					if (gameData != null)
					{
						CreateAndSavePlayerData();
						return;
					}
					DataManager.Instance.SaveManager.LoadData(gameData);
				}
				{
					CreateAndSavePlayerData();
				}
			}

			void GetAppearance()
			{
				return;

				/*PlayFabClientAPI.GetUserData(new GetUserDataRequest(), result =>
				{
					if (result.Data != null && result.Data.ContainsKey("Sans"))
					{
						Sans = result.Data["Sans"].Value + "Ang";
					}
					else
					{
						SaveUserData(nameof(Sans), Sans);
					}
				}, OnError);*/
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
				// int angelCoins = result.VirtualCurrency["AC"];
				// Debug.Log("AngelCoins" + angelCoins);
				// secondsLeftToRefreshEnergy = result.VirtualCurrencyRechargeTimes["AC"].SecondsToRecharge;
			}
		}

		public void SubmitNickname(string name)
		{
			Debug.Log($"{nameof(SubmitNickname)} : ({name})");
			var request = new UpdateUserTitleDisplayNameRequest
			{
				DisplayName = name,
			};

			PlayFabClientAPI.UpdateUserTitleDisplayName(request, result =>
			{
				Debug.Log("Updated display name!");
				DataManager.Instance.localDisplayName = result.DisplayName;
				// TODO : MainMenuManager.Instance.UpdateNickNameUI(result.DisplayName);
			}, OnError);
		}

		public void SendLeaderboard(int playTime)
		{
			var request = new UpdatePlayerStatisticsRequest
			{
				Statistics = new List<StatisticUpdate>
			{
				new StatisticUpdate
				{
					StatisticName = "�÷��̽ð�",
					Value = playTime
				}
			}
			};

			PlayFabClientAPI.UpdatePlayerStatistics(request, result =>
			{
				// Debug.Log("Successfull leaderboard sent");
			}, OnError);
		}

		public void GetLeaderboard()
		{
			var request = new GetLeaderboardRequest
			{
				StatisticName = "�÷��̽ð�",
				StartPosition = 0,
				MaxResultsCount = 10
			};

			PlayFabClientAPI.GetLeaderboard(request, result =>
			{
				foreach (var item in result.Leaderboard)
				{
					Debug.Log(item.Position + " " + item.PlayFabId + " " + item.StatValue + " " + item.DisplayName);
				}
			}, OnError);
		}

		public void GetLeaderboardAroundPlayer()
		{
			var request = new GetLeaderboardAroundPlayerRequest
			{
				StatisticName = "�÷��̽ð�",
				MaxResultsCount = 10
				// Ȧ���� �ϸ� ����� ��ġ
			};
			PlayFabClientAPI.GetLeaderboardAroundPlayer(request, result =>
			{
				foreach (var item in result.Leaderboard)
				{
					Debug.Log(item.Position + " " + item.PlayFabId + " " + item.StatValue + " " + item.DisplayName);
				}
			}, OnError);
		}

		public void SaveUserData(string _key, string _value)
		{
			var requset = new UpdateUserDataRequest
			{
				Data = new Dictionary<string, string>
				{
					{ _key, _value }
				}
			};
			PlayFabClientAPI.UpdateUserData(requset, OnDataSend, OnError);
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

					foreach (var item in result.News)
					{
						Debug.Log($"{item.Title} : {item.Body}");
					}
				},
				OnError);
		}

		public void BuyItem()
		{
			var request = new SubtractUserVirtualCurrencyRequest
			{
				VirtualCurrency = "AC",
				Amount = 10
			};
			PlayFabClientAPI.SubtractUserVirtualCurrency(request, result => { Debug.Log("Bought item! " + "itemName"); },
				OnError);
		}

		public void SavePlayerData(GameData gameData)
		{
			var requset = new UpdateUserDataRequest
			{
				Data = new Dictionary<string, string>
			{
				{ "Player", JsonConvert.SerializeObject(gameData) },
			}
			};
			PlayFabClientAPI.UpdateUserData(requset, OnDataSend, OnError);
		}

		public void CreateAndSavePlayerData()
		{
			DataManager.Instance.SaveManager.CreateNewGameData();
		}

		private void OnDataSend(UpdateUserDataResult result)
		{
			Debug.Log("Successful user data send!");
		}

		public void CloudScriptTest()
		{
			var request = new ExecuteCloudScriptRequest
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
			var request = new ExecuteCloudScriptRequest
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

		private void Update()
		{
			/*secondsLeftToRefreshEnergy -= Time.deltaTime;
			TimeSpan time = TimeSpan.FromSeconds(secondsLeftToRefreshEnergy);
			// Debug.Log(time.ToString("mm':'ss"));
			if (secondsLeftToRefreshEnergy < 0)
				GetVirtualCurrencies();*/
		}

		private void OnError(PlayFabError error)
		{
			// Debug.Log("Error while logging in/creating account!");
			Debug.LogError($"{nameof(PlayFabManager)} ERROR : {error.GenerateErrorReport()}");
		}
	}
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = nameof(GameSettings), menuName = "GameSettings")]
	public class GameSettings : ScriptableObject
	{
		[field: Header("_" + nameof(GameSettings))]
		[field: SerializeField] public bool UseLocalData { get; private set; } = true;
	}

	public static class GameSetting
	{
		public static bool UseLocalData = false;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		public static void OnBooting()
		{
			Debug.Log("SANS");

			GameSettings gameSettings = Resources.Load<GameSettings>(nameof(GameSettings));
			if (gameSettings == null)
			{
				Debug.LogError("GameSettings not found");
				return;
			}

			UseLocalData = gameSettings.UseLocalData;
		}
	}
}
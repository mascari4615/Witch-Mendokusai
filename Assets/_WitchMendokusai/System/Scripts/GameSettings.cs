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
		[field: SerializeField] public bool InitDataSODict { get; private set; } = true;
	}

	public static class GameSetting
	{
		public static GameSettings Data { get; private set; }

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		public static void OnBooting()
		{
			Debug.Log("SANS");

			Data = Resources.Load<GameSettings>(nameof(GameSettings));
			if (Data == null)
			{
				Debug.LogError("GameSettings not found");
				return;
			}
		}
	}
}
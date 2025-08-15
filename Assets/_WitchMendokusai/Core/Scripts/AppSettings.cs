using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = nameof(AppSettings), menuName = "WM/AppSettings")]
	public class AppSettings : ScriptableObject
	{
		[field: Header("_" + nameof(AppSettings))]
		[field: SerializeField] public bool UseLocalData { get; private set; } = true;
		[field: SerializeField] public bool InitDataSODict { get; private set; } = true;
	}

	public static class AppSetting
	{
		public static AppSettings Data { get; private set; }

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		public static void OnBooting()
		{
			Debug.Log("SANS");

			Data = Resources.Load<AppSettings>(nameof(AppSettings));
			if (Data == null)
			{
				Debug.LogError($"{nameof(AppSettings)} not found");
				return;
			}
		}
	}
}
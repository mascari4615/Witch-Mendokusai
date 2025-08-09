using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = nameof(BootstrapSettings), menuName = "BootstrapSettings")]
	public class BootstrapSettings : ScriptableObject
	{
		[field: Header("_" + nameof(BootstrapSettings))]
		[field: SerializeField] public TimeManager TimeManagerPrefab { get; private set; }
		[field: SerializeField] public DataManager DataManagerPrefab { get; private set; }
		[field: SerializeField] public AudioManager AudioManagerPrefab { get; private set; }
		[field: SerializeField] public InputManager InputManagerPrefab { get; private set; }
		[field: SerializeField] public bool UseBootstrap { get; private set; } = true;
	}

	public static class Bootstrap
	{
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		public static void OnBooting()
		{
			Debug.Log("SANS");

			BootstrapSettings bootstrapStuff = Resources.Load<BootstrapSettings>(nameof(BootstrapSettings));
			if (bootstrapStuff == null)
			{
				Debug.LogError("BootStrapSettings not found");
				return;
			}

			Object.Instantiate(bootstrapStuff.TimeManagerPrefab);
			Object.Instantiate(bootstrapStuff.DataManagerPrefab);
			Object.Instantiate(bootstrapStuff.AudioManagerPrefab);
			Object.Instantiate(bootstrapStuff.InputManagerPrefab);
		}
	}
}
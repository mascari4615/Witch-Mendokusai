using UnityEngine;

namespace WitchMendokusai
{
	public static class UGCLog
	{
		public static bool Verbose = true;

		public static void Info(string message)
		{
			if (!Verbose)
				return;

			Debug.Log($"[UGC] {message}");
		}

		public static void Warn(string message)
		{
			Debug.LogWarning($"[UGC] {message}");
		}

		public static void Error(string message)
		{
			Debug.LogError($"[UGC] {message}");
		}
	}
}

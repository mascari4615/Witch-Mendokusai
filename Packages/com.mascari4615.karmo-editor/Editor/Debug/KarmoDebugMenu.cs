using UnityEditor;
using System.Threading;
using UnityEngine;
using KarmoLab.KarmoEditor.Settings;

namespace KarmoLab.KarmoEditor.DebugUtils
{
	/// <summary>
	/// Mutex 해제 및 디버깅 유틸리티 (설정 기반)
	/// </summary>
	public static class KarmoDebugMenu
	{
		[MenuItem(Define.RootMenu + "DEBUG/Kill App Mutex %&m")]
		public static void KillMutex()
		{
			// 1. 설정 파일 찾기
			var guids = AssetDatabase.FindAssets("t:" + nameof(KarmoEditorSettings));
			if (guids.Length == 0)
			{
				Debug.LogWarning($"{Define.LogPrefix} KarmoEditorSettings asset not found! Please create one.");
				return;
			}

			var settings = AssetDatabase.LoadAssetAtPath<KarmoEditorSettings>(AssetDatabase.GUIDToAssetPath(guids[0]));
			if (settings == null) return;

			// 2. Mutex 해제
			if (settings.ApplicationMutexNames != null)
			{
				foreach (var name in settings.ApplicationMutexNames)
				{
					if (Mutex.TryOpenExisting(name, out Mutex mutex))
					{
						try
						{
							mutex.ReleaseMutex();
							Debug.Log($"{Define.LogPrefix} Released: {name}");
						}
						catch (System.Exception ex)
						{
							Debug.LogWarning($"{Define.LogPrefix} Release failed for {name}: {ex.Message}");
						}
						finally
						{
							mutex.Close();
							mutex.Dispose();
							Debug.Log($"{Define.LogPrefix} Closed and Disposed: {name}");
						}
					}
					else
					{
						Debug.Log($"{Define.LogPrefix} Mutex not found: {name}");
					}
				}
			}

			// 3. 필드 초기화 (리플렉션)
			if (settings.ReflectionFieldResets != null)
			{
				foreach (var info in settings.ReflectionFieldResets)
				{
					System.Type targetType = System.Type.GetType(info.FullTypeName);
					if (targetType != null)
					{
						System.Reflection.FieldInfo field = targetType.GetField(info.FieldName, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
						if (field != null)
						{
							field.SetValue(null, null);
							Debug.Log($"{Define.LogPrefix} Cleared field: {info.FullTypeName}.{info.FieldName}");
						}
					}
				}
			}

			Debug.Log($"{Define.LogPrefix} Mutex cleanup finished! ✨");
		}
	}
}

using KarmoLabs;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	public class SaveDataMenu
	{
		[MenuItem("WM/Delete Save Data")]
		public static void DeleteSaveData()
		{
			string savePath = SaveTool.GetSaveFilePath(SaveManager.SAVE_FILE_NAME);

			// 플레이어나 이 에디터의 플레이 모드가 세이브의 주인이면 지우지 않음. 지워도 주인이 곧 메모리로 다시 씀
			SaveOwnerLockResult lockResult = SaveOwnerLock.TryAcquire(savePath, out SaveOwnerLock ownerLock);
			if (lockResult == SaveOwnerLockResult.HeldByOther)
			{
				Debug.LogWarning($"Save data is in use by a running game. Close it first: {savePath}");
				return;
			}

			using SaveOwnerLock heldLock = ownerLock;
			if (System.IO.File.Exists(savePath))
			{
				SaveTool.DeleteSaveFile(SaveManager.SAVE_FILE_NAME);
				Debug.Log($"All save data has been deleted: {savePath}");
			}
			else
			{
				Debug.Log($"No save data found to delete: {savePath}");
			}
		}
	}
}

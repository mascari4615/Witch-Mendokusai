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

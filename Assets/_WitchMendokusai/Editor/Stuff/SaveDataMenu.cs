using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	public class SaveDataMenu
	{
		[MenuItem("WitchMendokusai/Delete Save Data")]
		public static void DeleteSaveData()
		{
			string path = Path.Combine(Application.dataPath, "WM.json");

			if (File.Exists(path))
			{
				File.Delete(path);
				Debug.Log("All save data has been deleted.");
			}
			else
			{
				Debug.Log("No save data found to delete.");
			}
		}
	}
}
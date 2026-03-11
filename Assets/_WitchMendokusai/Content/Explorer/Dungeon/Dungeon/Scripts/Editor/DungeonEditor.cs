using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	[CustomEditor(typeof(Dungeon))]
	public class DungeonEditor : Editor
	{
		public override void OnInspectorGUI()
		{
			Dungeon dungeon = (Dungeon)target;

			if (dungeon.ObjectiveType == DungeonObjectiveType.TimeSurvival)
			{
				EditorGUILayout.HelpBox(
					$"TimeSurvival은 ClearValue를 사용하지 않습니다.\nTimeBySecond({dungeon.TimeBySecond})가 클리어 조건으로 사용됩니다.",
					MessageType.Info);
			}

			base.OnInspectorGUI();
		}
	}
}

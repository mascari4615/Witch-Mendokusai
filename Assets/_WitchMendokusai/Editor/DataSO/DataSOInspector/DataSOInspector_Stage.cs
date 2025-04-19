using System;
using System.Collections.Generic;
using UnityEditor;

namespace WitchMendokusai
{
	[CustomEditor(typeof(Stage), true)]
	[CanEditMultipleObjects]
	public class DataSOInspector_Stage : DataSOInspector
	{
		protected override List<(string, Action)> GetCustomButtons()
		{
			return new List<(string, Action)>
			{
				("LoadStage", () =>
				{
					if (dataSO is WorldStage worldStage)
					{
						EditorManager.OpenScene(worldStage);
					}
				})
			};
		}
	}
}
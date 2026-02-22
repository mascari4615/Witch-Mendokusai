using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = nameof(EditorSettings), menuName = "WM/EditorSettings")]
	public class EditorSettings : ScriptableObject
	{
		[field: Header("_" + nameof(EditorSettings))]
		[field: SerializeField] public bool InitDataSODictOnCompile { get; private set; } = true;
		[field: SerializeField] public WorldStage StartWorldStage { get; private set; } = null;
	}

	public static class EditorSetting
	{
		private static EditorSettings data;
		public static EditorSettings Data
		{
			get
			{
				if (data == null)
					data = Resources.Load<EditorSettings>(nameof(EditorSettings));

				return data;
			}
			private set => data = value;
		}
	}
}
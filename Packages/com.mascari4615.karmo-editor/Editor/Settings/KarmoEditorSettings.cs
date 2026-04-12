using UnityEngine;
using System;
using System.Collections.Generic;
using KarmoLab.KarmoEditor;

namespace KarmoLab.KarmoEditor.Settings
{
	[CreateAssetMenu(fileName = nameof(KarmoEditorSettings), menuName = Define.CreateAssetMenuSettings + "/" + nameof(KarmoEditorSettings))]
	public class KarmoEditorSettings : ScriptableObject
	{
		[Header("Mutex Settings")]
		public string[] ApplicationMutexNames;

		[Header("Reset Fields (Reflection)")]
		public List<FieldResetInfo> ReflectionFieldResets;

		[Serializable]
		public class FieldResetInfo
		{
			public string FullTypeName; // e.g. KarmoToys.Main.KarmoToysApp, Assembly-CSharp
			public string FieldName;    // e.g. _appMutex
		}
	}
}

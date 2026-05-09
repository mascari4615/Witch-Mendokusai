using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[Serializable]
	public struct QuestNodeData
	{
		public QuestSO Quest;
		public Vector2 Position;
	}

	[CreateAssetMenu(fileName = "Chapter_", menuName = "WM/Variable/ChapterSO")]
	public class ChapterSO : DataSO
	{
		[field: SerializeField] public List<QuestNodeData> Nodes { get; private set; } = new();
	}
}

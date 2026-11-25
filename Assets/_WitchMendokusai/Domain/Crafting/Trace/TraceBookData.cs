using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WitchMendokusai.DomainSDK.Alchemy;

namespace WitchMendokusai
{
	// 조합판. 재료 줄, 마법이 숨은 칸, 귀찮음 문턱. Resources 에 하나
	// 수치와 마법 이름은 임시
	[CreateAssetMenu(fileName = nameof(TraceBookData), menuName = "WM/Crafting/TraceBook")]
	public class TraceBookData : ScriptableObject
	{
		[Serializable]
		public class MaterialRow
		{
			[Tooltip("연구할 재료")] public ItemData Item;
			[Min(1), Tooltip("자취 칸 하나를 밝히는 데 드는 횟수")] public int Need = 1;
		}

		[Serializable]
		public class MagicCell
		{
			[Tooltip("재료")] public ItemData Item;
			[Tooltip("자취")] public TraceType Trace;
			[Tooltip("마법 이름 (임시)")] public string Name;
		}

		public List<MaterialRow> Materials = new();
		public List<MagicCell> Magics = new();
		[Min(0), Tooltip("귀찮음이 이만큼 모이면 삐뚤어진 마법 하나. 0 이면 끔")] public int HasslesPerWarped = 4;

		private static TraceBookData current;

		public static TraceBookData Current
		{
			get
			{
				if (current == null)
					current = Resources.Load<TraceBookData>(nameof(TraceBookData));
				return current;
			}
		}

		public List<TraceMaterial> ToMaterials() => Materials.Select(row => new TraceMaterial(row.Item.ID, row.Need)).ToList();

		public List<TraceCombo> ToCombos() => Magics.Select(cell => new TraceCombo(cell.Item.ID, cell.Trace)).ToList();

		public string MagicName(int itemId, TraceType trace) => Magics.First(cell => cell.Item.ID == itemId && cell.Trace == trace).Name;
	}
}

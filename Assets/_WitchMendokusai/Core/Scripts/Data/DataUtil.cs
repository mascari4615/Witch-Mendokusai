using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WitchMendokusai
{
	public static class DataUtil
	{
		public static Color GetGradeColor(ItemGrade grade) => grade switch
		{
			ItemGrade.Common => Color.white,
			ItemGrade.Uncommon => new Color(43 / 255f, 123 / 255f, 1),
			ItemGrade.Rare => new Color(242 / 255f, 210 / 255f, 0),
			ItemGrade.Legendary => new Color(1, 0, 142 / 255f),
			_ => throw new ArgumentOutOfRangeException(nameof(ItemGrade), grade, null)
		};
	}
}
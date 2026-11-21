using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 판 시작 맵 오브젝트 배치 예산
	public partial class Dungeon
	{
		[PropertyOrder(130)][field: SerializeField, Tooltip("맵 오브젝트 배치 예산. 비우면 맵 오브젝트 없음")] public MapObjectBudget MapObjectBudget { get; private set; }

		// 입장 전 검사. 설정 오류는 판 전환 전에 예외
		public void ValidateMapObjects()
		{
			if (MapObjectBudget == null)
				return;
			List<string> errors = new();
			MapObjectBudget.Validate(errors);
			MapObjectSettings.Load().Validate(errors);
			MapObjectSettings.Load().ValidateBudget(MapObjectBudget, errors);
			if (errors.Count > 0)
				throw new System.InvalidOperationException($"{name} 맵 오브젝트 설정 오류: {string.Join(", ", errors)}");
		}
	}
}

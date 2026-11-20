using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 던전 한 곳의 판 시작 배치 예산. 크레딧으로 후보 오브젝트를 사듯 고름. 고르는 법은 MapObjectPlanner
	[CreateAssetMenu(fileName = nameof(MapObjectBudget), menuName = "WM/Variable/" + nameof(MapObjectBudget))]
	public class MapObjectBudget : ScriptableObject
	{
		[field: SerializeField, Tooltip("판당 배치 크레딧"), Min(0)] public int Credits { get; private set; } = 20;
		[field: SerializeField] public List<MapObjectData> Candidates { get; private set; } = new();
		[field: SerializeField, Tooltip("판 시드 갈래 값. 다른 판 시드 소비자와 겹치지 않게")] public int SeedSalt { get; private set; } = 7301;
		[field: SerializeField, Tooltip("뽑기 반복 상한. 비용 0 후보의 무한 반복 방지"), Min(1)] public int MaxPicks { get; private set; } = 32;

		public void Validate(List<string> errors)
		{
			if (Candidates.Count == 0)
				errors.Add($"{name}: 후보 없음");
			int minimumCost = 0;
			foreach (MapObjectData candidate in Candidates)
			{
				if (candidate == null)
				{
					errors.Add($"{name}: 빈 후보 칸");
					continue;
				}
				candidate.Validate(errors);
				minimumCost += candidate.PlacementCost * candidate.CountRange.x;
			}
			// 최소 개수를 다 못 사면 뒤 후보가 조용히 빠짐
			if (minimumCost > Credits)
				errors.Add($"{name}: 최소 개수 가격 합 {minimumCost} 이 크레딧 {Credits} 초과");
		}
	}
}

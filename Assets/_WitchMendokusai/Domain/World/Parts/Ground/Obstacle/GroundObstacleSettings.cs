using UnityEngine;

namespace WitchMendokusai
{
	// 던전 하나의 막는 지형 장애물 설정. Dungeon.GroundObstacles 에 연결, 비우면 장애물 없는 맵
	[CreateAssetMenu(fileName = nameof(GroundObstacleSettings), menuName = "WM/Variable/GroundObstacleSettings")]
	public sealed class GroundObstacleSettings : ScriptableObject
	{
		[field: SerializeField] public GroundObstacleRules Rules { get; private set; } = new();
	}
}

using UnityEngine;

namespace WitchMendokusai
{
	// 막는 지형 장애물 (나무 밑동, 바위). 비우면 장애물 없는 맵
	public partial class Dungeon
	{
		[PropertyOrder(131)][field: SerializeField, Tooltip("막는 지형 장애물 설정. 비우면 장애물 없는 맵")] public GroundObstacleSettings GroundObstacles { get; private set; }
	}
}

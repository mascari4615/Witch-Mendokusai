using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 직사각 한타 맵 (v1) — 바닥 plane + 박스콜라이더 4벽 경계 + 2팀 대칭 스폰(팀0 -Z / 팀1 +Z).
	/// 크기·벽·스폰 전부 인스펙터 노출(데이터 커스텀). 원형/장애물/레인 등 다른 구조는 별도 ArenaMapSO 서브클래스.
	/// </summary>
	[CreateAssetMenu(fileName = nameof(RectangleArenaMap), menuName = "WM/Arena/RectangleArenaMap")]
	public class RectangleArenaMap : ArenaMapSO
	{
		[field: Header("_" + nameof(RectangleArenaMap))]
		[field: Tooltip("X 축 폭(월드 단위).")]
		[field: SerializeField, Min(2f)] public float Width { get; private set; } = 24f;

		[field: Tooltip("Z 축 길이(월드 단위). 두 팀이 ±Z 로 마주봄.")]
		[field: SerializeField, Min(2f)] public float Length { get; private set; } = 36f;

		[field: SerializeField, Min(0.1f)] public float WallHeight { get; private set; } = 2f;
		[field: SerializeField, Min(0.1f)] public float WallThickness { get; private set; } = 0.5f;

		[field: Tooltip("팀당 출전 유닛 수(X 축 균등 배치).")]
		[field: SerializeField, Min(1)] public int PerTeam { get; private set; } = 3;

		[field: Tooltip("스폰을 경계벽에서 안쪽으로 들이는 거리.")]
		[field: SerializeField, Min(0f)] public float SpawnInset { get; private set; } = 5f;

		// 직사각 = 2팀 대칭(-Z / +Z) 고정. 3팀+ 는 별도 맵 구조(다른 ArenaMapSO).
		public override int TeamCount => 2;
		public override int SpawnsPerTeam => PerTeam;

		public override IReadOnlyList<Vector3> GetSpawns(int teamId)
		{
			List<Vector3> result = new();
			float halfLength = Length / 2f;
			float z = teamId == 0 ? -(halfLength - SpawnInset) : halfLength - SpawnInset;
			float usableWidth = Mathf.Max(0f, Width - (SpawnInset * 2f));

			for (int i = 0; i < PerTeam; i++)
			{
				float x;
				if (PerTeam == 1)
				{
					x = 0f;
				}
				else
				{
					x = -(usableWidth / 2f) + (usableWidth * (i / (float)(PerTeam - 1)));
				}
				result.Add(new Vector3(x, 0f, z));
			}
			return result;
		}

		public override void Build(Transform root)
		{
			float halfWidth = Width / 2f;
			float halfLength = Length / 2f;

			GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
			ground.name = "Ground";
			ground.transform.SetParent(root, false);
			ground.transform.localPosition = Vector3.zero;
			// Plane = 10x10 유닛 @ scale 1 → Width/Length 에 맞춰 스케일.
			ground.transform.localScale = new Vector3(Width / 10f, 1f, Length / 10f);

			GameObject walls = new GameObject("Walls");
			walls.transform.SetParent(root, false);

			BuildWall(walls.transform, "Wall_North", new Vector3(0f, WallHeight / 2f, halfLength), new Vector3(Width, WallHeight, WallThickness));
			BuildWall(walls.transform, "Wall_South", new Vector3(0f, WallHeight / 2f, -halfLength), new Vector3(Width, WallHeight, WallThickness));
			BuildWall(walls.transform, "Wall_East", new Vector3(halfWidth, WallHeight / 2f, 0f), new Vector3(WallThickness, WallHeight, Length));
			BuildWall(walls.transform, "Wall_West", new Vector3(-halfWidth, WallHeight / 2f, 0f), new Vector3(WallThickness, WallHeight, Length));
		}

		private void BuildWall(Transform parent, string wallName, Vector3 localPosition, Vector3 localScale)
		{
			GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
			wall.name = wallName;
			wall.transform.SetParent(parent, false);
			wall.transform.localPosition = localPosition;
			wall.transform.localScale = localScale;
		}
	}
}

using UnityEngine;

namespace WitchMendokusai
{
	// 잔해 하나를 띄울 때 넘기는 값. 모양과 재질과 움직임 수치는 부르는 쪽 (상자, 몬스터 프로필) 이 정함
	public struct DebrisSpawn
	{
		public Mesh Mesh;
		public Material Material;
		public Vector3 Position;
		public Quaternion Rotation;
		public Vector3 Velocity;
		public Vector3 SpinAxis;
		public float SpinDegrees;
		public float Size;
		// 기울어진 채 날 때 바닥에서 띄울 거리 (크기 1 기준)
		public float FlyClearance;
		// 누웠을 때 바닥에서 중심까지 거리 (크기 1 기준)
		public float RestClearance;
		public float Gravity;
		public float Bounce;
		public float GroundHeight;
		// 재질 색에 곱함. 흰색이면 그대로 묶어 그림
		public Color Tint;
		public bool HasTint;
		// 누운 뒤 바닥 캔버스에 남길 자국. 0 이면 없음
		public float StainRadius;
		public Color StainColor;
	}

	// 잔해의 상태. 날기 -> 눕기 -> (유지) -> 사라짐
	public enum DebrisState
	{
		Idle = 0,
		Flying = 1,
		Settling = 2,
		Resting = 3,
		Vanishing = 4,
	}

	// 풀에서 돌려 쓰는 잔해 한 칸. DebrisField 만 만짐
	public sealed class DebrisPiece
	{
		public Transform Transform;
		public MeshFilter Filter;
		public MeshRenderer Renderer;
		public DebrisSpawn Spawn;
		public DebrisState State;
		public Vector3 Velocity;
		public float StateTime;
		// 누워 있던 시간의 합. 밀려서 다시 날아도 줄지 않음 (계속 밟아도 영원히 남지 않게)
		public float LingerAge;
		public bool HasRested;
		public Quaternion SettleFrom;
		public Quaternion SettleTo;
		public Vector3 SettleFromPosition;
		public int RestingIndex = -1;
	}
}

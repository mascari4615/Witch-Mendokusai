using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 판의 바닥을 만들 때 쓰는 한 조각 지식 — <b>유니티 Plane 프리미티브는 scale 1 에서 10x10 유닛</b>이다.
	///
	/// ★ 왜 따로 두나: 투기장(`RectangleArenaMap`)과 개척(`TowerDefenseTerrainView`)이 각자
	///   `new Vector3(width / 10f, 1f, length / 10f)` 를 적어두고 있었다. 저 10 은 우리가 정한 수가
	///   아니라 <b>유니티가 정한 수</b>다 — 누군가 Plane 대신 Quad 로 바꾸는 날 한쪽만 고쳐지면
	///   그 판만 바닥이 열 배 어긋난다. 그 오차는 「지형이 좀 이상한데」로 보이지 에러로는 안 뜬다.
	/// ★ 프리미티브를 *만드는* 일까지는 안 가져온다: 개척은 콜라이더·머티리얼을 손보는 자기 래퍼
	///   (`TowerDefenseVisuals.Primitive`)를 쓰고 투기장은 맨 `CreatePrimitive` 를 쓴다.
	///   여기서 묶어야 할 것은 만드는 방법이 아니라 <b>치수 환산</b> 하나다.
	/// </summary>
	public static class GroundPlane
	{
		/// <summary> 유니티 Plane 프리미티브의 한 변(유닛) @ scale 1. </summary>
		public const float PRIMITIVE_UNITS = 10f;

		/// <summary> 원하는 가로·세로(월드 유닛)를 Plane 의 localScale 로 환산. </summary>
		public static Vector3 ScaleFor(float width, float length)
		{
			return new Vector3(width / PRIMITIVE_UNITS, 1f, length / PRIMITIVE_UNITS);
		}
	}
}

using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 판을 그릴 도형 하나 — 만들자마자 <b>이 프로젝트가 실제로 쓰는 셰이더</b>를 입혀 돌려준다.
	///
	/// ★ 왜 맨 `GameObject.CreatePrimitive` 를 쓰면 안 되나 (TASK-WM-208): 그게 붙여주는 기본 재질은
	///   <b>빌트인 렌더러용</b>이라 URP 에서 제대로 안 나온다. 에디터에서는 그럭저럭 보이지만
	///   <b>빌드에서 통째로 밋밋한 회색</b>이 된다 — 즉 <b>에디터만 보고 있으면 영원히 안 걸린다.</b>
	///   개척이 먼저 겪었고(사용자 실증), 투기장 맵은 그 교훈이 안 퍼져 맨 호출을 쓰고 있었다.
	///
	/// 만드는 방법만 여기 모은다 — 콜라이더·머티리얼 세부를 더 손대는 게임은 자기 래퍼를 덧대면 된다
	/// (개척의 `TowerDefenseVisuals` 가 그렇다).
	/// </summary>
	public static class CombatPrimitive
	{
		public static GameObject Create(PrimitiveType type, bool unlit = false)
		{
			GameObject primitive = GameObject.CreatePrimitive(type);

			Renderer renderer = primitive.GetComponent<Renderer>();
			if (renderer != null)
				renderer.sharedMaterial = new Material(FindShader(unlit ? CombatShaderNames.Unlit : CombatShaderNames.Lit));

			return primitive;
		}

		/// <summary>
		/// 이름으로 셰이더를 찾되 <b>못 찾으면 조용히 넘어가지 않는다</b> —
		/// 무음 폴백이 바로 이 병의 정체였다(화면은 멀쩡한 척하고 아무도 모른다).
		/// </summary>
		private static Shader FindShader(string shaderName)
		{
			Shader shader = Shader.Find(shaderName);
			if (shader != null)
				return shader;

			Debug.LogError($"{nameof(CombatPrimitive)}: 셰이더 「{shaderName}」 를 못 찾음 — 빌드에 안 실렸다는 뜻이다"
				+ "(그래픽 설정의 「항상 포함할 셰이더」 확인). 판이 회색이 된다.");
			return Shader.Find(CombatShaderNames.LegacyFallback);
		}
	}
}

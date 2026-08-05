using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 개척 판의 *보이는 것*을 만드는 한 곳 (TASK-WM-194).
	///
	/// ★ 왜 생겼나: 판의 땅·안개·암반·벽·함정은 전부 코드가 즉석에서 세우는 도형이다.
	///   `GameObject.CreatePrimitive` 가 붙여주는 기본 재질은 *옛 렌더러용*이라, 에디터에서는
	///   그럭저럭 보이지만 **빌드에서는 통째로 밋밋한 회색**이 된다(사용자 실증: "개척 진입하니까
	///   맵에 회색 밖에 안 보이는데"). 에디터만 보고 있으면 영원히 안 걸리는 종류다.
	/// ★ 그래서 도형을 만드는 입구를 하나로 모으고, 그 자리에서 *이 프로젝트가 실제로 쓰는 셰이더*를
	///   붙인다. 이름으로 찾는 셰이더는 빌드에 안 실릴 수 있으므로 「항상 포함할 셰이더」에도
	///   등록돼 있어야 한다 — 그 약속은 <see cref="TowerDefenseShaderNames"/> 가 이름으로 못 박고,
	///   에디터 시험이 그 목록을 대조한다(빌드를 굽지 않고도 이 병을 잡기 위해).
	/// </summary>
	public static class TowerDefenseVisuals
	{
		/// <summary> 도형 하나 — 만들자마자 이 프로젝트 셰이더를 입혀서 돌려준다. </summary>
		public static GameObject Primitive(PrimitiveType type, bool unlit = false)
		{
			GameObject primitive = GameObject.CreatePrimitive(type);
			Renderer renderer = primitive.GetComponent<Renderer>();
			if (renderer != null)
				renderer.sharedMaterial = unlit ? CreateUnlit() : CreateLit();
			return primitive;
		}

		/// <summary> 빛을 받는 재질(땅·암반·벽). </summary>
		public static Material CreateLit()
		{
			return new Material(FindShader(TowerDefenseShaderNames.Lit));
		}

		/// <summary> 빛과 무관한 재질(안개·표시용 판때기). </summary>
		public static Material CreateUnlit()
		{
			return new Material(FindShader(TowerDefenseShaderNames.Unlit));
		}


		/// <summary>
		/// 반투명으로 바꾼다 — 길 표시·안개처럼 *덮되 가리지 않아야* 하는 것들이 쓴다.
		/// ★ 같은 함수가 두 곳에 복제돼 있었다(길 표시 / 안개). 한쪽만 고치면 화면이 갈라진다.
		/// </summary>
		public static void MakeTransparent(Material material)
		{
			material.SetFloat("_Surface", 1f); // 1 = Transparent
			material.SetOverrideTag("RenderType", "Transparent");
			material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
			material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
			material.SetInt("_ZWrite", 0);
			material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
			material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
		}

		/// <summary>
		/// 바닥에 붙는 표시(길 안내·배치 미리보기)로 만든다 — 바닥은 덮되 *아무것도 자르지 않는다*.
		///
		/// ★ 왜 필요한가: 이런 판때기는 불투명하게 그려야 색이 제대로 나오는데, 불투명한 것은
		///   「여기까지가 앞이다」라는 깊이를 남긴다. 인형·마수 그림은 그 뒤에 그려지면서 그 깊이에
		///   걸려 *잘려 나간다*. 개척은 위에서 내려다보는 시점이라 인형 그림이 거의 눕고, 그래서
		///   바닥 몇 cm 위의 판때기가 인형 몸통 한가운데를 가로지른다(사용자 실측: "길이 공중에 떠
		///   있어서 유닛이 중간에 짤린다"). 그리는 *순서*를 고쳐도 이건 안 없어진다 — 깊이의 문제다.
		/// ★ 왜 한 함수인가: 같은 처리가 길 안내와 배치 미리보기 두 곳에 필요하다. 한쪽만 고치면
		///   화면이 갈라진다(이 파일이 이미 한 번 겪은 병 — MakeTransparent 참고).
		/// </summary>
		/// <param name="aboveFog">
		/// 안개보다 나중에 그릴 것인가. 길 안내는 *안개에 가려져야 맞다* — 안 가본 땅의 길목이
		/// 보이면 안개가 뜻을 잃는다. 반대로 **커서 표시는 무조건 보여야 한다**(사용자 실측:
		/// "안개가 마우스 마커를 가리는데, 마커는 보여야죠") — 지금 어디를 가리키는지가 안 보이면
		/// 안개 낀 땅에는 아예 지을 수가 없다. 「가려질 것」과 「항상 보일 것」이 갈리는 자리다.
		/// </param>
		public static void MakeFloorDecal(Material material, bool aboveFog = false)
		{
			material.SetFloat("_ZWrite", 0f);
			if (aboveFog == false)
			{
				// 바닥 바로 다음 줄 — 바닥을 덮고, 안개·스프라이트보다는 먼저(길은 안개에 가려져야 맞다).
				material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry + 1;
				return;
			}

			// ★ 커서 표시는 **맨 마지막에, 깊이 검사 없이** 그린다 (사용자 실측 3회: "여전히 마커 가려짐").
			//   앞서 두 번은 「안개보다 조금 뒤 순번」으로만 밀었는데 그걸로는 안 됐다 — 순번을 조금 미루는
			//   것과 「무엇에도 안 가린다」는 다른 요구다. 진짜 필요한 건 *가장 마지막 줄* + *깊이 무시*다.
			//   커서가 안 보이면 안개 낀 땅에는 아예 지을 수가 없으므로, 여기서는 다른 모든 규칙을 이긴다.
			material.SetOverrideTag("RenderType", "Transparent");
			material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
			material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
			material.SetFloat("_Surface", 1f);
			material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
			material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
			material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay;
		}

		/// <summary>
		/// 이름으로 셰이더를 찾되, 못 찾으면 *조용히 회색으로 넘어가지 않는다*.
		/// 무음 폴백이 바로 이번 병의 정체였다 — 화면은 멀쩡한 척하고 아무도 모른다.
		/// </summary>
		private static Shader FindShader(string shaderName)
		{
			Shader shader = Shader.Find(shaderName);
			if (shader != null)
				return shader;

			Debug.LogError($"{nameof(TowerDefenseVisuals)}: 셰이더 「{shaderName}」 를 못 찾음 — "
				+ "빌드에 안 실렸다는 뜻이다(그래픽 설정의 「항상 포함할 셰이더」 확인). 화면이 회색이 된다.");
			return Shader.Find(TowerDefenseShaderNames.LegacyFallback);
		}
	}

	/// <summary>
	/// 개척이 이름으로 찾는 셰이더들 — 여기 있는 것은 *전부* 「항상 포함할 셰이더」에 등록돼 있어야 한다.
	/// 목록을 코드 한 곳에 모아두면 에디터 시험이 설정과 대조할 수 있다(빌드 없이 잡는 유일한 길).
	/// </summary>
	public static class TowerDefenseShaderNames
	{
		public const string Lit = "Universal Render Pipeline/Lit";
		public const string Unlit = "Universal Render Pipeline/Unlit";
		public const string OverlayLine = "WM/TowerDefenseOverlayLine";

		/// <summary> 정말 아무것도 없을 때의 마지막 수단 — 보이기는 한다. </summary>
		public const string LegacyFallback = "Sprites/Default";

		/// <summary> 빌드에 반드시 실려야 하는 것들. </summary>
		public static readonly string[] MustBeIncluded = { Lit, Unlit, OverlayLine };
	}
}

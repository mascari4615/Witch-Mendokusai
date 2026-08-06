using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 공용 전투 셰이더(`CombatShaderNames`)가 *빌드에 실리는가* — 에디터에서만 도는 시험이지만
	/// 잡는 것은 <b>빌드에서만 나는 병</b>이다.
	///
	/// ★ 왜 개척 시험이 있는데 또 두나 (TASK-WM-208): 개척 시험은 개척 목록을 본다.
	///   지금은 그 목록이 공용 상수로 포워딩돼 있어 결과적으로 같은 문자열을 지키지만,
	///   **그건 개척이 계속 그 이름을 쓴다는 우연에 기대는 것**이다. 개척이 자기 셰이더를 바꾸거나
	///   목록에서 빼는 순간 투기장 몫이 조용히 무방비가 된다 — 오늘 하루 파낸 실패 모양 그대로다.
	///   공용 목록은 공용 시험이 지킨다.
	/// </summary>
	public class CombatShaderInclusionTests
	{
		private static HashSet<string> AlwaysIncludedShaderNames()
		{
			HashSet<string> included = new();
			SerializedObject graphics = new(GraphicsSettings.GetGraphicsSettings());
			SerializedProperty always = graphics.FindProperty("m_AlwaysIncludedShaders");

			Assert.IsNotNull(always, "그래픽 설정에서 「항상 포함할 셰이더」 목록을 못 찾았다.");

			for (int index = 0; index < always.arraySize; index++)
			{
				Shader shader = always.GetArrayElementAtIndex(index).objectReferenceValue as Shader;
				if (shader != null)
					included.Add(shader.name);
			}
			return included;
		}

		[Test]
		public void 공용_전투_셰이더는_전부_빌드에_실린다()
		{
			// 「0개라 통과」를 막는다 — 목록이 비면 지킬 게 없는 것이지 안전한 게 아니다.
			Assert.IsNotEmpty(CombatShaderNames.MustBeIncluded, "공용 셰이더 목록이 비었다(회귀 신호).");

			HashSet<string> included = AlwaysIncludedShaderNames();
			foreach (string shaderName in CombatShaderNames.MustBeIncluded)
			{
				Assert.IsTrue(included.Contains(shaderName),
					$"셰이더 「{shaderName}」 이 「항상 포함할 셰이더」에 없다 — 빌드에서 판이 회색이 된다. "
					+ "Project Settings > Graphics 에 추가할 것.");
			}
		}

		[Test]
		public void 목록에_적힌_공용_셰이더는_실제로_존재한다()
		{
			// 이름 오타는 에디터에서도 조용하다 — 폴백이 대신 뜨고 아무도 모른다.
			foreach (string shaderName in CombatShaderNames.MustBeIncluded)
				Assert.IsNotNull(Shader.Find(shaderName), $"셰이더 「{shaderName}」 이 프로젝트에 없다(이름 오타?).");
		}
	}
}

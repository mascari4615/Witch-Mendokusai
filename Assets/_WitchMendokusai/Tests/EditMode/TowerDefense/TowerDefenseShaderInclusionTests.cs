using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 개척이 이름으로 찾는 셰이더가 *빌드에 실리는가* — 에디터에서만 도는 시험이지만 잡는 것은
	/// **빌드에서만 나는 병**이다.
	///
	/// ★ 왜 필요한가: 에디터는 프로젝트의 모든 셰이더를 들고 있어서 `Shader.Find` 가 항상 성공한다.
	///   빌드는 *쓰인다고 판단된 것*만 챙기므로, 코드가 이름으로만 찾는 셰이더는 통째로 빠진다.
	///   그러면 판의 땅·안개·암반이 전부 밋밋한 회색이 된다(사용자 실증 2026-08-03:
	///   "개척 진입하니까 맵에 회색 밖에 안 보이는데"). 33분짜리 빌드를 굽지 않고 잡을 유일한 길이
	///   「이름 목록 ↔ 항상 포함할 셰이더 설정」 대조다.
	/// </summary>
	public class TowerDefenseShaderInclusionTests
	{
		[Test]
		public void 개척이_이름으로_찾는_셰이더는_전부_빌드에_실린다()
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

			foreach (string shaderName in TowerDefenseShaderNames.MustBeIncluded)
			{
				Assert.IsTrue(included.Contains(shaderName),
					$"셰이더 「{shaderName}」 이 「항상 포함할 셰이더」에 없다 — 빌드에서 판이 회색이 된다. "
					+ "Project Settings > Graphics 에 추가할 것.");
			}
		}

		[Test]
		public void 목록에_적힌_셰이더는_실제로_존재한다()
		{
			// 이름 오타는 에디터에서도 조용하다 — 폴백이 대신 뜨고 아무도 모른다.
			foreach (string shaderName in TowerDefenseShaderNames.MustBeIncluded)
				Assert.IsNotNull(Shader.Find(shaderName), $"셰이더 「{shaderName}」 이 프로젝트에 없다(이름 오타?).");
		}
	}
}

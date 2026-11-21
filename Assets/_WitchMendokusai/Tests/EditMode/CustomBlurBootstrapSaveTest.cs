using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	// 도메인 재로드마다 도는 블러 연결 검사가 전체 SaveAssets 로 남의 미저장 자산까지 저장하던 것 (2026-11-21 MOIS Stage_TempCombat 저장 오류 18회)
	// 옛 코드에서 실패하면 열린 에디터의 dirty 자산을 전부 저장하므로 사용자 에디터에서 빨간 상태로 돌리지 않음
	public class CustomBlurBootstrapSaveTest
	{
		private const string RT_PATH = "Assets/_WitchMendokusai/Core/Resources/Rendering/CustomBlurOutput.renderTexture";
		private const string PROBE_PATH = "Assets/_WitchMendokusai/Tests/EditMode/__BlurBootstrapSaveProbe.mat";
		private Material probe;

		[SetUp]
		public void SetUp()
		{
			probe = new Material(Shader.Find("Hidden/InternalErrorShader"));
			AssetDatabase.CreateAsset(probe, PROBE_PATH);
		}

		[TearDown]
		public void TearDown() => AssetDatabase.DeleteAsset(PROBE_PATH);

		[Test]
		public void Wiring_check_does_not_save_unrelated_dirty_asset()
		{
			// 출력 텍스처가 없으면 검사가 저장 전에 돌아가 옛 코드에서도 통과함
			Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<RenderTexture>(RT_PATH), "전제: 블러 출력 텍스처");
			probe.renderQueue = 2100;
			EditorUtility.SetDirty(probe);

			MethodInfo ensure = typeof(CustomBlurOutputBootstrapMenu).GetMethod("EnsureRendererWired", BindingFlags.NonPublic | BindingFlags.Static);
			Assert.IsNotNull(ensure, "EnsureRendererWired 이름 바뀜");
			ensure.Invoke(null, null);

			Assert.IsTrue(EditorUtility.IsDirty(probe), "연결 검사가 관계없는 자산을 저장함");
		}
	}
}

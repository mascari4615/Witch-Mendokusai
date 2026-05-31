using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.Sandbox
{
	// WM 기능 격리 프리뷰 갤러리. 등록된 데모를 분류별로 나열 + 한 클릭 프리뷰(에디트 모드, Play 미사용).
	public sealed class SandboxGalleryWindow : EditorWindow
	{
		private Vector2 scroll;

		[MenuItem("WM/Sandbox Gallery")]
		public static void Open()
		{
			SandboxGalleryWindow window = GetWindow<SandboxGalleryWindow>("WM Sandbox");
			window.minSize = new Vector2(320f, 240f);
		}

		private void OnGUI()
		{
			EditorGUILayout.HelpBox(
				"기능을 격리된 미니 무대(빈 씬)에서 라이브 프리뷰. Play 미사용 → WM 부트스트랩(World 진입)·다른 세션 무방해. " +
				"프리뷰는 Scene View 에 뜬다(⏱ = 시간 흐름 데모).", MessageType.Info);

			using (new EditorGUILayout.HorizontalScope())
			{
				if (GUILayout.Button("새로고침"))
				{
					Repaint();
				}

				using (new EditorGUI.DisabledScope(SandboxStage.HasActiveStage == false))
				{
					if (GUILayout.Button("무대 닫기"))
					{
						SandboxStage.Close();
					}
				}
			}

			EditorGUILayout.Space();
			scroll = EditorGUILayout.BeginScrollView(scroll);

			IReadOnlyList<ISandboxDemo> demos = SandboxRegistry.Discover();
			if (demos.Count == 0)
			{
				EditorGUILayout.LabelField("등록된 데모 없음 — ISandboxDemo 를 구현하세요.");
			}

			string currentCategory = null;
			foreach (ISandboxDemo demo in demos)
			{
				if (demo.Category != currentCategory)
				{
					currentCategory = demo.Category;
					EditorGUILayout.Space(4f);
					EditorGUILayout.LabelField(currentCategory, EditorStyles.boldLabel);
				}

				using (new EditorGUILayout.HorizontalScope())
				{
					string suffix = demo is ISandboxAnimatedDemo ? "  ⏱" : string.Empty;
					EditorGUILayout.LabelField($"  {demo.Title}{suffix}");
					if (GUILayout.Button("프리뷰", GUILayout.Width(80f)))
					{
						SandboxStage.Open(demo);
					}
				}
			}

			EditorGUILayout.EndScrollView();
		}
	}
}

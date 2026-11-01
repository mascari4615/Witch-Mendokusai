using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// 씬 로드 및 소유자가 등록한 활성 UIDocument의 텍스트 입력 포커스 검사.
	// IsTyping GameCondition용. 동적 문서는 소유자가 등록
	public static class UIToolkitFocus
	{
		private static readonly HashSet<UIDocument> documents = new();

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void Reset()
		{
			documents.Clear();
			SceneManager.sceneLoaded -= RegisterSceneDocuments;
			SceneManager.sceneLoaded += RegisterSceneDocuments;
		}

		private static void RegisterSceneDocuments(Scene scene, LoadSceneMode mode)
		{
			documents.RemoveWhere(document => document == null);
			foreach (GameObject root in scene.GetRootGameObjects())
				foreach (UIDocument document in root.GetComponentsInChildren<UIDocument>(true))
					Register(document);
		}

		// 동적 문서의 소유자가 생성/해제 때 등록. 프레임마다 씬 전체 검색 제거
		public static void Register(UIDocument document) => documents.Add(document);
		public static void Unregister(UIDocument document) => documents.Remove(document);

		public static bool IsAnyTextFieldFocused()
		{
			foreach (UIDocument document in documents)
			{
				if (document == null || document.isActiveAndEnabled == false)
					continue;
				VisualElement root = document.rootVisualElement;
				if (root == null)
					continue;

				IPanel panel = root.panel;
				if (panel == null)
					continue;

				FocusController focusController = panel.focusController;
				if (focusController == null)
					continue;

				VisualElement focused = focusController.focusedElement as VisualElement;
				while (focused != null)
				{
					if (focused is TextField)
						return true;
					focused = focused.parent;
				}
			}

			return false;
		}
	}
}


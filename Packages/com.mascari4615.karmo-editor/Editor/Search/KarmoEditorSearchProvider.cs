using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
using KarmoLab.KarmoEditor.DebugUtils;
using KarmoLab.KarmoEditor.Builder;
using KarmoLab.KarmoEditor.Settings;

namespace KarmoLab.KarmoEditor.Search
{
	/// <summary>
	/// Unity Quick Search (Ctrl+K) 통합을 위한 프로바이더
	/// </summary>
	public static class KarmoEditorSearchProvider
	{
		private const string ProviderId = "karmolab";
		private const string FilterId = "kl:";

		[SearchItemProvider]
		public static SearchProvider CreateProvider()
		{
			return new SearchProvider(ProviderId, "KarmoLab")
			{
				filterId = FilterId,
				priority = 999, // 검색 결과 상단 노출
				fetchItems = (context, items, provider) =>
				{
					var searchPattern = context.searchQuery;
					if (string.IsNullOrEmpty(searchPattern)) return null;

					var actions = new List<SearchItem>();

					AddAction(actions, provider, "Kill App Mutex", "실행 중인 앱 뮤텍스 강제 종료 및 정적 필드 초기화", "d_DebuggerAttached", KarmoDebugMenu.KillMutex);
					AddAction(actions, provider, "Open Build Helper", "빌드 및 배포 도구 창 열기", "BuildSettings.Editor.Small", KarmoBuildWindow.ShowWindow);
					AddAction(actions, provider, "Open Karmo Settings", "Project Settings 내 KarmoLab 설정으로 이동", "SettingsIcon", () => SettingsService.OpenProjectSettings(Define.ProjectSettingsPath));
					AddAction(actions, provider, "Create KarmoEditorSettings", "새로운 KarmoEditorSettings 에셋 생성", "ScriptableObject Icon", KarmoSettingsUtility.CreateKarmoSettings);

					// 검색어 필터링
					var filtered = actions.Where(i => i.label != null && i.label.ToLower().Contains(searchPattern.ToLower())).ToList();
					items.AddRange(filtered);

					return null;
				}
			};
		}

		private static void AddAction(List<SearchItem> items, SearchProvider provider, string label, string description, string iconName, Action action)
		{
			// id와 label을 인자로 전달. userData(마지막 인자)에 action 저장
			var item = provider.CreateItem(label, label, description, EditorGUIUtility.IconContent(iconName).image as Texture2D, action);
			items.Add(item);
		}

		[SearchActionsProvider]
		public static IEnumerable<SearchAction> CreateActions()
		{
			// 더블 클릭 또는 엔터 시 실행될 기본 액션
			yield return new SearchAction(ProviderId, "execute", null, "Execute Action")
			{
				handler = (item) =>
				{
					// SearchItem.data 필드에서 action을 직접 가져옴 (Unity 2021+ Search API 기준)
					if (item.data is Action action)
					{
						action.Invoke();
					}
				}
			};
		}
	}
}

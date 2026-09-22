using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 컷씬 트랙 (자막, 암전) 의 디렉터 바인딩이 프리팹에 살아 있는지 (change.wm-code-structure 8단계, 2026-09-22).
	///
	/// ★ 왜: 믹서 둘이 UIManager 를 매 프레임 찾던 것을 트랙 바인딩 (playerData) 으로 교체.
	///   바인딩은 `[Canvas] CutScene.prefab` 의 PlayableDirector 안 한 줄. 트랙을 다시 만들거나
	///   프리팹을 되돌리면 자막과 암전이 조용히 안 나옴 (믹서는 null 이면 그냥 return). 여기서 탐지.
	/// </summary>
	public sealed class CutsceneTrackBindingTests
	{
		private const string PREFAB_PATH = "Assets/_WitchMendokusai/Core/UI/Cutscene/[Canvas] CutScene.prefab";

		[Test]
		public void CutScenePrefab_BindsSubtitleAndFadeTracksToItsOwnModule()
		{
			GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
			Assert.IsNotNull(prefab, PREFAB_PATH + " 없음");

			PlayableDirector director = prefab.GetComponentInChildren<PlayableDirector>(true);
			Assert.IsNotNull(director, "컷씬 프리팹에 PlayableDirector 없음");

			CutSceneModule module = prefab.GetComponentInChildren<CutSceneModule>(true);
			Assert.IsNotNull(module, "컷씬 프리팹에 CutSceneModule 없음");

			TimelineAsset timeline = director.playableAsset as TimelineAsset;
			Assert.IsNotNull(timeline, "디렉터에 TimelineAsset 없음");

			bool subtitleBound = false;
			bool fadeBound = false;
			foreach (TrackAsset track in timeline.GetOutputTracks())
			{
				if (track is SubtitleTrack)
				{
					Object bound = director.GetGenericBinding(track);
					Assert.AreSame(module.Subtitle, bound as TextMeshProUGUI, "Subtitle Track 이 이 프리팹의 자막 글자에 안 묶임");
					subtitleBound = true;
				}
				else if (track is FadeTrack)
				{
					Object bound = director.GetGenericBinding(track);
					Assert.AreSame(module.FadeCanvasGroup, bound as CanvasGroup, "Fade Track 이 이 프리팹의 암전 CanvasGroup 에 안 묶임");
					fadeBound = true;
				}
			}

			Assert.IsTrue(subtitleBound, "타임라인에 SubtitleTrack 없음");
			Assert.IsTrue(fadeBound, "타임라인에 FadeTrack 없음");
		}
	}
}

using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Playables;

namespace WitchMendokusai
{
	[TrackBindingType(typeof(CanvasGroup))]
	[TrackClipType(typeof(FadeClip))]
	public class FadeTrack : TrackAsset
	{
		public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
		{
			return ScriptPlayable<FadeTackMixer>.Create(graph, inputCount);
		}
	}
}

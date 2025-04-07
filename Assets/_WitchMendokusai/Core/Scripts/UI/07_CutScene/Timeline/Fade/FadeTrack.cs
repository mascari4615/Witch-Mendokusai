using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Timeline;
using TMPro;
using UnityEngine.Playables;

namespace WitchMendokusai
{
	// [TrackBindingType(typeof(TextMeshProUGUI))]
	[TrackClipType(typeof(FadeClip))]
	public class FadeTrack : TrackAsset
	{
		public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
		{
			return ScriptPlayable<FadeTackMixer>.Create(graph, inputCount);
		}
	}
}
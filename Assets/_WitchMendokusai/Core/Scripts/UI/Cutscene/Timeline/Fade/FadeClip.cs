using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace WitchMendokusai
{
	public class FadeClip : PlayableAsset
	{
		// public float alpha;

		public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
		{
			var playable = ScriptPlayable<FadeBehaviour>.Create(graph);

			FadeBehaviour fadeBehaviour = playable.GetBehaviour();
			// fadeBehaviour.alpha = alpha;

			return playable;
		}
	}
}
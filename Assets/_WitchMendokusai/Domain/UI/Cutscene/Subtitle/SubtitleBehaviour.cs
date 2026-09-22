using UnityEngine.Playables;

namespace WitchMendokusai
{
	// 클립 데이터만. 화면 쓰기는 SubtitleTackMixer 하나 (같은 프레임에 둘이 쓰면 마지막 것이 우선)
	public class SubtitleBehaviour : PlayableBehaviour
	{
		public string subtitleText;
	}
}

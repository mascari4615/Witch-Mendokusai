using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>마우스나 포커스 아래 칸만 원래 밝기, 같은 줄의 나머지는 살짝 흐리게. UIFxProfile 의 dim 칸</summary>
	[CreateAssetMenu(fileName = "Dim_0_Siblings", menuName = "WM/UI/Focus/Dim")]
	public sealed class UIDimStyle : ScriptableObject
	{
		[Range(0f, 1f)] public float dimOpacity = 0.55f;
		public float seconds = 0.15f;
	}
}

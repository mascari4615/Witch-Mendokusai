using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>고른 칸이 커지고 바탕과 글자 색이 뒤집힘, 누르면 한 번 번쩍. UIFxProfile 의 select 칸</summary>
	[CreateAssetMenu(fileName = "Select_0_Invert", menuName = "WM/UI/Focus/Select")]
	public sealed class UISelectStyle : ScriptableObject
	{
		public float scale = 1.08f;
		public float seconds = 0.14f;
		[Tooltip("끝에서 넘쳤다 돌아오는 세기")]
		public float overshoot = 1.6f;
		[Tooltip("켜면 바탕과 글자 색을 아래 색으로")]
		public bool invert = true;
		public Color fillColor = new(0.95f, 0.93f, 0.99f, 1f);
		public Color textColor = new(0.1f, 0.08f, 0.15f, 1f);
		[Tooltip("누를 때 번쩍 색과 시간")]
		public Color flashColor = new(1f, 0.84f, 0.92f, 1f);
		public float flashSeconds = 0.06f;
	}
}

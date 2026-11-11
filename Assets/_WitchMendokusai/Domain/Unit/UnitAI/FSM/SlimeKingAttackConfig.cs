using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(menuName = "WM/Slime King Attack Config")]
	public sealed class SlimeKingAttackConfig : ScriptableObject
	{
		[Min(0f)] public float DashPreDelay = 0.2f;
		[Min(0f)] public float DashPostDelay = 0.5f;
		[Min(0f)] public float ApproachMinimumSpeed = 3f;
		[Min(0.01f)] public float MinimumEscapeSpeed = 0.96f;
		[Min(0.01f)] public float EscapeAcceleration = 100f;
		[Min(0f)] public float EscapeReactionSeconds = 0.5f;
		[Min(0f)] public float EscapePadding = 0.2f;
		[Min(0f)] public float MaximumFleeSpeed = 4f;
		[Min(0.01f)] public float MaximumDashDuration = 2.5f;
		[Min(0.01f)] public float MaximumDashDistance = 37.5f;
		public Material TelegraphMaterial;
		public Color PrepareColor = Color.yellow;
		public Color RecoveryColor = Color.cyan;
	}
}

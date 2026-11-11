using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(menuName = "WM/Survival Map Challenge")]
	public sealed class SurvivalMapChallengeConfig : ScriptableObject
	{
		public bool EnableSite = true;
		public bool EnableCrossing = true;
		public Material MarkerMaterial;
		public LayerMask GroundMask;
		public LayerMask ObstacleMask;
		[Min(1)] public int SitePlacementAttempts = 8;
		[Min(0f)] public float SiteDistance = 11f;
		[Min(0.01f)] public float MarkerWidth = 0.2f;
		[Min(0f)] public float GroundProbeHeight = 15f;
		[Min(0f)] public float GroundLift = 0.02f;
		public Color SiteColor = Color.yellow;
		public Color CrossingColor = Color.cyan;
		[Min(0f)] public float CrossingEventTime = 240f;
		[Min(0.01f)] public float MinimumEscapeSpeed = 0.96f;
		[Range(0.01f, 1f)] public float EscapeSpeedMultiplier = 0.4f;
		[Min(0.01f)] public float EscapeAcceleration = 100f;
		[Min(0f)] public float ReactionSeconds = 0.5f;
		[Min(0f)] public float EscapePadding = 0.2f;
		[Min(0.01f)] public float CrossingSpacing = 0.5f;
		[Min(0.01f)] public float CrossingLifetime = 11f;
		[Min(0.01f)] public float RouteSampleSpacing = 0.2f;
		[Min(1)] public int RouteMaxSamples = 128;
		[Min(0f)] public float RouteGroundDeviation = 0.2f;
	}
}

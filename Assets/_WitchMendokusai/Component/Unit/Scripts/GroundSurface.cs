using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// Mark this GameObject as a valid ground surface for jump detection.
	/// Any object with this component can be detected as ground by IsGrounded raycast.
	/// </summary>
	[DisallowMultipleComponent]
	public class GroundSurface : MonoBehaviour
	{
		[SerializeField] private bool isWalkable = true;

		public bool IsWalkable => isWalkable;
	}
}

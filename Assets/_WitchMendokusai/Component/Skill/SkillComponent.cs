using UnityEngine;

namespace WitchMendokusai
{
	public abstract class SkillComponent : MonoBehaviour
	{
		public abstract void InitContext(SkillObject skillObject);
	}
}
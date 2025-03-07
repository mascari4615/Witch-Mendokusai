using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static WitchMendokusai.MHelper;

namespace WitchMendokusai
{
	public class SkillObject : MonoBehaviour
	{
		[field: Header("Context")]
		public UnitObject User { get; private set; }
		public bool UsedByPlayer { get; private set; }

		private SkillComponent[] skillComponents;

		private void OnEnable()
		{
			ObjectBufferManager.AddObject(ObjectType.Skill, gameObject);
		}

		private void OnDisable()
		{
			if (IsPlaying)
				ObjectBufferManager.RemoveObject(ObjectType.Skill, gameObject);
		}

		public void InitContext(UnitObject unitObject)
		{
			User = unitObject;
			UsedByPlayer = (unitObject is PlayerObject);

			skillComponents = GetComponentsInChildren<SkillComponent>(true);

			foreach (SkillComponent skillComponent in skillComponents)
				skillComponent.InitContext(this);
		}
	}
}
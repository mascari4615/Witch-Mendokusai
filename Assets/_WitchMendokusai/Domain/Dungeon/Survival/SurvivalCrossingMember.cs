using UnityEngine;

namespace WitchMendokusai
{
	public sealed class SurvivalCrossingMember : MonoBehaviour
	{
		private MonsterObject monster;
		private UnitBrain[] brains;
		private bool[] brainStates;
		private Vector3 direction;
		private Vector3 destination;
		private float remaining;
		private bool running;
		public bool IsCrossing => running;

		public void Begin(MonsterObject monster, Vector3 direction, Vector3 destination, float lifetime)
		{
			this.monster = monster;
			this.direction = direction;
			this.destination = destination;
			remaining = lifetime;
			brains = GetComponents<UnitBrain>();
			brainStates = new bool[brains.Length];
			for (int index = 0; index < brains.Length; index++)
			{
				brainStates[index] = brains[index].enabled;
				brains[index].enabled = false;
			}
			running = true;
			enabled = true;
			monster.UnitMovement.SetMoveDirection(transform.InverseTransformDirection(direction));
		}

		private void Update()
		{
			if (running == false || GameConditionBridge.Get(GameConditionType.IsPaused) || monster.IsActionSuspended || monster.UnitMovement.IsPaused) return;
			remaining -= Time.deltaTime;
			if (remaining <= 0f || Vector3.Dot(destination - monster.UnitMovement.Position, direction) <= 0f)
				gameObject.SetActive(false);
		}

		private void OnDisable()
		{
			if (running == false) return;
			running = false;
			monster.UnitMovement.CancelImpulse();
			monster.UnitMovement.SetMoveDirection(Vector3.zero);
			for (int index = 0; index < brains.Length; index++)
				if (brains[index] != null) brains[index].enabled = brainStates[index];
			enabled = false;
		}
	}
}

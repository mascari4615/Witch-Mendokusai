using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class FarmFieldObject : MonoBehaviour, IInteractable
	{
		public enum State { Empty, Growing, Ready }

		[Header("_" + nameof(FarmFieldObject))]
		[SerializeField, Min(1f)] private float growSeconds = 30f;
		[SerializeField] private List<DataSOWithPercentage> harvestLoots;

		[Header("Models")]
		[SerializeField] private GameObject modelEmpty;
		[SerializeField] private GameObject modelGrowing;
		[SerializeField] private GameObject modelReady;

		public State CurrentState { get; private set; }

		private BuildingObject buildingObject;

		private void OnEnable()
		{
			buildingObject = GetComponentInParent<BuildingObject>();
			Refresh();
			InvokeRepeating(nameof(Refresh), 1f, 1f);
		}

		private void OnDisable()
		{
			CancelInvoke(nameof(Refresh));
		}

		public void OnInteract()
		{
			Refresh();
			switch (CurrentState)
			{
				case State.Empty:   PlantSeed(); break;
				case State.Growing: ShowGrowingBubble(); break;
				case State.Ready:   Harvest(); break;
			}
		}

		private void PlantSeed()
		{
			WriteRuntime(FarmRuntimeData.Planted());
			Refresh();
		}

		private void Harvest()
		{
			GameLogic.SpawnLootItem(harvestLoots, transform.position);
			WriteRuntime(FarmRuntimeData.Empty);
			Refresh();
		}

		private void ShowGrowingBubble()
		{
			long remaining = (long)growSeconds - ReadRuntime().ElapsedSeconds;
			UIManager.Instance.SpeechBubble.Show(transform, $"...아직 자라는 중 ({remaining}s)");
		}

		private void Refresh()
		{
			FarmRuntimeData runtime = ReadRuntime();
			CurrentState = runtime.IsEmpty ? State.Empty
				: runtime.ElapsedSeconds >= growSeconds ? State.Ready
				: State.Growing;
			modelEmpty.SetActive(CurrentState == State.Empty);
			modelGrowing.SetActive(CurrentState == State.Growing);
			modelReady.SetActive(CurrentState == State.Ready);
		}

		private FarmRuntimeData ReadRuntime()
			=> FarmRuntimeData.FromJson(buildingObject.SaveData.RuntimeData);

		private void WriteRuntime(FarmRuntimeData runtime)
			=> buildingObject.UpdateRuntimeData(runtime.ToJson());
	}
}

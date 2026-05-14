using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace WitchMendokusai
{
	public class FarmFieldObject : MonoBehaviour, IInteractable
	{
		public enum State { Empty, Growing, Ready }

		[Header("_" + nameof(FarmFieldObject))]
		[SerializeField] private SeedItemData seed;

		[Header("Models")]
		[SerializeField] private GameObject modelEmpty;
		[SerializeField] private GameObject modelGrowing;
		[SerializeField] private GameObject modelReady;

		public State CurrentState { get; private set; }

		private BuildingObject buildingObject;
		private UIManager uiManager;
		private SOManager soManager;

		[Inject]
		public void Construct(UIManager uiManager, SOManager soManager)
		{
			this.uiManager = uiManager;
			this.soManager = soManager;
		}

		private void Awake()
		{
			LifetimeScope.Find<SceneLifetimeScope>()?.Container.Inject(this);
		}

		private void OnEnable()
		{
			if (seed == null)
			{
				Debug.LogError("[FarmFieldObject] SeedItemData가 할당되지 않았습니다.", this);
				return;
			}
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
			Inventory inventory = soManager.ItemInventory;
			int index = inventory.FindItemIndex(seed);
			if (index < 0)
			{
				uiManager.SpeechBubble.Show(transform, $"{seed.Name} 씨앗이 없습니다.");
				return;
			}

			inventory.Remove(index, 1);
			WriteRuntime(FarmRuntimeData.Planted());
			Refresh();
		}

		private void Harvest()
		{
			GameLogic.SpawnLootItem(seed.HarvestLoots, transform.position);
			WriteRuntime(FarmRuntimeData.Empty);
			Refresh();
		}

		private void ShowGrowingBubble()
		{
			long remaining = (long)seed.GrowSeconds - ReadRuntime().ElapsedSeconds;
			uiManager.SpeechBubble.Show(transform, $"...아직 자라는 중 ({remaining}s)");
		}

		private void Refresh()
		{
			FarmRuntimeData runtime = ReadRuntime();
			CurrentState = runtime.IsEmpty ? State.Empty
				: runtime.ElapsedSeconds >= seed.GrowSeconds ? State.Ready
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

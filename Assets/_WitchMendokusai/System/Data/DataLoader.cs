using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using UnityEngine.SceneManagement;

namespace WitchMendokusai
{
	public class DataLoader : Singleton<DataLoader>
	{
		[SerializeField] private Image progressBar;

		public IEnumerator LoadData()
		{
			// 로딩 시 강제로 로비로 이동
			// 인게임 씬에서 에디터 플레이 시 Awake, Start 등이 호출 될 때 로드가 안된 상태로 데이터에 접근해서 오류가 생기는 문제 방지
			SceneManager.LoadScene("Lobby");

			gameObject.SetActive(true);
			progressBar.fillAmount = 0f;

			List<AsyncOperationHandle> handles = new();
			LoadAssetsAsync(handles);

			while (true)
			{
				float totalPercent = 0;
				foreach (AsyncOperationHandle handle in handles)
					totalPercent += handle.PercentComplete;
				progressBar.fillAmount = totalPercent / handles.Count;

				// Debug.Log($"Loading... {progressBar.fillAmount * 100}%");

				if (handles.All(handle => handle.IsDone))
					break;

				yield return null;
			}
			Debug.Log($"Loading... {progressBar.fillAmount * 100}%");

			//foreach (var handle in handles)
			//	Addressables.Release(handle);

			progressBar.fillAmount = 1f;
			gameObject.SetActive(false);
		}

		private void LoadAssetsAsync(List<AsyncOperationHandle> handles)
		{
			SOManager.Instance.DataSOs.Clear();

			// TODO: MDataSOUtil에서 목록 가져오기
			LoadAsset<QuestSO>(nameof(QuestSO));
			LoadAsset<CardData>(nameof(CardData));
			LoadAsset<ItemData>(nameof(ItemData));
			LoadAsset<MonsterWave>(nameof(MonsterWave));
			LoadAsset<SkillData>(nameof(SkillData));
			LoadAsset<WorldStage>(nameof(WorldStage));
			LoadAsset<Dungeon>(nameof(Dungeon));
			LoadAsset<DungeonStage>(nameof(DungeonStage));
			LoadAsset<DungeonConstraint>(nameof(DungeonConstraint));
			LoadAsset<Doll>(nameof(Doll));
			LoadAsset<NPC>(nameof(NPC));
			LoadAsset<Monster>(nameof(Monster));
			LoadAsset<UnitStatData>(nameof(UnitStatData));
			LoadAsset<GameStatData>(nameof(GameStatData));
			LoadAsset<DungeonStatData>(nameof(DungeonStatData));
			LoadAsset<Building>(nameof(Building));

			void LoadAsset<T>(string label) where T : DataSO
			{
				var handle = Addressables.LoadAssetsAsync<T>(label, null);
				handle.Completed += OnAssetsLoaded;
				handles.Add(handle);
			}
		}

		private void OnAssetsLoaded<T>(AsyncOperationHandle<IList<T>> obj) where T : DataSO
		{
			if (obj.Status == AsyncOperationStatus.Succeeded)
			{
				List<T> assets = obj.Result.ToList();
				SOManager.Instance.DataSOs[typeof(T)] = new();

				foreach (T asset in assets)
				{
					// Debug.Log($"Loaded {asset.name}");
					SOManager.Instance.DataSOs[typeof(T)].Add(asset.ID, asset);
				}
			}
		}
	}
}
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using UnityEngine.SceneManagement;
using System;
using System.Reflection;
using TMPro;
using VContainer;

namespace WitchMendokusai
{
	public class DataLoader : MonoBehaviour
	{
		public static DataLoader Instance { get; private set; }

		public static bool TryGetExistingInstance(out DataLoader mgr)
		{
			mgr = Instance;
			return mgr != null;
		}

		[SerializeField] private Image progressBar;
		[SerializeField] private TextMeshProUGUI progressText;

		private SOManager soManager;

		[Inject]
		public void Construct(SOManager soManager)
		{
			this.soManager = soManager;
		}

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}
			Instance = this;
			// prefab m_IsActive=1 (Container spawn 시 Awake 보장) + Awake 직후 self-deactivate
			// — Lobby 시 progress UI 안 보이게. LoadData() 의 SetActive(true) 가 보이게 만듦.
			gameObject.SetActive(false);
		}

		private void OnDestroy()
		{
			if (Instance == this)
				Instance = null;
		}

		public IEnumerator LoadData()
		{
			// 로딩 시 강제로 로비로 이동
			// 인게임 씬에서 에디터 플레이 시 Awake, Start 등이 호출 될 때 로드가 안된 상태로 데이터에 접근해서 오류가 생기는 문제 방지
			if (SceneManager.GetActiveScene().name != "Lobby")
				SceneManager.LoadScene("Lobby");

			gameObject.SetActive(true);
			progressBar.fillAmount = 0f;

			// 데이터 초기화
			soManager.DataSOs.Clear();

			List<AsyncOperationHandle> handles = new();
			LoadAssetsAsync(handles);

			while (handles.All(handle => handle.IsDone) == false)
			{
				float totalPercent = handles.Sum(handle => handle.PercentComplete) / handles.Count;
				progressBar.fillAmount = totalPercent;
				progressText.text = $"로딩 중... {totalPercent * 100f:0.0}%";

				yield return null;
			}

			progressBar.fillAmount = 1f;
			progressText.text = "로딩 완료";

			yield return new WaitForSeconds(0.5f); // 완료된 상태 잠시 표시
			gameObject.SetActive(false);
		}

		private void LoadAssetsAsync(List<AsyncOperationHandle> handles)
		{
			foreach (Type type in DataSODefine.AssetPrefixes.Keys)
			{
				LoadAssetByType(type, handles);
			}

			void LoadAssetByType(Type type, List<AsyncOperationHandle> handles)
			{
				MethodInfo method = typeof(DataLoader).GetMethod(nameof(LoadAsset), BindingFlags.Instance | BindingFlags.NonPublic);
				MethodInfo genericMethod = method.MakeGenericMethod(type);
				genericMethod.Invoke(this, new object[] { type.Name, handles });
			}
		}

		private void LoadAsset<T>(string label, List<AsyncOperationHandle> handles) where T : DataSO
		{
			AsyncOperationHandle<IList<T>> handle = Addressables.LoadAssetsAsync<T>(label, null);
			handle.Completed += OnAssetsLoaded;
			handles.Add(handle);
		}

		private void OnAssetsLoaded<T>(AsyncOperationHandle<IList<T>> obj) where T : DataSO
		{
			if (obj.Status == AsyncOperationStatus.Succeeded)
			{
				List<T> assets = obj.Result.ToList();
				Type type = typeof(T);

				if (soManager.DataSOs.ContainsKey(type) == false)
					soManager.DataSOs[type] = new();

				foreach (T asset in assets)
				{
					soManager.DataSOs[type][asset.ID] = asset;
					// Debug.Log($"Load {type.Name}: {asset.ID}, {soManager.DataSOs[type][asset.ID].name}");
				}

				// Debug.Log($"{typeof(T).Name} 로드 완료, {assets.Count}개");
			}
			else
			{
				Debug.LogError($"{typeof(T).Name} 로드 실패: {obj.OperationException?.Message}");
			}
		}
	}
}

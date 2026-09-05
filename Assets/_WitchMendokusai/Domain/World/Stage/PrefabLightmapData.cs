using System;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

// https://github.com/Ayfel/PrefabLightmapping

[ExecuteAlways]
public class PrefabLightmapData : MonoBehaviour
{
	[Serializable]
	private struct RendererInfo
	{
		public Renderer renderer;
		public int lightmapIndex;
		public Vector4 lightmapScaleOffset;
	}

	[Serializable]
	private struct LightInfo
	{
		public Light light;
		public LightmapBakeType lightmapBaketype;
		public MixedLightingMode mixedLightingMode;
	}

	[SerializeField] private List<RendererInfo> rendererInfos = new();
	[SerializeField] private List<Texture2D> lightmaps = new();
	[SerializeField] private List<Texture2D> lightmapsDir = new();
	[SerializeField] private List<Texture2D> shadowMasks = new();
	[SerializeField] private List<LightInfo> lightInfos = new();

	[Tooltip("켜면 LightmapSettings에 이 프리팹 라이트맵만 남기고 이전 항목은 제거합니다. 구역 전환 시 라이트맵이 쌓이는 것을 막을 때 사용합니다. 씬에 이 프리팹 밖의 베이크 지오메트리가 있으면 끄세요.")]
	[SerializeField] private bool replaceSceneLightmaps = true;

	// 런타임: 비활성화 후 다시 켤 때는 Start가 재호출되지 않으므로 OnEnable에서 적용해야 함 (스테이지 A/B 재사용 패턴).
	// 에디터(ExecuteAlways): 플레이 아닐 때 Init 하면 LightmapSettings를 건드리므로 플레이 중에만 수행.
	private void OnEnable()
	{
		SceneManager.sceneLoaded += OnSceneLoaded;
		if (Application.isPlaying)
			Init();
	}

	private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;
	private void OnSceneLoaded(Scene _, LoadSceneMode __) => Init();

	/// <summary>
	/// 라이팅 설정 교체 등으로 <see cref="LightmapSettings"/>가 갱신된 뒤, 이 프리팹의 라이트맵을 다시 끼워 넣을 때 호출.
	/// </summary>
	public void RefreshLightmaps() => Init();

	private void Init()
	{
		if (rendererInfos == null || rendererInfos.Count == 0)
			return;

		if (lightmaps == null || lightmaps.Count == 0 || lightmaps[0] == null)
			return;

		List<Texture2D> savedLightmaps = lightmaps;
		int[] offsetsIndexes = new int[savedLightmaps.Count];

		bool useCombinedDirectional = UseCombinedDirectionalLightmaps(lightmapsDir, savedLightmaps.Count);

		if (replaceSceneLightmaps)
		{
			LightmapData[] onlyThisPrefab = BuildLightmapDataArrayForPrefab(savedLightmaps, useCombinedDirectional);
			LightmapSettings.lightmapsMode = useCombinedDirectional ? LightmapsMode.CombinedDirectional : LightmapsMode.NonDirectional;
			LightmapSettings.lightmaps = onlyThisPrefab;
			for (int i = 0; i < offsetsIndexes.Length; i++)
				offsetsIndexes[i] = i;
			ApplyRendererInfo(rendererInfos, offsetsIndexes, lightInfos);
			return;
		}

		LightmapData[] curLightmaps = LightmapSettings.lightmaps ?? Array.Empty<LightmapData>();
		int countTotal = curLightmaps.Length;

		List<LightmapData> combinedLightmaps = new();
		combinedLightmaps.AddRange(curLightmaps);

		for (int i = 0; i < savedLightmaps.Count; i++)
		{
			bool exists = false;
			for (int j = 0; j < curLightmaps.Length; j++)
			{
				if (savedLightmaps[i] == curLightmaps[j].lightmapColor)
				{
					exists = true;
					offsetsIndexes[i] = j;
					break;
				}
			}

			if (exists)
				continue;

			offsetsIndexes[i] = countTotal;

			Texture2D dirTex = null;
			if (useCombinedDirectional && lightmapsDir != null && i < lightmapsDir.Count)
				dirTex = lightmapsDir[i];

			Texture2D maskTex = null;
			if (shadowMasks != null && shadowMasks.Count == savedLightmaps.Count && i < shadowMasks.Count)
				maskTex = shadowMasks[i];

			LightmapData newlightmapdata = new()
			{
				lightmapColor = savedLightmaps[i],
				lightmapDir = dirTex,
				shadowMask = maskTex,
			};

			combinedLightmaps.Add(newlightmapdata);
			countTotal += 1;
		}

		LightmapSettings.lightmapsMode = useCombinedDirectional ? LightmapsMode.CombinedDirectional : LightmapsMode.NonDirectional;
		LightmapSettings.lightmaps = combinedLightmaps.ToArray();
		ApplyRendererInfo(rendererInfos, offsetsIndexes, lightInfos);
	}

	private static bool UseCombinedDirectionalLightmaps(List<Texture2D> dirs, int lightmapCount)
	{
		if (dirs == null || dirs.Count != lightmapCount)
			return false;
		for (int i = 0; i < lightmapCount; i++)
		{
			if (dirs[i] == null)
				return false;
		}
		return true;
	}

	private LightmapData[] BuildLightmapDataArrayForPrefab(List<Texture2D> savedLightmaps, bool useCombinedDirectional)
	{
		LightmapData[] arr = new LightmapData[savedLightmaps.Count];
		for (int i = 0; i < savedLightmaps.Count; i++)
		{
			Texture2D dirTex = null;
			if (useCombinedDirectional && lightmapsDir != null && i < lightmapsDir.Count)
				dirTex = lightmapsDir[i];

			Texture2D maskTex = null;
			if (shadowMasks != null && shadowMasks.Count == savedLightmaps.Count && i < shadowMasks.Count)
				maskTex = shadowMasks[i];

			arr[i] = new LightmapData
			{
				lightmapColor = savedLightmaps[i],
				lightmapDir = dirTex,
				shadowMask = maskTex,
			};
		}
		return arr;
	}

	private void ApplyRendererInfo(List<RendererInfo> infos, int[] lightmapOffsetIndex, List<LightInfo> lightsInfo)
	{
		if (infos == null || lightmapOffsetIndex == null)
			return;

		foreach (RendererInfo info in infos)
		{
			if (info.renderer == null)
				continue;
			if (info.lightmapIndex < 0 || info.lightmapIndex >= lightmapOffsetIndex.Length)
				continue;

			info.renderer.lightmapIndex = lightmapOffsetIndex[info.lightmapIndex];
			info.renderer.lightmapScaleOffset = info.lightmapScaleOffset;

			// You have to release shaders.
			Material[] mat = info.renderer.sharedMaterials;
			foreach (Material m in mat)
			{
				if (m != null && Shader.Find(m.shader.name) != null)
					m.shader = Shader.Find(m.shader.name);
			}
		}

		if (lightsInfo == null)
			return;

		foreach (LightInfo lightInfo in lightsInfo)
		{
			if (lightInfo.light == false)
				continue;

			LightBakingOutput bakingOutput = new()
			{
				isBaked = true,
				lightmapBakeType = lightInfo.lightmapBaketype,
				mixedLightingMode = lightInfo.mixedLightingMode
			};

			lightInfo.light.bakingOutput = bakingOutput;
		}
	}

#if UNITY_EDITOR
	[MenuItem("Assets/Bake Prefab Lightmaps")]
	public static void GenerateLightmapInfo()
	{
		// UnityEditor.Lightmapping.Bake();

		PrefabLightmapData[] instances = FindObjectsByType<PrefabLightmapData>(FindObjectsInactive.Include);
		foreach (PrefabLightmapData instance in instances)
		{
			GenerateLightmapInfo(instance);

			// 타겟 프리팹 찾기 (이 PrefabLightmapData가 붙어있는 프리팹 찾기)
			GameObject targetPrefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(instance.gameObject);
			if (targetPrefab != null)
			{
				// 루트 프리팹 찾기 (가장 바깥쪽 프리팹 찾기)
				GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(instance.gameObject);
				GameObject rootPrefab = PrefabUtility.GetCorrespondingObjectFromSource(instance.gameObject);

				// 타겟 프리팹이 다른 프리팹의 일부가 아닌 경우
				if (targetPrefab == rootPrefab)
				{
					// 프리팹에 변경 사항 적용합니다.
					PrefabUtility.ApplyPrefabInstance(instance.gameObject, InteractionMode.AutomatedAction);
				}
				else
				{
					string rootPath = AssetDatabase.GetAssetPath(rootPrefab);

					// 루트 프리팹 인스턴스 언팩
					PrefabUtility.UnpackPrefabInstanceAndReturnNewOutermostRoots(root, PrefabUnpackMode.OutermostRoot);

					// 타겟 프리팹에 변경 사항 적용
					PrefabUtility.ApplyPrefabInstance(instance.gameObject, InteractionMode.AutomatedAction);

					// 루트 프리팹 저장 및 연결
					PrefabUtility.SaveAsPrefabAssetAndConnect(root, rootPath, InteractionMode.AutomatedAction);
				}
			}
		}
	}

	private static void GenerateLightmapInfo(PrefabLightmapData instance)
	{
		instance.rendererInfos.Clear();
		instance.lightmaps.Clear();
		instance.lightmapsDir.Clear();
		instance.shadowMasks.Clear();
		instance.lightInfos.Clear();

		// 메쉬렌더러, 라이트맵 정보
		MeshRenderer[] renderers = instance.gameObject.GetComponentsInChildren<MeshRenderer>();
		foreach (MeshRenderer renderer in renderers)
		{
			if (renderer.lightmapScaleOffset == Vector4.zero)
				continue;

			// 1ibrium's pointed out this issue : https://docs.unity3d.com/ScriptReference/Renderer-lightmapIndex.html
			if (renderer.lightmapIndex < 0 || renderer.lightmapIndex == 0xFFFE)
				continue;

			LightmapData[] baked = LightmapSettings.lightmaps;
			if (baked == null || renderer.lightmapIndex >= baked.Length)
				continue;

			LightmapData lightmapData = baked[renderer.lightmapIndex];
			Texture2D lightmap = lightmapData.lightmapColor;
			Texture2D lightmapDir = lightmapData.lightmapDir;
			Texture2D shadowMask = lightmapData.shadowMask;

			// 라이트맵이 이미 존재하는지 확인
			int lightmapIndex = instance.lightmaps.IndexOf(lightmap);
			bool exists = lightmapIndex != -1;

			// 라이트맵이 존재하지 않으면 추가
			if (exists == false)
			{
				lightmapIndex = instance.lightmaps.Count;

				instance.lightmaps.Add(lightmap);
				instance.lightmapsDir.Add(lightmapDir);
				instance.shadowMasks.Add(shadowMask);
			}

			RendererInfo rendererInfo = new()
			{
				renderer = renderer,
				lightmapScaleOffset = renderer.lightmapScaleOffset,
				lightmapIndex = lightmapIndex
			};

			instance.rendererInfos.Add(rendererInfo);
		}

		// 빛 정보
		Light[] lights = instance.gameObject.GetComponentsInChildren<Light>(true);
		foreach (Light light in lights)
		{
			LightInfo lightInfo = new()
			{
				light = light,
				lightmapBaketype = light.lightmapBakeType,
				mixedLightingMode = Lightmapping.lightingSettings.mixedBakeMode
			};

			instance.lightInfos.Add(lightInfo);
		}
	}
#endif
}

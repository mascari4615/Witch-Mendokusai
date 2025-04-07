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

	private void Awake() => Init();
	private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
	private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;
	private void OnSceneLoaded(Scene _, LoadSceneMode __) => Init();

	private void Init()
	{
		if (rendererInfos == null || rendererInfos.Count == 0)
			return;

		List<Texture2D> savedLightmaps = lightmaps;
		int[] offsetsIndexes = new int[savedLightmaps.Count];

		LightmapData[] curLightmaps = LightmapSettings.lightmaps;
		int countTotal = curLightmaps.Length;

		List<LightmapData> combinedLightmaps = new();
		combinedLightmaps.AddRange(curLightmaps);

		for (int i = 0; i < savedLightmaps.Count; i++)
		{
			// 라이트맵이 이미 존재하는지 확인
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

			// 라이트맵이 존재하지 않으면 추가
			if (exists == false)
			{
				offsetsIndexes[i] = countTotal;

				LightmapData newlightmapdata = new()
				{
					lightmapColor = savedLightmaps[i],
					lightmapDir = lightmapsDir.Count == savedLightmaps.Count ? lightmapsDir[i] : default,
					shadowMask = shadowMasks.Count == savedLightmaps.Count ? shadowMasks[i] : default,
				};

				combinedLightmaps.Add(newlightmapdata);
				countTotal += 1;
			}
		}

		bool isDirectional = true;
		foreach (Texture2D t in lightmapsDir)
		{
			if (t == null)
			{
				isDirectional = false;
				break;
			}
		}

		LightmapSettings.lightmapsMode = (lightmapsDir.Count == savedLightmaps.Count && isDirectional) ? LightmapsMode.CombinedDirectional : LightmapsMode.NonDirectional;
		ApplyRendererInfo(rendererInfos, offsetsIndexes, lightInfos);
		LightmapSettings.lightmaps = combinedLightmaps.ToArray();
	}

	private void ApplyRendererInfo(List<RendererInfo> infos, int[] lightmapOffsetIndex, List<LightInfo> lightsInfo)
	{
		foreach (RendererInfo info in infos)
		{
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

		foreach (LightInfo lightInfo in lightsInfo)
		{
			LightBakingOutput bakingOutput = new()
			{
				isBaked = true,
				lightmapBakeType = lightInfo.lightmapBaketype,
				mixedLightingMode = lightInfo.mixedLightingMode
			};

			// For EditorOnly
			if (lightInfo.light)
				lightInfo.light.bakingOutput = bakingOutput;
		}
	}

#if UNITY_EDITOR
	[MenuItem("Assets/Bake Prefab Lightmaps")]
	private static void GenerateLightmapInfo()
	{
		if (Lightmapping.giWorkflowMode != Lightmapping.GIWorkflowMode.OnDemand)
		{
			Debug.LogError("ExtractLightmapData requires that you have baked you lightmaps and Auto mode is disabled.");
			return;
		}
		// UnityEditor.Lightmapping.Bake();

		PrefabLightmapData[] instances = FindObjectsByType<PrefabLightmapData>(FindObjectsInactive.Include, FindObjectsSortMode.None);
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

			LightmapData lightmapData = LightmapSettings.lightmaps[renderer.lightmapIndex];
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

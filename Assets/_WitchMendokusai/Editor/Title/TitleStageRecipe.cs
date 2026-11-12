using UnityEngine;

namespace WitchMendokusai
{
	// 시작 화면 무대 소품 배치 설정. TitleStageBuilder 입력
	[CreateAssetMenu(fileName = nameof(TitleStageRecipe), menuName = "WM/Title/TitleStageRecipe")]
	public class TitleStageRecipe : ScriptableObject
	{
		[Header("Seed")]
		public int Seed = 4615;

		[Header("Materials")]
		public Material GroundSource;
		public float GroundColorScale = 1.2f;
		public Material GrassSource;
		public Material GrassLit;

		[Header("Big tree")]
		public GameObject TreePrefab;
		public Vector2 BigTreePosition = new(4f, 2f);
		public float BigTreeScale = 1.9f;

		[Header("Forest")]
		public int ForestCount = 34;
		public Vector2 ForestRadius = new(55f, 150f);
		public Vector2 ForestScale = new(1.1f, 2.4f);
		[Tooltip("카메라 쪽 (음의 z) 으로 이 각도 범위는 비움")]
		public float ForestFrontGapDegrees = 70f;

		[Header("Shrub")]
		public GameObject ShrubPrefab;
		public int ShrubCount = 70;
		public float ShrubRadius = 60f;
		public Vector2 ShrubScale = new(0.8f, 1.6f);

		[Header("Grass")]
		public GameObject GrassPrefab;
		public int GrassCount = 650;
		public float GrassRadius = 70f;
		public Vector2 GrassScale = new(0.9f, 1.8f);

		[Header("Gravel")]
		public GameObject GravelPrefab;
		public int GravelCount = 26;
		public float GravelRadius = 45f;

		[Header("Lobby install")]
		public SkyPresetSO SkyPreset;
		public Material SkyboxMaterial;
		[Range(0f, 24f)] public float Hour = 14.5f;
		public Vector3 CameraPosition = new(-6f, 3.2f, -46f);
		public Vector3 CameraEuler = new(-6f, 8f, 0f);
		public float CameraFieldOfView = 42f;
	}
}

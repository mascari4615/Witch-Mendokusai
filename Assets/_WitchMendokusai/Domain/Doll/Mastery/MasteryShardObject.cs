using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 숙련 조각. 경험치 구슬처럼 줍기 범위에 닿으면 끌려와 바로 획득 (사용자 2026-11-22 22:26)
	// 외형: 작은 마름모 보석 (CrystalClusterMesh + MasteryGem 세 단 색), 바닥에서 퍼지는 고리, 둘레를 도는 반짝 별 (사용자 2026-11-23 07:25 시안 5 '단색 목업 느낌')
	// 획득하면 막대 스탯 (고유 효과) 과 막대 진행 (확정 변신). 처리는 DollMasteryRun.Pickup, 연출은 숙련 자료의 순간 값
	// 고리와 별은 MasteryShardObject.Fx.cs
	public partial class MasteryShardObject : LootObject
	{
		private static readonly int PULSE = Shader.PropertyToID("_Pulse");
		private static readonly Dictionary<string, Mesh> MESH_CACHE = new();

		[Header("외형")]
		[SerializeField] private Transform shardTransform;
		[SerializeField] private MeshFilter crystalFilter;
		[SerializeField] private MeshRenderer crystalRenderer;
		[SerializeField, Tooltip("보석 모양. 같은 이름이면 메시 공유")] private string clusterName = "MasteryCrystal";
		[SerializeField] private List<CrystalSpec> crystals = new();
		[SerializeField, Tooltip("면 음영 (정점 색). 색 자체는 재질 (MasteryGem)")] private CrystalClusterMesh.FacetShade facetShade;
		[SerializeField, Tooltip("보석 크기 배율"), Min(0.1f)] private float visualScale = 1f;

		[Header("움직임")]
		[SerializeField, Tooltip("초당 회전 각도")] private float spinSpeed = 70f;
		[SerializeField, Tooltip("위아래 흔들림 높이")] private float bobHeight = .14f;
		[SerializeField, Tooltip("초당 위아래 흔들림 횟수")] private float bobFrequency = .9f;
		[SerializeField, Tooltip("등장할 때 커지는 시간 (초)"), Min(0.01f)] private float popSeconds = .45f;
		[SerializeField, Tooltip("등장 크기 곡선 (0~1 시간 -> 크기 배율). 끝에 살짝 넘쳤다 돌아옴")] private AnimationCurve popCurve = new(new Keyframe(0f, 0f), new Keyframe(.7f, 1.18f), new Keyframe(1f, 1f));

		[Header("빛")]
		[SerializeField, Tooltip("바닥을 물들이는 점광 세기 기본값")] private float glowIntensity = 2.2f;
		[SerializeField, Tooltip("점광 반경 (m)"), Min(0.1f)] private float glowRange = 3.2f;
		[SerializeField, Tooltip("숨쉬기 폭 (0~1)"), Range(0f, 1f)] private float breathAmount = .35f;
		[SerializeField, Tooltip("초당 숨쉬기 횟수")] private float breathFrequency = 1.4f;
		[SerializeField, Tooltip("숨이 가장 클 때 보석 밝기 더함 (MasteryGem _Pulse)"), Min(0f)] private float pulseBrightness = .35f;

		private int track;
		private float phase;
		private float spawnTime;
		private Color trackColor = Color.white;
		private MaterialPropertyBlock block;
		// 점광은 코드가 처음 Init 에서 붙임 (프리팹 Light 칸을 손으로 쓰지 않게)
		private Light glow;

		public Color TrackColor => trackColor;

		// Awake 는 LootObject 것 (줍기 충돌체). 여기서 가리지 않게 외형 준비는 Init 에서
		// groundDepth: 조각 중심에서 지면까지 (바닥 고리 자리)
		public void Init(int masteryTrack, Color color, float groundDepth)
		{
			Mesh mesh = SharedMesh();
			if (crystalFilter.sharedMesh != mesh)
				crystalFilter.sharedMesh = mesh;
			block ??= new MaterialPropertyBlock();
			if (glow == null)
			{
				glow = gameObject.AddComponent<Light>();
				glow.type = LightType.Point;
				glow.shadows = LightShadows.None;
			}
			glow.range = glowRange;
			track = masteryTrack;
			trackColor = color;
			phase = Random.value;
			spawnTime = Time.time;
			glow.color = color;
			shardTransform.localScale = Vector3.zero;
			InitFx(color, groundDepth);
		}

		private Mesh SharedMesh()
		{
			if (MESH_CACHE.TryGetValue(clusterName, out Mesh mesh) && mesh != null)
				return mesh;
			mesh = CrystalClusterMesh.Build(crystals, facetShade, clusterName);
			MESH_CACHE[clusterName] = mesh;
			return mesh;
		}

		private void Update()
		{
			float time = Time.time + phase;
			float pop = popCurve.Evaluate(Mathf.Clamp01((Time.time - spawnTime) / popSeconds));
			shardTransform.localScale = Vector3.one * (pop * visualScale);
			shardTransform.localRotation = Quaternion.Euler(0f, time * spinSpeed, 0f);
			shardTransform.localPosition = Vector3.up * Mathf.Sin(time * bobFrequency * Mathf.PI * 2f) * bobHeight;

			float wave = 0.5f + 0.5f * Mathf.Sin(time * breathFrequency * Mathf.PI * 2f);
			float breath = 1f - breathAmount + breathAmount * wave;
			crystalRenderer.GetPropertyBlock(block);
			block.SetFloat(PULSE, pulseBrightness * wave);
			crystalRenderer.SetPropertyBlock(block);
			glow.intensity = glowIntensity * breath * pop;
			UpdateFx(time, pop, breath);
		}

		protected override void OnEffect()
		{
			DollMasteryRun.Pickup(track, playerProvider.CurrentObject, transform.position);
		}
	}
}

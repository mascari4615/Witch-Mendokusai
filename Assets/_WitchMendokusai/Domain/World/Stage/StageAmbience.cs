using UnityEngine;

namespace WitchMendokusai
{
	// 스테이지가 정한 떠도는 입자 (Stage.AmbientParticles). StageManager 가 소유하고 스테이지가 바뀔 때 Apply
	// 사용자 2026-11-27 "플레이어에 붙은게 애초에 이상함, 스테이지에 따라야지". 전에는 Player.prefab 자식이라 실내에서도 먼지가 돌았음
	// - 입자는 월드 공간이라 몸통만 플레이어 자리로 옮기면 이미 난 먼지는 제자리에 남음 (플레이어 자식이던 때와 같은 모양)
	// - 실내 (욘의 집, 박물관, 갱도. IndoorScope) 에서는 끔. 같은 스테이지 안의 방이라 스테이지 값만으로는 못 가름
	public sealed class StageAmbience
	{
		private GameObject source;
		private GameObject instance;

		public void Apply(Stage stage)
		{
			GameObject wanted = stage != null ? stage.AmbientParticles : null;
			if (wanted == source)
				return;
			Clear();
			source = wanted;
			if (source != null)
				instance = Object.Instantiate(source);
		}

		// 매 프레임. 플레이어가 없으면 그 자리에 둠
		public void Follow(Transform target)
		{
			if (instance == null)
				return;
			bool visible = IndoorScope.IsIndoor == false;
			if (instance.activeSelf != visible)
				instance.SetActive(visible);
			if (visible && target != null)
				instance.transform.position = target.position;
		}

		public void Clear()
		{
			if (instance != null)
				Object.Destroy(instance);
			instance = null;
			source = null;
		}
	}
}

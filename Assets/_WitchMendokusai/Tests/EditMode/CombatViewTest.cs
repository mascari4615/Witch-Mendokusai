using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	// 전투 시야 경계. 정본 memo wm/design/camera-angle-20261122/README.md
	public class CombatViewTest
	{
		private const float PITCH = 30f;
		private const float CAMERA_DISTANCE = 14f;
		private const float MAX_DEPTH = 36f;
		private readonly Plane[] planes = new Plane[6];
		private GameObject cameraObject;
		private Camera camera;

		[SetUp]
		public void SetUp()
		{
			cameraObject = new GameObject("CombatViewTestCamera");
			camera = cameraObject.AddComponent<Camera>();
			camera.fieldOfView = 50f;
			camera.aspect = 16f / 9f;
			camera.nearClipPlane = 0.3f;
			camera.farClipPlane = 100f;
			Quaternion rotation = Quaternion.Euler(PITCH, 0f, 0f);
			cameraObject.transform.SetPositionAndRotation(-(rotation * Vector3.forward) * CAMERA_DISTANCE, rotation);
		}

		[TearDown]
		public void TearDown()
		{
			CombatView.SetMaxDepth(0f);
			Object.DestroyImmediate(cameraObject);
		}

		[Test]
		public void Ground_far_ahead_is_on_screen_without_limit()
		{
			CombatView.SetMaxDepth(0f);
			CombatView.CalculatePlanes(camera, planes);
			Assert.IsTrue(IsInside(Ahead(40f)), "30도 카메라는 경계 없으면 40m 앞 땅이 화면 안");
		}

		[Test]
		public void Limit_cuts_ground_beyond_depth()
		{
			CombatView.SetMaxDepth(MAX_DEPTH);
			CombatView.CalculatePlanes(camera, planes);
			Assert.IsTrue(IsInside(Ahead(0f)), "캐릭터 발밑");
			Assert.IsTrue(IsInside(Ahead(20f)), "20m 앞");
			Assert.IsFalse(IsInside(Ahead(30f)), "30m 앞은 깊이 36 밖");
			Assert.IsFalse(IsInside(Ahead(40f)), "40m 앞");
		}

		[Test]
		public void Side_planes_stay_when_limited()
		{
			CombatView.SetMaxDepth(MAX_DEPTH);
			CombatView.CalculatePlanes(camera, planes);
			Assert.IsFalse(IsInside(new Vector3(30f, 0f, 0f)), "캐릭터 옆 30m 는 화면 밖 그대로");
		}

		[Test]
		public void Limit_beyond_far_clip_keeps_camera_planes()
		{
			CombatView.SetMaxDepth(500f);
			CombatView.CalculatePlanes(camera, planes);
			Plane[] original = GeometryUtility.CalculateFrustumPlanes(camera);
			Assert.AreEqual(original[5].distance, planes[5].distance, 0.001f);
		}

		private static Vector3 Ahead(float meters) => new(0f, 0f, meters);

		private bool IsInside(Vector3 point) => GeometryUtility.TestPlanesAABB(planes, new Bounds(point, Vector3.one * 0.2f));
	}
}

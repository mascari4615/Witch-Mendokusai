using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 층별 시차. wm-depth-1 (가장 먼) 부터 wm-depth-4 (가장 가까운) 클래스 요소가 포인터 위치에 따라 다른 거리로 움직임
	/// 포인터가 없으면 천천히 저절로. UIFxProfile 의 parallax 칸
	/// </summary>
	[CreateAssetMenu(fileName = "Parallax_0_Layers", menuName = "WM/UI/Parallax")]
	public sealed class UIParallax : ScriptableObject
	{
		public const string DEPTH_CLASS_PREFIX = "wm-depth-";

		[Tooltip("깊이 1~4 의 최대 이동, 픽셀")]
		public float[] depthPixels = { 6f, 14f, 22f, 30f };
		[Tooltip("세로 이동 비율")]
		[Range(0f, 1f)] public float verticalRatio = 0.6f;
		[Tooltip("포인터 추적 속도, 초당 비율")]
		public float follow = 5f;
		[Tooltip("포인터가 멈춘 뒤 저절로 움직이기까지, 초")]
		public float idleSeconds = 2.5f;
		[Tooltip("저절로 움직이는 한 바퀴 시간, 초")]
		public float driftSeconds = 10f;

		public void Attach(VisualElement root)
		{
			List<(VisualElement element, float pixels)> layers = new();
			for (int depth = 1; depth <= depthPixels.Length; depth++)
			{
				float pixels = depthPixels[depth - 1];
				root.Query(className: DEPTH_CLASS_PREFIX + depth).ForEach(element => layers.Add((element, pixels)));
			}
			Vector2 target = Vector2.zero;
			Vector2 current = Vector2.zero;
			float sinceInput = idleSeconds;
			float elapsed = 0f;
			root.RegisterCallback<PointerMoveEvent>(evt =>
			{
				Rect bound = root.worldBound;
				if (bound.width <= 0f || bound.height <= 0f)
					return;
				target = new Vector2((evt.position.x - bound.x) / bound.width * 2f - 1f, (evt.position.y - bound.y) / bound.height * 2f - 1f);
				sinceInput = 0f;
			});
			root.schedule.Execute(timer =>
			{
				if (UIIdleFx.IsShown(root) == false)
					return;
				float deltaSeconds = UIFxTween.Seconds(timer);
				elapsed += deltaSeconds;
				sinceInput += deltaSeconds;
				if (sinceInput >= idleSeconds)
				{
					float phase = elapsed / UIFxTween.Duration(driftSeconds) * Mathf.PI * 2f;
					// 가로세로 박자를 달리한 8자 흐름 (모양 정의, 빠르기는 driftSeconds)
					target = new Vector2(Mathf.Sin(phase), Mathf.Sin(phase * 0.73f) * 0.5f);
				}
				current = Vector2.Lerp(current, target, 1f - Mathf.Exp(-follow * deltaSeconds));
				foreach ((VisualElement element, float pixels) in layers)
					element.style.translate = new Translate(-current.x * pixels, -current.y * pixels * verticalRatio);
			}).Every(0).Until(() => root.panel == null);
		}
	}
}

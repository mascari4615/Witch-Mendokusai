using System.Collections;
using System.IO;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 개발용 프레임 녹화. 게임 시간을 고정 간격으로 돌려 (Time.captureFramerate) 화면을 PNG 로 한 장씩 저장
	/// - 히트스톱, 실제 시간 연출까지 같은 간격으로 재현되어 영상으로 묶으면 실제 속도
	/// - 에디터 Game 뷰와 같은 해상도. 끝나면 고정 해제
	/// - 호출: DevFrameRecorder.Begin(폴더, 초당 프레임, 초, JPG 품질). 메뉴나 명령이 아니라 검증 스크립트용
	/// - JPG 품질이 0 이면 PNG. 노트북에서 1080p PNG 는 한 장 저장이 느려 녹화가 몇 배 길어짐
	/// </summary>
	public sealed class DevFrameRecorder : MonoBehaviour
	{
		public static bool IsRecording { get; private set; }

		public static void Begin(string directory, int framesPerSecond, float seconds, int jpegQuality = 0)
		{
			if (IsRecording)
				throw new System.InvalidOperationException("이미 녹화 중");
			Directory.CreateDirectory(directory);
			GameObject host = new("DevFrameRecorder");
			DontDestroyOnLoad(host);
			host.AddComponent<DevFrameRecorder>().StartCoroutine(Record(host, directory, framesPerSecond, seconds, jpegQuality));
		}

		private static IEnumerator Record(GameObject host, string directory, int framesPerSecond, float seconds, int jpegQuality)
		{
			IsRecording = true;
			Time.captureFramerate = framesPerSecond;
			int frames = Mathf.CeilToInt(framesPerSecond * seconds);
			float scaledSum = 0f;
			float unscaledSum = 0f;
			for (int i = 0; i < frames; i++)
			{
				yield return new WaitForEndOfFrame();
				scaledSum += Time.deltaTime;
				unscaledSum += Time.unscaledDeltaTime;
				Texture2D texture = ScreenCapture.CaptureScreenshotAsTexture();
				bool jpeg = jpegQuality > 0;
				byte[] bytes = jpeg ? texture.EncodeToJPG(jpegQuality) : texture.EncodeToPNG();
				File.WriteAllBytes(Path.Combine(directory, $"f{i:D4}" + (jpeg ? ".jpg" : ".png")), bytes);
				Destroy(texture);
			}
			Debug.Log($"[DevFrameRecorder] {frames}프레임, 게임 시간 합 {scaledSum:0.00}초, 실제 시간 합 {unscaledSum:0.00}초, 기대 {frames / (float)framesPerSecond:0.00}초");
			Time.captureFramerate = 0;
			IsRecording = false;
			Destroy(host);
		}
	}
}

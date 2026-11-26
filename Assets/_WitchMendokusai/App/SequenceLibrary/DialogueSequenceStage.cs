using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using WitchMendokusai.ContentRuntime;

namespace WitchMendokusai.SequenceLibrary
{
    // 대화 `@연출 <이름>` 의 3D 어댑터. 보관함 이름을 파일로 풀어 SequenceStage 로 표시
    // 대화 코어는 이 클래스를 모름. DialogueSceneStageBridge 로만 호출
    public sealed class DialogueSequenceStage : MonoBehaviour, IDialogueSceneStage
    {
        private SequenceStage stage;
        private string shownKey;
        private bool isFailureReported;

        public IReadOnlyList<string> SceneKeys { get { return SequenceCatalog.Keys; } }
        public bool IsShowing { get { return stage != null && stage.IsShowing; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            GameObject host = new GameObject("Dialogue sequence stage");
            DontDestroyOnLoad(host);
            host.AddComponent<DialogueSequenceStage>();
        }

        private void Awake()
        {
            stage = gameObject.AddComponent<SequenceStage>();
            DialogueSceneStageBridge.Register(this);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        public bool TryShow(string sceneKey, out string problem)
        {
            try
            {
                stage.Show(SequenceCatalog.FileOf(sceneKey));
                shownKey = sceneKey;
                isFailureReported = false;
                problem = null;
                return true;
            }
            catch (Exception exception)
            {
                problem = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
        }

        public void Hide()
        {
            stage.Hide();
        }

        // 씬 통째 전환이면 넘겨받은 카메라의 주인이 사라지므로 먼저 해제. 추가 로드는 유지
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single || IsShowing == false) return;
            Debug.LogWarning("[" + nameof(DialogueSequenceStage) + "] 씬 전환으로 연출을 닫음: " + shownKey);
            stage.Hide();
        }

        // 재생 중 계산 실패는 멈춘 화면으로만 보여서 한 번 오류 로그
        private void Update()
        {
            if (IsShowing == false || isFailureReported || stage.Player.Failure == null) return;
            isFailureReported = true;
            Debug.LogError("[" + nameof(DialogueSequenceStage) + "] 연출 재생 실패 " + shownKey + ": " + stage.Player.Failure);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            DialogueSceneStageBridge.Clear(this);
        }
    }
}

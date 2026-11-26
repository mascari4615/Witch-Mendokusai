using System;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.ContentRuntime;

namespace WitchMendokusai.SequenceLibrary
{
    public sealed partial class SequenceLibraryMenu
    {
        private SequenceStage stage;

        private SequenceStage Stage
        {
            get
            {
                if (stage == null) stage = gameObject.AddComponent<SequenceStage>();
                return stage;
            }
        }

        public void LoadSequence(string file)
        {
            if (opened == false) throw new InvalidOperationException("Open the sequence library before playback");
            try
            {
                Stage.Show(file);
                entries.style.display = DisplayStyle.None;
                transport.style.display = DisplayStyle.Flex;
                panel.style.backgroundColor = Color.clear;
                timeline.highValue = Stage.Duration;
                timeline.SetEnabled(Stage.Duration > 0);
                play.SetEnabled(Stage.Duration > 0);
                timeline.SetValueWithoutNotify(0);
                status.text = Path.GetFileName(file).Replace(SequenceCatalog.SUFFIX, "");
                play.Focus();
            }
            catch (Exception exception)
            {
                StopSequence();
                entries.style.display = DisplayStyle.Flex;
                transport.style.display = DisplayStyle.None;
                panel.style.backgroundColor = new Color(.08f, .09f, .12f, 1);
                status.text = "연출을 열지 못했어요: " + exception.Message;
            }
        }

        private void StopSequence()
        {
            if (stage != null) stage.Hide();
        }

        private void TogglePlayback()
        {
            if (Player == null || Player.IsLoaded == false) return;
            if (Player.IsPlaying) Player.Pause();
            else { if (Player.Time >= timeline.highValue) Player.Seek(0); Player.Play(); }
        }

        private void Update()
        {
            if (Player == null || Player.IsLoaded == false) return;
            timeline.SetValueWithoutNotify(Player.Time);
            if (Player.Failure != null) status.text = PlaybackFailure(Player.Failure);
            play.text = Player.IsPlaying ? "일시 정지" : "재생";
        }

        private static string PlaybackFailure(string detail)
        {
            if (detail == "Dynamics seek exceeds the step budget; advance in smaller intervals")
                return "한 번에 옮기기엔 시간이 너무 멀어요. 가까운 시간부터 나눠 이동하세요.";
            if (detail == "Obstacle encloses the particle reach" || detail == "Length and collision constraints could not be satisfied")
                return "충돌 모양과 연결 길이가 맞지 않아요. 편집기에서 반경이나 배치를 고쳐 다시 내보내세요.";
            return "연출 설정을 계산하지 못했어요. 편집기에서 확인하고 다시 내보내세요.";
        }
    }
}

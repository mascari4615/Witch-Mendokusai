using System;
using UnityEngine;

namespace Motion.Unity
{
    public sealed class PoseClipPlayer : MonoBehaviour
    {
        private PoseClipSampler sampler;
        private IPoseTarget target;
        private float time;
        public bool IsPlaying { get; private set; }
        public float Time { get { return time; } }

        public void Initialize(PoseClip clip, IPoseTarget poseTarget)
        {
            PoseClipSampler next = new PoseClipSampler(clip);
            next.Apply(0, poseTarget);
            sampler = next;
            target = poseTarget;
            time = 0;
            IsPlaying = false;
        }

        public void Play()
        {
            if (sampler == null)
            {
                throw new InvalidOperationException("Initialize a clip before playback");
            }
            IsPlaying = true;
        }

        public void Pause()
        {
            IsPlaying = false;
        }

        public void Seek(float seconds)
        {
            if (sampler == null)
            {
                throw new InvalidOperationException("Initialize a clip before seeking");
            }
            sampler.Apply(seconds, target);
            time = sampler.Loop ? ((seconds % sampler.Duration) + sampler.Duration) % sampler.Duration : Mathf.Clamp(seconds, 0, sampler.Duration);
        }

        private void Update()
        {
            if (IsPlaying == false)
            {
                return;
            }
            Seek(time + UnityEngine.Time.deltaTime);
            if (sampler.Loop == false && time >= sampler.Duration)
            {
                IsPlaying = false;
            }
        }
    }
}

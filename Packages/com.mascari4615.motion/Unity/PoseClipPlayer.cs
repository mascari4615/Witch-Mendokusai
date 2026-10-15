using System;
using UnityEngine;

namespace Motion.Unity
{
    public sealed class PoseClipPlayer : MonoBehaviour
    {
        private PoseClipSampler sampler;
        private IPoseTarget target;
        private IValueTarget values;
        private float time;
        public bool IsPlaying { get; private set; }
        public float Time { get { return time; } }

        public void Initialize(PoseClip clip, IPoseTarget poseTarget, IValueTarget valueTarget = null)
        {
            PoseClipSampler next = new PoseClipSampler(clip);
            next.Apply(0, poseTarget, valueTarget);
            sampler = next;
            target = poseTarget;
            values = valueTarget;
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
            sampler.Apply(seconds, target, values);
            time = ClipTime.Normalize(seconds, sampler.Duration, sampler.Loop);
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

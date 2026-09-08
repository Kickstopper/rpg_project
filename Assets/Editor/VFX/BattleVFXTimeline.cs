using System;

namespace RPGProject.Editor.VFX
{
    /// <summary>Variable-duration timeline, independent of scene time and asset state.</summary>
    public sealed class BattleVFXTimeline
    {
        private double[] ends = Array.Empty<double>();
        private double lastTime;
        public double Duration { get; private set; }
        public double Position { get; private set; }
        public int Frame { get; private set; } = -1;
        public bool Playing { get; private set; }
        public bool Finished { get; private set; }
        public bool Loop { get; set; } = true;
        public double Speed { get; set; } = 1;
        public bool Valid { get; private set; }
        public int Count => ends.Length;

        public void Configure(float[] durations, double now, bool reset)
        {
            int previous = Frame;
            bool play = Playing;
            ends = new double[durations == null ? 0 : durations.Length];
            Duration = 0;
            Valid = ends.Length > 0;
            for (int i = 0; i < ends.Length; i++)
            {
                float value = durations[i];
                if (!IsPositiveFinite(value)) Valid = false;
                Duration += IsPositiveFinite(value) ? value : 0;
                ends[i] = Duration;
            }
            SeekFrame(reset ? 0 : Math.Max(0, previous), now);
            Playing = play && Valid;
        }

        public static bool IsPositiveFinite(float value) => value > 0 && !float.IsNaN(value) && !float.IsInfinity(value);

        public void SetPlaying(bool play, double now)
        {
            Tick(now);
            if (play && Finished) SeekFrame(0, now);
            Playing = play && Valid;
            lastTime = now;
        }

        public void SeekFrame(int index, double now)
        {
            Frame = Count == 0 ? -1 : Math.Max(0, Math.Min(index, Count - 1));
            Position = Frame <= 0 ? 0 : ends[Frame - 1];
            Finished = false;
            Playing = false;
            lastTime = now;
        }

        public void SeekTime(double position, double now)
        {
            if (double.IsNaN(position) || double.IsInfinity(position)) return;
            Position = Math.Max(0, Math.Min(position, Duration));
            Frame = FindFrame(Position);
            Playing = false;
            Finished = false; // Scrubbing the endpoint intentionally shows the final frame.
            lastTime = now;
        }

        public double StartOf(int index) => index <= 0 || Count == 0 ? 0 : ends[Math.Min(index, Count) - 1];

        public bool Tick(double now)
        {
            if (double.IsNaN(now) || double.IsInfinity(now)) return false;
            double delta = Math.Max(0, now - lastTime);
            lastTime = now;
            if (!Playing || !Valid || Speed <= 0 || double.IsNaN(Speed) || double.IsInfinity(Speed)) return false;
            int previous = Frame;
            Position += delta * Speed;
            if (Position >= Duration)
            {
                if (Loop) Position %= Duration;
                else
                {
                    Position = Duration;
                    Frame = Count - 1;
                    Playing = false;
                    Finished = true;
                    return true;
                }
            }
            Frame = FindFrame(Position);
            return Frame != previous;
        }

        private int FindFrame(double position)
        {
            if (Count == 0) return -1;
            int low = 0, high = Count - 1;
            while (low < high)
            {
                int middle = low + (high - low) / 2;
                if (position < ends[middle]) high = middle;
                else low = middle + 1;
            }
            return low;
        }
    }
}

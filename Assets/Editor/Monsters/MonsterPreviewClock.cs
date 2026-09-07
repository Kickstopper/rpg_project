using System;

namespace MonsterEditing
{
    /// <summary>Editor-time playback; no dependency on scene time, coroutines or asset state.</summary>
    public sealed class MonsterPreviewClock
    {
        public int Frame { get; private set; }
        public int FrameCount { get; private set; }
        public double Interval { get; private set; }
        public bool Playing { get; private set; }
        public bool CanPlay => FrameCount > 1 && Interval > 0 && !double.IsNaN(Interval) && !double.IsInfinity(Interval);
        private int originFrame;
        private double phaseStart, lastTime, pausedFraction;

        public void Configure(int count, double interval, double now, bool resetFrame)
        {
            FrameCount = Math.Max(0, count); Interval = interval;
            Frame = resetFrame || FrameCount == 0 ? 0 : Math.Min(Frame, FrameCount - 1);
            originFrame = Frame; phaseStart = lastTime = now; pausedFraction = 0;
            if (!CanPlay) Playing = false;
        }

        public void SetPlaying(bool play, double now)
        {
            Tick(now);
            if (Playing && CanPlay) pausedFraction = Elapsed(now) % Interval;
            originFrame = Frame; phaseStart = lastTime = now;
            Playing = play && CanPlay;
        }

        public void Seek(int frame, double now)
        {
            Frame = FrameCount == 0 ? 0 : Math.Max(0, Math.Min(frame, FrameCount - 1));
            originFrame = Frame; phaseStart = lastTime = now;
            Playing = false; pausedFraction = 0;
        }

        private double Elapsed(double now) => pausedFraction + Math.Max(0, now - phaseStart);

        public bool Tick(double now)
        {
            if (double.IsNaN(now) || double.IsInfinity(now)) return false;
            if (now < lastTime) { phaseStart += now - lastTime; lastTime = now; return false; }
            lastTime = now;
            if (!Playing || !CanPlay) return false;
            // Absolute elapsed time avoids accumulating fractional errors on every editor update.
            // Modulo before integer conversion also avoids loops/overflow after a long editor stall.
            double inCycle = Elapsed(now) % (Interval * FrameCount);
            long steps = (long)Math.Floor(inCycle / Interval);
            int previous = Frame;
            Frame = (int)((originFrame + steps) % FrameCount);
            return Frame != previous;
        }
    }
}

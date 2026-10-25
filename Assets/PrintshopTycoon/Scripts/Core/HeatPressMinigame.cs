using System;

namespace PrintshopTycoon.Core
{
    public enum PressResult { Miss, Good, Perfect }

    /// <summary>
    /// Timing minigame: an indicator sweeps back and forth over a bar; stop it inside the
    /// target zone. Pure logic so it can be unit tested and driven by any UI.
    /// </summary>
    public class HeatPressMinigame
    {
        readonly BalanceData balance;
        readonly Random random;

        public float Position { get; private set; }   // 0..1
        public float TargetCenter { get; private set; }
        public bool Running { get; private set; }
        float direction = 1f;

        public float GoodHalfWidth => balance.goodZoneWidth * 0.5f;
        public float PerfectHalfWidth => balance.perfectZoneWidth * 0.5f;

        public HeatPressMinigame(BalanceData balance, int seed)
        {
            this.balance = balance;
            random = new Random(seed);
        }

        public void Start()
        {
            // Keep the zone fully inside the bar.
            float margin = GoodHalfWidth + 0.05f;
            TargetCenter = margin + (float)random.NextDouble() * (1f - 2f * margin);
            Position = 0f;
            direction = 1f;
            Running = true;
        }

        public void Tick(float deltaTime)
        {
            if (!Running || deltaTime <= 0f) return;
            Position += direction * balance.indicatorSpeed * deltaTime;
            // Ping-pong, also for large steps.
            while (Position > 1f || Position < 0f)
            {
                if (Position > 1f) { Position = 2f - Position; direction = -1f; }
                else if (Position < 0f) { Position = -Position; direction = 1f; }
            }
        }

        public PressResult Stop()
        {
            Running = false;
            return Judge(Position, TargetCenter, GoodHalfWidth, PerfectHalfWidth);
        }

        public static PressResult Judge(float position, float center, float goodHalfWidth, float perfectHalfWidth)
        {
            float distance = Math.Abs(position - center);
            if (distance <= perfectHalfWidth) return PressResult.Perfect;
            if (distance <= goodHalfWidth) return PressResult.Good;
            return PressResult.Miss;
        }
    }
}

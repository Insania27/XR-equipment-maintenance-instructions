using System;

namespace Ps5Maintenance
{
    // Pure state machine: UI, AR tracking and repositories do not control step order.
    public sealed class MaintenanceSession
    {
        public int Index { get; private set; }
        public int StepCount { get; }
        public bool Finished { get; private set; }
        public bool[] Completed { get; }
        public MaintenanceSession(int count)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            StepCount = count;
            Completed = new bool[count];
        }
        public bool Advance(bool confirmed, bool markerVisible, bool modelAllowed)
        {
            if (Finished || !confirmed || !markerVisible || !modelAllowed) return false;
            Completed[Index] = true;
            if (Index == StepCount - 1) Finished = true;
            else Index++;
            return true;
        }
        public bool Back()
        {
            if (Finished || Index == 0) return false;
            Index--;
            return true;
        }
    }
}

using System;
using System.Threading;

namespace dnGREP.Common
{
    /// <summary>
    /// Limits the estimated memory used by files that are searched at the same time.
    /// A worker asks for the estimated cost of a file before starting it and gives the cost
    /// back when done. The gate never blocks the only active file, so a single file that
    /// costs more than the whole budget still runs, alone.
    /// </summary>
    public sealed class SearchMemoryGate
    {
        private const int PollMilliseconds = 250;
        private const double BudgetFraction = 0.25;
        private const double PressureFraction = 0.75;

        private readonly object sync = new();
        private readonly Func<long> workingSet;
        private readonly long workingSetLimit;
        private long inUse;
        private int active;

        /// <param name="budgetBytes">Total estimated bytes that concurrent files may use.</param>
        /// <param name="workingSetLimitBytes">While the process working set is above this, a new file
        /// only starts when no other file is being searched. Zero disables the check.</param>
        /// <param name="workingSetProvider">Overrides the process working set, for tests.</param>
        public SearchMemoryGate(long budgetBytes, long workingSetLimitBytes = 0, Func<long>? workingSetProvider = null)
        {
            Budget = Math.Max(1, budgetBytes);
            workingSetLimit = workingSetLimitBytes;
            workingSet = workingSetProvider ?? (() => Environment.WorkingSet);
        }

        /// <summary>
        /// Creates a gate sized from the memory available to the process.
        /// </summary>
        /// <param name="budgetMegabytes">A fixed budget in MB, or zero or less to use a quarter of available memory.</param>
        public static SearchMemoryGate Create(int budgetMegabytes)
        {
            long available = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            if (available <= 0)
            {
                available = 8L * 1024 * 1024 * 1024;
            }

            long budget = budgetMegabytes > 0 ? budgetMegabytes * 1024L * 1024 : (long)(available * BudgetFraction);
            return new SearchMemoryGate(budget, (long)(available * PressureFraction));
        }

        public long Budget { get; }

        public int ActiveCount
        {
            get { lock (sync) { return active; } }
        }

        public long InUse
        {
            get { lock (sync) { return inUse; } }
        }

        /// <summary>
        /// Waits until a file with the estimated cost may start. Returns the amount reserved,
        /// which must be passed to <see cref="Exit"/>.
        /// </summary>
        /// <exception cref="OperationCanceledException">The search was canceled while waiting.</exception>
        public long Enter(long cost, PauseCancelToken pauseCancelToken)
        {
            cost = Math.Clamp(cost, 1, Budget);

            while (true)
            {
                lock (sync)
                {
                    if (active == 0 || (inUse + cost <= Budget && !UnderMemoryPressure()))
                    {
                        inUse += cost;
                        active++;
                        return cost;
                    }

                    Monitor.Wait(sync, PollMilliseconds);
                }

                // outside the lock: this blocks while the search is paused
                pauseCancelToken.WaitWhilePausedOrThrowIfCancellationRequested();
            }
        }

        public void Exit(long reserved)
        {
            lock (sync)
            {
                inUse -= reserved;
                active--;
                Monitor.PulseAll(sync);
            }
        }

        private bool UnderMemoryPressure() => workingSetLimit > 0 && workingSet() > workingSetLimit;
    }
}

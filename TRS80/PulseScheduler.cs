using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sharp80.TRS80
{
    internal class PulseScheduler
    {
        // Note: PulseScheduler does not support serialization. It's up to individual 
        // compontents to serialize and deserialize their pulse reqs and register them.
        private List<PulseReq> pulseReqs = new List<PulseReq>();
        private ulong nextPulseReqTick = UInt64.MaxValue;
        public void Execute(ulong TickCount)
        {
            if (TickCount > nextPulseReqTick)
            {
                // descending to avoid problems with new reqs being added during the trigger callback
                for (int i = pulseReqs.Count - 1; i >= 0; i--)
                {
                    if (TickCount > pulseReqs[i].Trigger)
                        pulseReqs[i].Execute();
                }
                pulseReqs.RemoveAll(req => req.Inactive);
                if (pulseReqs.Count == 0)
                {
                    nextPulseReqTick = ulong.MaxValue;
                }
                else
                {
                    lock (pulseReqs)
                        nextPulseReqTick = pulseReqs.Min(req => req.Trigger);
                }
            }
        }
        internal void RegisterPulseReq(PulseReq Req, bool SetTrigger, ulong TickCount)
        {
            if (SetTrigger)
                Req.SetTrigger(BaselineTicks: TickCount);

            System.Diagnostics.Debug.Assert(Req.Active);

            lock (pulseReqs)
            {
                if (!pulseReqs.Contains(Req) && !Req.Inactive)
                    pulseReqs.Add(Req);
                nextPulseReqTick = pulseReqs.Min(req => req.Trigger);
            }
        }
    }
}
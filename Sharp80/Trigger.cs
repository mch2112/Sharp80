using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sharp80
{
    internal class Trigger : ISerializable
    {
        private bool enabled;
        private bool latched;
        private bool triggerLock;
        private bool canFireOnEnable;

        public delegate void LatchDelegate();

        private event LatchDelegate Fired;
        private event LatchDelegate Reset;

        public Trigger(LatchDelegate FireCallback, LatchDelegate ResetCallback, bool TriggerLock = false, bool CanFireOnEnable = false)
        {
            Fired += FireCallback;
            Reset += ResetCallback;

            triggerLock = TriggerLock;
            canFireOnEnable = CanFireOnEnable;
        }

        public bool Triggered { get; private set; }
        public void ResetTrigger() { Triggered = false; }

        public bool Enabled
        {
            get { return enabled; }
            set
            {
                update(Enabled: value, Latched: null);
            }
        }
        public bool Latched
        {
            get { return latched; }
        }

        public void Latch()
        {
            update(Enabled: null, Latched: true);
        }
        public void Unlatch()
        {
            update(Enabled: null, Latched: false);
        }

        public void Serialize(System.IO.BinaryWriter Writer)
        {
            Writer.Write(enabled);
            Writer.Write(latched);
            Writer.Write(triggerLock);
            Writer.Write(canFireOnEnable);
            Writer.Write(Triggered);
        }
        public void Deserialize(System.IO.BinaryReader Reader)
        {
            enabled = Reader.ReadBoolean();
            latched = Reader.ReadBoolean();
            triggerLock = Reader.ReadBoolean();
            canFireOnEnable = Reader.ReadBoolean();
            Triggered = Reader.ReadBoolean();
        }

        private void update(bool? Enabled, bool? Latched)
        {
            System.Diagnostics.Debug.Assert(!Enabled.HasValue || !Latched.HasValue);

            bool wasLatchedAndEnabled = this.latched && this.enabled;

            this.enabled = Enabled ?? this.enabled;
            this.latched = Latched ?? this.latched;

            if (latched && enabled)
            {
                if (!wasLatchedAndEnabled && (Latched.HasValue || canFireOnEnable))
                {
                    Triggered = true;
                    if (Fired != null)
                        Fired();
                }
            }
            else
            {
                if (!triggerLock)
                    ResetTrigger();

                if (wasLatchedAndEnabled)
                    if (Reset != null)
                        Reset();
            }
        }
    }
}

/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;

namespace Sharp80
{
    /// <summary>
    /// Triggers are used to fire events. The main ones are used for the floppy
    /// drive events (motor on and non maskable interrupt request, and the
    /// reset button. Triggers exist for other devices that aren't currently
    /// used.
    /// </summary>
    internal class Trigger : ISerializable
    {
        public bool Triggered { get; private set; }

        private bool enabled;
        private bool latched;
        private bool triggerLock;
        private bool canLatchBeforeEnabled;

        public delegate void LatchDelegate();

        private event LatchDelegate Fired;
        private event LatchDelegate Reset;

        public Trigger(LatchDelegate FireCallback, LatchDelegate ResetCallback, bool TriggerLock = false, bool CanLatchBeforeEnabled = false)
        {
            Fired += FireCallback;
            Reset += ResetCallback;

            triggerLock = TriggerLock;
            canLatchBeforeEnabled = CanLatchBeforeEnabled;
        }

        public void ResetTrigger() { Triggered = false; }

        public bool Enabled
        {
            get { return enabled; }
            set
            {
                Update(Enabled: value, Latched: null);
            }
        }
        public bool Latched
        {
            get { return latched; }
        }
        public void LatchIf(bool Latch)
        {
            Update(Enabled: null, Latched: Latch);
        }
        public void Latch()
        {
            Update(Enabled: null, Latched: true);
        }
        public void Unlatch()
        {
            Update(Enabled: null, Latched: false);
        }
        public void Serialize(System.IO.BinaryWriter Writer)
        {
            Writer.Write(enabled);
            Writer.Write(latched);
            Writer.Write(triggerLock);
            Writer.Write(canLatchBeforeEnabled);
            Writer.Write(Triggered);
        }
        public void Deserialize(System.IO.BinaryReader Reader)
        {
            enabled = Reader.ReadBoolean();
            latched = Reader.ReadBoolean();
            triggerLock = Reader.ReadBoolean();
            canLatchBeforeEnabled = Reader.ReadBoolean();
            Triggered = Reader.ReadBoolean();
        }

        private void Update(bool? Enabled, bool? Latched)
        {
            System.Diagnostics.Debug.Assert(!Enabled.HasValue || !Latched.HasValue);

            bool wasLatchedAndEnabled = latched && enabled;

            enabled = Enabled ?? enabled;

            if (Enabled == false && !canLatchBeforeEnabled)
                latched = false;
            else if (Latched == true && (enabled || canLatchBeforeEnabled))
                latched = true;
            else
                latched = Latched ?? latched;

            if (latched && enabled)
            {
                if (!Triggered)
                {
                    Triggered = true;
                    Fired?.Invoke();
                }
            }
            else
            {
                if (!triggerLock)
                    ResetTrigger();

                if (wasLatchedAndEnabled)
                    Reset?.Invoke();
            }
        }
        public override string ToString()
        {
            return String.Format("Latched: {0} Enabled: {1} Triggered: {2}", Latched, Enabled, Triggered);
        }
    }
}

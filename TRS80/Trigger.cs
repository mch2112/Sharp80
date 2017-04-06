/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;

namespace Sharp80.TRS80
{
    /// <summary>
    /// Triggers are used to fire events. The main ones are used for the floppy
    /// drive events (motor on and non maskable interrupt request), cassette
    /// port events, and the reset button. Triggers exist for other devices that
    /// aren't currently used.
    /// </summary>
    internal class Trigger : ISerializable
    {
        public bool Triggered { get; private set; }
        public bool Latched { get; private set; }

        private bool enabled;
        private bool triggerLock;
        private bool canLatchBeforeEnabled;

        private event Action Fired;
        private event Action Reset;

        public Trigger(Action FireCallback, Action ResetCallback, bool TriggerLock = false, bool CanLatchBeforeEnabled = false)
        {
            Fired += FireCallback;
            Reset += ResetCallback;

            triggerLock = TriggerLock;
            canLatchBeforeEnabled = CanLatchBeforeEnabled;
        }
        public bool Enabled
        {
            get => enabled;
            set => Update(Enabled: value, Latched: null);
        }
        public void LatchIf(bool Latch) => Update(Enabled: null, Latched: Latch);
        public void Latch() => LatchIf(true);
        public void Unlatch() => LatchIf(false);
        public void ResetTrigger() => Triggered = false;

        public void Serialize(System.IO.BinaryWriter Writer)
        {
            Writer.Write(enabled);
            Writer.Write(Latched);
            Writer.Write(triggerLock);
            Writer.Write(canLatchBeforeEnabled);
            Writer.Write(Triggered);
        }
        public bool Deserialize(System.IO.BinaryReader Reader, int DeserilizationVersion)
        {
            try
            {
                enabled = Reader.ReadBoolean();
                Latched = Reader.ReadBoolean();
                triggerLock = Reader.ReadBoolean();
                canLatchBeforeEnabled = Reader.ReadBoolean();
                Triggered = Reader.ReadBoolean();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void Update(bool? Enabled, bool? Latched)
        {
            System.Diagnostics.Debug.Assert(!Enabled.HasValue || !Latched.HasValue);

            bool wasLatchedAndEnabled = this.Latched && enabled;

            enabled = Enabled ?? enabled;

            if (Enabled == false && !canLatchBeforeEnabled)
                this.Latched = false;
            else if (Latched == true && (enabled || canLatchBeforeEnabled))
                this.Latched = true;
            else
                this.Latched = Latched ?? this.Latched;

            if (this.Latched && enabled)
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
            return $"Latched: {Latched} Enabled: {Enabled} Triggered: {Triggered}";
        }
    }
}

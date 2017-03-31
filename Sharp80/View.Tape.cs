using System;
using System.Text;

namespace Sharp80
{
    internal class ViewTape : View
    {
        protected override ViewMode Mode => ViewMode.Tape;
        protected override bool ForceRedraw => Computer.TapeStatus == TRS80.TapeStatus.Reading || Computer.TapeStatus == TRS80.TapeStatus.Writing;
        protected override bool CanSendKeysToEmulation => false;

        protected override bool processKey(KeyState Key)
        {
            if (Key.Pressed && Key.IsUnmodified)
            {
                Invalidate();
                switch (Key.Key)
                {
                    case KeyCode.B:
                        if (Storage.SaveTapeIfRequired(Computer))
                        {
                            Computer.TapeLoadBlank();
                            Settings.LastTapeFile = Computer.TapeFilePath;
                            MessageCallback("Blank Tape Loaded");
                        }
                        break;
                    case KeyCode.E:
                        if (Storage.SaveTapeIfRequired(Computer))
                        {
                            Computer.TapeEject();
                            MessageCallback("Tape Ejected");
                        }
                        break;
                    case KeyCode.L:
                        if (Storage.SaveTapeIfRequired(Computer))
                            Load();
                        break;
                    case KeyCode.P:
                        Computer.TapePlay();
                        MessageCallback("Playing Tape");
                        break;
                    case KeyCode.S:
                        Computer.TapeStop();
                        MessageCallback("Tape Stopped");
                        break;
                    case KeyCode.R:
                        Computer.TapeRecord();
                        switch (Computer.TapeStatus)
                        {
                            case TRS80.TapeStatus.WriteEngaged:
                                MessageCallback("Waiting to Record");
                                break;
                            case TRS80.TapeStatus.Writing:
                                MessageCallback("Recording");
                                break;
                        }
                        break;
                    case KeyCode.W:
                        Computer.TapeRewind();
                        MessageCallback("Tape Rewound");
                        break;
                    case KeyCode.X:
                        Computer.TapeUserSelectedSpeed = Computer.TapeUserSelectedSpeed == TRS80.Baud.High ? TRS80.Baud.Low : TRS80.Baud.High;
                        break;
                    case KeyCode.F8:
                        if (!Computer.IsRunning)
                            CurrentMode = ViewMode.Normal;
                        return base.processKey(Key);
                    default:
                        return base.processKey(Key);
                }

                return true;
            }
            else
            {
                return base.processKey(Key);
            }
        }

        protected override byte[] GetViewBytes()
        {
            string fileName = Computer.TapeFilePath;

            return PadScreen(Encoding.ASCII.GetBytes(
                Header("Sharp 80 Tape Management") +
                Format(string.Format("Speed Selected: {0} Baud", Computer.TapeUserSelectedSpeed == TRS80.Baud.High ? "High 1500" : "Low 500")) +
                Format("Tape File: " + FitFilePath(fileName, ScreenMetrics.NUM_SCREEN_CHARS_X - "Tape File: ".Length)) +
                Format(string.Format(@"{0:0000.0} ({1:00.0%})", Computer.TapeCounter, Computer.TapePercent)) +
                Format(string.Format("{0} {1} {2}",
                       StatusToString(Computer.TapeStatus),
                       Computer.TapeIsBlank ? String.Empty : Computer.TapeSpeed == TRS80.Baud.High ? "1500 Baud" : "500 Baud",
                       Computer.TapeMotorOn ? Computer.TapePulseStatus : String.Empty)) +
                Separator() +
                Format("[L] Load from file") +
                Format("[B] Load blank tape") +
                Format() +
                Format("[P] Play    [R] Record") +
                Format("[S] Stop    [W] Rewind") +
                Format() +
                Format("[E] Eject") +
                Format() +
                Format("[X] Toggle user selected speed")
                ));
        }
        private void Load()
        {
            var path = Storage.GetDefaultTapeFileName();

            if (String.IsNullOrWhiteSpace(path))
                path = Storage.FILE_NAME_NEW;

            bool selectFile = true;

            if (Storage.IsFileNameToken(path))
            {
                path = System.IO.Path.Combine(Storage.AppDataPath, "Tapes\\");
                selectFile = false;
            }
            path = Storage.GetTapeFilePath(Prompt: "Select tape file to load",
                                     DefaultPath: path,
                                     Save: false,
                                     SelectFileInDialog: selectFile);

            if (path.Length > 0)
            {
                if (Computer.TapeLoad(path))
                {
                    Settings.LastTapeFile = Computer.TapeFilePath;
                }
                else
                {
                    Dialogs.AlertUser("Failed to load tape file.");
                    return;
                }
            }    
            MessageCallback("Tape Loaded");
        }
        private string StatusToString(TRS80.TapeStatus Status)
        {
            switch (Status)
            {
                case TRS80.TapeStatus.Stopped:
                    return "Stopped";
                case TRS80.TapeStatus.ReadEngaged:
                    return "Play Engaged";
                case TRS80.TapeStatus.WriteEngaged:
                    return "Record Engaged";
                case TRS80.TapeStatus.Reading:
                    return "Playing";
                case TRS80.TapeStatus.Writing:
                    return "Recording";
                case TRS80.TapeStatus.Waiting:
                    return "Waiting";
                default:
                    return "Error";
            }
        }
    }
}

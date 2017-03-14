using System;
using System.Text;

namespace Sharp80
{
    internal class ViewTape : View
    {
        protected override ViewMode Mode => ViewMode.Tape;
        protected override bool ForceRedraw => Computer.TapeStatus == TapeStatus.Reading || Computer.TapeStatus == TapeStatus.Writing;

        protected override bool processKey(KeyState Key)
        {
            if (Key.Pressed)
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
                            case TapeStatus.WriteEngaged:
                                MessageCallback("Waiting to Record");
                                break;
                            case TapeStatus.Writing:
                                MessageCallback("Recording");
                                break;
                        }
                        break;
                    case KeyCode.W:
                        Computer.TapeRewind();
                        MessageCallback("Tape Rewound");
                        break;
                    case KeyCode.X:
                        Computer.TapeUserSelectedSpeed = Computer.TapeUserSelectedSpeed == Baud.High ? Baud.Low : Baud.High;
                        break;
                    case KeyCode.F8:
                        if (!Computer.IsRunning)
                            CurrentMode = ViewMode.Normal;
                        return base.processKey(Key);
                    default:
                        return base.processKey(Key);
                }
            }
            return true;
        }

        protected override byte[] GetViewBytes()
        {
            string fileName = Computer.TapeFilePath;

            return PadScreen(Encoding.ASCII.GetBytes(
                Header("Sharp 80 Tape Management") +
                Format(string.Format("Speed Selected: {0} Baud", Computer.TapeUserSelectedSpeed == Baud.High ? "High 1500" : "Low 500")) +
                Format("Tape File: " + FitFilePath(fileName, ScreenMetrics.NUM_SCREEN_CHARS_X - "Tape File: ".Length)) +
                Format(string.Format(@"{0:0000.0} ({1:00.0%})", Computer.TapeCounter, Computer.TapePercent)) +
                Format(string.Format("{0} {1} {2}",
                       StatusToString(Computer.TapeStatus),
                       Computer.TapeIsBlank ? String.Empty : Computer.TapeSpeed == Baud.High ? "1500 Baud" : "500 Baud",
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
                if (!Computer.TapeLoad(path))
                {
                    Dialogs.AlertUser("Failed to load tape file.");
                    return;
                }
            }
            Settings.LastTapeFile = Computer.TapeFilePath;
            MessageCallback("Tape Loaded");
        }
        private string StatusToString(TapeStatus Status)
        {
            switch (Status)
            {
                case TapeStatus.Stopped:
                    return "Stopped";
                case TapeStatus.ReadEngaged:
                    return "Play Engaged";
                case TapeStatus.WriteEngaged:
                    return "Record Engaged";
                case TapeStatus.Reading:
                    return "Playing";
                case TapeStatus.Writing:
                    return "Recording";
                case TapeStatus.Waiting:
                    return "Waiting";
                default:
                    return "Error";
            }
        }
    }
}

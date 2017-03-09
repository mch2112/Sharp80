/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3

using System;

namespace Sharp80
{
    internal partial class SoundX : ISound, IDisposable
    {

        private class Noise
        {
            private const double TRACK_STEP_WAV_1_FREQ = 783.99; // G
            private const double TRACK_STEP_WAV_2_FREQ = 523.25; // C

            private const double DRIVE_NOISE_WAV_1_FREQ = 251;
            private const double DRIVE_NOISE_WAV_2_FREQ = 133;

            private const double DRIVE_NOISE_WAV_1_PERIOD = SAMPLE_RATE / DRIVE_NOISE_WAV_1_FREQ;
            private const double DRIVE_NOISE_WAV_2_PERIOD = SAMPLE_RATE / DRIVE_NOISE_WAV_2_FREQ;

            private const double MAX_DRIVE_NOISE_AMP_1 = MAX_SOUND_AMPLITUDE / 27.0d;
            private const double MAX_DRIVE_NOISE_AMP_2 = MAX_SOUND_AMPLITUDE / 23.0d;

            private const double MAX_TRACK_NOISE_AMP = MAX_SOUND_AMPLITUDE / 30.0d;

            private const int DRIVE_NOISE_SAMPLE_SIZE = (int)(DRIVE_NOISE_WAV_1_PERIOD * DRIVE_NOISE_WAV_2_PERIOD * 2.0 * Math.PI);
            private const int TRACK_STEP_NOISE_SAMPLE_SIZE = SAMPLE_RATE / 6;

            private short[] driveNoise;
            private short[] trackStepNoise;
            private double trackWav1Period, trackWav2Period;

            Random r = new Random();

            private int noiseCursor, trackStepCursor;

            public Noise(int SampleRate)
            {
                trackWav1Period = SampleRate / TRACK_STEP_WAV_1_FREQ;
                trackWav2Period = SampleRate / TRACK_STEP_WAV_2_FREQ;

                driveNoise = new short[DRIVE_NOISE_SAMPLE_SIZE];
                trackStepNoise = new short[TRACK_STEP_NOISE_SAMPLE_SIZE];

                for (int i = 0; i < DRIVE_NOISE_SAMPLE_SIZE; ++i)
                    driveNoise[i] = (short)(MAX_DRIVE_NOISE_AMP_1 * Math.Sin(i / DRIVE_NOISE_WAV_1_PERIOD * (Math.PI * 2)) +
                                            MAX_DRIVE_NOISE_AMP_2 * Math.Sin(i / DRIVE_NOISE_WAV_2_PERIOD * (Math.PI * 2))
                                            // random noise
                                            * (0.85 + 0.15 * ((double)r.Next() / (double)(int.MaxValue)))
                                            );

                for (int i = 0; i < TRACK_STEP_NOISE_SAMPLE_SIZE; ++i)
                {
                    trackStepNoise[i] = (short)(((MAX_TRACK_NOISE_AMP * Math.Sin(i / trackWav1Period * (Math.PI * 2)))
                                               + (MAX_TRACK_NOISE_AMP * Math.Sin(i / trackWav2Period * (Math.PI * 2)))
                                          )
                                          // Fade in/out
                                          //* (1.0 - (Math.Abs((i - TRACK_STEP_NOISE_SAMPLE_SIZE / 2.0) / (TRACK_STEP_NOISE_SAMPLE_SIZE / 2.0))))
                                          // Random noise
                                          //* (0.85 + 0.15 * ((double)r.Next() / (double)(int.MaxValue)))
                                          );
                }
                Reset();
            }
            public void Reset()
            {
                trackStepCursor = TRACK_STEP_NOISE_SAMPLE_SIZE;
                noiseCursor = 0;
            }
            public short GetNoiseSample()
            {
                var sample = driveNoise[noiseCursor];
                noiseCursor = ++noiseCursor % DRIVE_NOISE_SAMPLE_SIZE;

                if (trackStepCursor < TRACK_STEP_NOISE_SAMPLE_SIZE)
                    sample += trackStepNoise[trackStepCursor++];

                return sample;
            }
            public void TrackStep()
            {
                trackStepCursor = 0;
            }
        }
    }
}

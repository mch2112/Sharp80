using System;

namespace Sharp80.TRS80
{
    public class Noise
    {
        private const double TRACK_STEP_WAV_1_FREQ = 783.99; // G
        private const double TRACK_STEP_WAV_2_FREQ = 523.25; // C

        private const double DRIVE_NOISE_WAV_1_FREQ = 251;
        private const double DRIVE_NOISE_WAV_2_FREQ = 133;

        private double wav1Period;
        private double wav2Period;

        private double maxDriveNoiseAmp1;
        private double maxDriveNoiseAmp2;

        private double maxTrackNoiseAmp;

        private int driveNoiseSampleSize;
        private int trackStepNoiseSampleSize;

        private short[] driveNoise;
        private short[] trackStepNoise;
        private double trackWav1Period, trackWav2Period;

        Random r = new Random();

        private int noiseCursor, trackStepCursor;

        public Noise(int SampleRate, int MaxSoundAmplitude)
        {
            wav1Period = SampleRate / DRIVE_NOISE_WAV_1_FREQ;
            wav2Period = SampleRate / DRIVE_NOISE_WAV_2_FREQ;

            maxDriveNoiseAmp1 = MaxSoundAmplitude / 27.0d;
            maxDriveNoiseAmp2 = MaxSoundAmplitude / 23.0d;
            maxTrackNoiseAmp = MaxSoundAmplitude / 30.0d;

            trackWav1Period = SampleRate / TRACK_STEP_WAV_1_FREQ;
            trackWav2Period = SampleRate / TRACK_STEP_WAV_2_FREQ;

            driveNoiseSampleSize = (int)(wav1Period * wav2Period * 2.0 * Math.PI);
            trackStepNoiseSampleSize = SampleRate / 6;

            driveNoise = new short[driveNoiseSampleSize];
            trackStepNoise = new short[trackStepNoiseSampleSize];

            for (int i = 0; i < driveNoiseSampleSize; ++i)
                driveNoise[i] = (short)(maxDriveNoiseAmp1 * Math.Sin(i / wav1Period * (Math.PI * 2)) +
                                        maxDriveNoiseAmp2 * Math.Sin(i / wav2Period * (Math.PI * 2))
                                        // random noise
                                        * (0.85 + 0.15 * ((double)r.Next() / (double)(int.MaxValue)))
                                        );

            for (int i = 0; i < trackStepNoiseSampleSize; ++i)
            {
                trackStepNoise[i] = (short)(((maxTrackNoiseAmp * Math.Sin(i / trackWav1Period * (Math.PI * 2)))
                                           + (maxTrackNoiseAmp * Math.Sin(i / trackWav2Period * (Math.PI * 2)))
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
            trackStepCursor = trackStepNoiseSampleSize;
            noiseCursor = 0;
        }
        public short GetNoiseSample()
        {
            var sample = driveNoise[noiseCursor];
            noiseCursor = ++noiseCursor % driveNoiseSampleSize;

            if (trackStepCursor < trackStepNoiseSampleSize)
                sample += trackStepNoise[trackStepCursor++];

            return sample;
        }

        public void TrackStep() => trackStepCursor = 0;
    }
}
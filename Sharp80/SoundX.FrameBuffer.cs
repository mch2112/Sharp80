/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using SharpDX;

namespace Sharp80
{
    internal partial class SoundX : ISound, IDisposable
    {
        /// <summary>
        /// A circular frame buffer with read and write cursors and support for managing
        /// reading and writing out of sync
        /// </summary>
        private class FrameBuffer<T> where T : struct
        {
            private int bufferSize;
            private int readCursor;
            private int writeCursor;
            private int frameSize;
            private int minLatency;
            private int maxLatency;

            // has the write cursor wrapped around and is now before
            // the read cursor?
            private bool writeWrap;

            private T[] buffer;
            private T[] silentFrame;

            public FrameBuffer(int FrameSize, int MinLatencySamples)
            {
                frameSize = FrameSize;
                
                minLatency = MinLatencySamples;
                maxLatency = minLatency * 3;

                // don't skimp
                bufferSize = 10 * maxLatency;

                buffer = new T[bufferSize];
                silentFrame = new T[frameSize];

                Reset();
            }

            public void Sample(T Val)
            {
                if (Latency < minLatency / 2)
                {
                    // Samples not fast enough, so
                    // double this sample
                    WriteToBuffer(Val);
                    WriteToBuffer(Val);
                }
                else if (Latency < minLatency)
                {
                    // Double some of the samples
                    WriteToBuffer(Val);
                    if (writeCursor % 2 == 0)
                        WriteToBuffer(Val);
                }
                else if (Latency > maxLatency)
                {
                    // Samples coming in too fast: drop sample
                }
                else
                {
                    // Normal 
                    WriteToBuffer(Val);
                }
                CheckOverread();
            }

            private void WriteToBuffer(T Val)
            {
                buffer[writeCursor++] = Val;
                ZeroWriteCursor();
            }

            public void ReadSilentFrame(DataPointer Buffer)
            {
                Buffer.CopyFrom(silentFrame, 0, frameSize);
            }
            public void ReadFrame(DataPointer Buffer)
            {
                int startFrame = readCursor;
                Buffer.CopyFrom(buffer, readCursor, frameSize);

                readCursor += frameSize;

                if (readCursor >= bufferSize)
                {
                    readCursor = 0;
                    if (writeWrap)
                        writeWrap = false;
                    else
                        Reset();
                }
                CheckOverread();
            }
            public void Reset()
            {
                readCursor = 0;
                writeCursor = maxLatency * 2;
                Array.Clear(buffer, 0, writeCursor);
                writeWrap = false;
            }
            private int Latency
            {
                get { return (writeCursor + bufferSize - readCursor) % bufferSize; }
            }
            private void ZeroWriteCursor()
            {
                if (writeCursor >= bufferSize)
                {
                    writeCursor = 0;
                    if (writeWrap)
                        Reset();  // Overwrite: the write cursor has completely lapped the read cursor: punt
                    else
                        writeWrap = true;
                }
            }
            private void CheckOverread()
            {
                if ((writeWrap && writeCursor > readCursor) || (!writeWrap && writeCursor < readCursor + frameSize))
                {
                    // We've exhausted our latency and the read cursor 
                    // has run past the write cursor
                    Reset();
                }
            }
        }
    }
}

using SharpDX;
using System;

namespace Sharp80
{
    internal partial class SoundX : ISound, IDisposable
    {
        private class FrameBuffer<T> where T:struct
        {
            private int bufferSize;
            private int readCursor;
            private int writeCursor;
            private int frameSize;
            private int latencyLimit;

            private bool writeWrap;

            private T[] buffer;
            private T[] silentFrame;

            // Debug info
            private long resets, wrapArounds, drops, doubles, /* overreads, */ frameReads, total;
            
            public FrameBuffer(int FrameSize, int MaxLatencyFrames)
            {
                frameSize = FrameSize;
                bufferSize = 100 * frameSize;

                latencyLimit = MaxLatencyFrames * frameSize;
                
                buffer = new T[bufferSize];
                silentFrame = new T[frameSize];
                
                Reset();
            }
            public bool FrameReady
            {
                get { return Latency > frameSize; }
            }
            public void Sample(T Val)
            {
                total++;
                /*if (Latency < minLatency)
                {
                    doubles++;
                    // double sample
                    buffer[writeCursor++] = Val;
                    ZeroWriteCursor();
                    buffer[writeCursor++] = Val;
                    ZeroWriteCursor();
                }
                else
                */if (Latency > latencyLimit)
                {
                    drops++;
                    // drop sample
                }
                else
                {
                    // Normal 
                    buffer[writeCursor++] = Val;
                    ZeroWriteCursor();
                }
                //CheckOverread();
            }
            public void ReadSilentFrame(DataPointer Buffer)
            {
                Buffer.CopyFrom<T>(silentFrame, 0, frameSize);
            }
            public void ReadFrame(DataPointer Buffer)
            {
                if (!FrameReady)
                    throw new Exception();

                frameReads++;

                Buffer.CopyFrom<T>(buffer, readCursor, frameSize);

                readCursor += frameSize;

                if (readCursor >= bufferSize)
                {
                    readCursor = 0;
                    if (writeWrap)
                        writeWrap = false;
                    else
                        WrapAround();
                }
                //CheckOverread();
            }
            public void Reset()
            {
                readCursor = 0;
                writeCursor = latencyLimit * 2;
                Array.Clear(buffer, 0, writeCursor);
                writeWrap = false;
                resets++;
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
                        WrapAround();
                    else
                        writeWrap = true;
                }
            }
            //private void CheckOverread()
            //{
            //    if ((writeWrap && writeCursor > readCursor) || (!writeWrap && writeCursor < readCursor + frameSize))
            //    {
            //        overreads++;
            //        //Log.LogMessage(string.Format("Sound Buffer Error: Read: {0} Write: {1} Frame Size: {2} Wrap: {3}", readCursor, writeCursor, frameSize, writeWrap ? "Yes" : "No"));
            //        Reset();
            //    }
            //}
            private void WrapAround()
            {
                wrapArounds++;
                Reset();
            }            
        }
    }
}

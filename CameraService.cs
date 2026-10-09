using System;
using OpenCvSharp;

namespace CustomCursor
{
    public class CameraService : IDisposable
    {
        private VideoCapture? capture;

        public bool IsOpened =>
            capture != null && capture.IsOpened();

        public bool Start()
        {
            capture = new VideoCapture(0);

            if (!capture.IsOpened())
            {
                capture.Dispose();
                capture = null;

                return false;
            }

            return true;
        }

        public Mat? CaptureFrame()
        {
            if (capture == null || !capture.IsOpened())
                return null;

            var frame = new Mat();

            if (!capture.Read(frame) || frame.Empty())
            {
                frame.Dispose();
                return null;
            }

            return frame;
        }

        public void Stop()
        {
            if (capture != null)
            {
                capture.Release();
                capture.Dispose();
                capture = null;
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
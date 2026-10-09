using System;
using OpenCvSharp;

namespace CustomCursor
{
    public class FaceDetector : IDisposable
    {
        private readonly CascadeClassifier faceCascade;

        public FaceDetector()
        {
            string cascadePath = FindCascadeFile();

            faceCascade = new CascadeClassifier(cascadePath);

            if (faceCascade.Empty())
            {
                throw new Exception(
                    "顔検出用のカスケードファイルを読み込めませんでした。");
            }
        }

        // ========================================
        // 顔を検出して位置を返す
        // ========================================

        public Rect? DetectFace(Mat frame)
        {
            if (frame == null || frame.Empty())
                return null;

            using var gray = new Mat();

            // カラー → グレースケール
            Cv2.CvtColor(
                frame,
                gray,
                ColorConversionCodes.BGR2GRAY);

            // 明るさを均一化
            Cv2.EqualizeHist(gray, gray);

            // 顔を検出
            Rect[] faces =
                faceCascade.DetectMultiScale(
                    gray,
                    1.1,
                    5);

            // 顔がなければ null
            if (faces.Length == 0)
                return null;

            // 今は最初に見つかった顔を使用
            return faces[0];
        }

        // ========================================
        // 顔があるかだけ確認する
        // ========================================

        public bool HasFace(Mat frame)
        {
            return DetectFace(frame).HasValue;
        }

        // ========================================
        // 顔検出XMLを探す
        // ========================================

        private static string FindCascadeFile()
        {
            string baseDirectory =
                AppContext.BaseDirectory;

            string cascadePath =
                System.IO.Path.Combine(
                    baseDirectory,
                    "haarcascade_frontalface_default.xml");

            if (!System.IO.File.Exists(cascadePath))
            {
                throw new Exception(
                    "顔検出用XMLファイルが見つかりません。\n" +
                    cascadePath);
            }

            return cascadePath;
        }

        // ========================================
        // 終了処理
        // ========================================

        public void Dispose()
        {
            faceCascade.Dispose();
        }
    }
}
using System;
using OpenCvSharp;
using Point = OpenCvSharp.Point;
using Size = OpenCvSharp.Size;

namespace CustomCursor
{
    public class GazeResult
    {
        // -1 = 左、+1 = 右
        public double X { get; set; }

        // -1 = 上、+1 = 下
        public double Y { get; set; }

        public bool IsValid { get; set; }

        // 顔
        public Rect Face { get; set; }

        // 左目
        public Rect LeftEye { get; set; }

        // 右目
        public Rect RightEye { get; set; }

        // 左右どちらの目が検出できたか
        public bool LeftEyeDetected { get; set; }
        public bool RightEyeDetected { get; set; }

        public GazeResult()
        {
            Face = new Rect();
            LeftEye = new Rect();
            RightEye = new Rect();

            IsValid = false;
            LeftEyeDetected = false;
            RightEyeDetected = false;
        }
    }

    public class GazeDetector
    {
        private readonly FaceDetector faceDetector;

        // ==================================================
        // 目領域
        // ==================================================

        private Rect? trackedLeftEye;
        private Rect? trackedRightEye;

        private int lostFrames = 0;

        // 何フレーム顔・目を見失ったら再初期化するか
        private const int MaxLostFrames = 10;

        // ==================================================
        // 視線平滑化
        // ==================================================

        private double smoothX = 0;
        private double smoothY = 0;

        private const double SmoothSpeed = 0.20;

        // ==================================================
        // 左右反転
        // ==================================================

        // カメラ映像が鏡像になっている場合は true
        private const bool MirrorX = true;

        public GazeDetector(FaceDetector faceDetector)
        {
            this.faceDetector = faceDetector;
        }

        // ==================================================
        // 視線検出
        // ==================================================

        public GazeResult Detect(Mat frame)
        {
            GazeResult result = new GazeResult();

            if (frame == null || frame.Empty())
                return result;

            // ==================================================
            // まだ目領域を持っていない場合
            // または追跡を失った場合
            // ==================================================

            if (!trackedLeftEye.HasValue &&
                !trackedRightEye.HasValue)
            {
                if (!TryInitializeEyes(frame, result))
                    return result;
            }

            // ==================================================
            // 現在の目領域を少し追跡する
            // ==================================================

            Rect? leftEye = trackedLeftEye;
            Rect? rightEye = trackedRightEye;

            // ==================================================
            // 瞳孔検出
            // ==================================================

            Point? leftPupil = null;
            Point? rightPupil = null;

            if (leftEye.HasValue)
            {
                leftPupil = DetectPupil(
                    frame,
                    leftEye.Value
                );
            }

            if (rightEye.HasValue)
            {
                rightPupil = DetectPupil(
                    frame,
                    rightEye.Value
                );
            }

            // ==================================================
            // 少なくとも片目が検出できた
            // ==================================================

            if (leftPupil.HasValue ||
                rightPupil.HasValue)
            {
                lostFrames = 0;

                double gazeX = 0;
                double gazeY = 0;

                int count = 0;

                // --------------------------------------------------
                // 左目
                // --------------------------------------------------

                if (leftPupil.HasValue &&
                    leftEye.HasValue)
                {
                    Rect eye = leftEye.Value;

                    double centerX =
                        eye.X + eye.Width / 2.0;

                    double centerY =
                        eye.Y + eye.Height / 2.0;

                    double x =
                        (leftPupil.Value.X - centerX)
                        / (eye.Width * 0.30);

                    double y =
                        (leftPupil.Value.Y - centerY)
                        / (eye.Height * 0.30);

                    gazeX += x;
                    gazeY += y;

                    count++;

                    result.LeftEyeDetected = true;
                }

                // --------------------------------------------------
                // 右目
                // --------------------------------------------------

                if (rightPupil.HasValue &&
                    rightEye.HasValue)
                {
                    Rect eye = rightEye.Value;

                    double centerX =
                        eye.X + eye.Width / 2.0;

                    double centerY =
                        eye.Y + eye.Height / 2.0;

                    double x =
                        (rightPupil.Value.X - centerX)
                        / (eye.Width * 0.30);

                    double y =
                        (rightPupil.Value.Y - centerY)
                        / (eye.Height * 0.30);

                    gazeX += x;
                    gazeY += y;

                    count++;

                    result.RightEyeDetected = true;
                }

                // --------------------------------------------------
                // 平均
                // --------------------------------------------------

                gazeX /= count;
                gazeY /= count;

                // ==================================================
                // 左右反転
                // ==================================================

                if (MirrorX)
                {
                    gazeX = -gazeX;
                }

                // ==================================================
                // -1 ～ +1
                // ==================================================

                gazeX =
                    Math.Max(
                        -1.0,
                        Math.Min(1.0, gazeX)
                    );

                gazeY =
                    Math.Max(
                        -1.0,
                        Math.Min(1.0, gazeY)
                    );

                // ==================================================
                // 平滑化
                // ==================================================

                smoothX +=
                    (gazeX - smoothX)
                    * SmoothSpeed;

                smoothY +=
                    (gazeY - smoothY)
                    * SmoothSpeed;

                result.X = smoothX;
                result.Y = smoothY;

                result.IsValid = true;

                result.LeftEye =
                    leftEye ?? new Rect();

                result.RightEye =
                    rightEye ?? new Rect();

                return result;
            }

            // ==================================================
            // 今フレームは瞳孔を見失った
            // ==================================================

            lostFrames++;

            // すぐに視線を無効にしない
            if (lostFrames <= MaxLostFrames)
            {
                result.X = smoothX;
                result.Y = smoothY;
                result.IsValid = false;

                result.LeftEye =
                    leftEye ?? new Rect();

                result.RightEye =
                    rightEye ?? new Rect();

                return result;
            }

            // ==================================================
            // 長時間見失った
            // → 目領域をリセット
            // ==================================================

            trackedLeftEye = null;
            trackedRightEye = null;

            lostFrames = 0;

            return result;
        }

        // ==================================================
        // 顔から目領域を初期化
        // ==================================================

        private bool TryInitializeEyes(
            Mat frame,
            GazeResult result)
        {
            Rect? face =
                faceDetector.DetectFace(frame);

            if (!face.HasValue)
                return false;

            Rect faceRect = face.Value;

            result.Face = faceRect;

            if (faceRect.Width <= 20 ||
                faceRect.Height <= 20)
            {
                return false;
            }

            // ==================================================
            // 顔の上側だけを見る
            // ==================================================

            int eyeY =
                faceRect.Y +
                (int)(faceRect.Height * 0.20);

            int eyeHeight =
                (int)(faceRect.Height * 0.28);

            int eyeWidth =
                (int)(faceRect.Width * 0.32);

            // カメラ画像上の左側
            Rect leftEye =
                new Rect(
                    faceRect.X +
                    (int)(faceRect.Width * 0.12),

                    eyeY,

                    eyeWidth,
                    eyeHeight
                );

            // カメラ画像上の右側
            Rect rightEye =
                new Rect(
                    faceRect.X +
                    (int)(faceRect.Width * 0.56),

                    eyeY,

                    eyeWidth,
                    eyeHeight
                );

            leftEye =
                ClampRect(
                    leftEye,
                    frame.Width,
                    frame.Height
                );

            rightEye =
                ClampRect(
                    rightEye,
                    frame.Width,
                    frame.Height
                );

            if (leftEye.Width <= 5 ||
                leftEye.Height <= 5 ||
                rightEye.Width <= 5 ||
                rightEye.Height <= 5)
            {
                return false;
            }

            trackedLeftEye = leftEye;
            trackedRightEye = rightEye;

            result.LeftEye = leftEye;
            result.RightEye = rightEye;

            return true;
        }

        // ==================================================
        // 瞳孔検出
        // ==================================================

        private Point? DetectPupil(
            Mat frame,
            Rect eyeRect)
        {
            using Mat eye =
                new Mat(
                    frame,
                    eyeRect
                );

            using Mat gray =
                new Mat();

            Cv2.CvtColor(
                eye,
                gray,
                ColorConversionCodes.BGR2GRAY
            );

            // ==================================================
            // 少しぼかす
            // ==================================================

            Cv2.GaussianBlur(
                gray,
                gray,
                new Size(5, 5),
                0
            );

            // ==================================================
            // 暗い部分を抽出
            // ==================================================

            using Mat threshold =
                new Mat();

            Cv2.Threshold(
                gray,
                threshold,
                60,
                255,
                ThresholdTypes.BinaryInv
            );

            // ==================================================
            // ノイズ除去
            // ==================================================

            using Mat kernel =
                Cv2.GetStructuringElement(
                    MorphShapes.Ellipse,
                    new Size(3, 3)
                );

            Cv2.MorphologyEx(
                threshold,
                threshold,
                MorphTypes.Open,
                kernel
            );

            // ==================================================
            // 輪郭
            // ==================================================

            Cv2.FindContours(
                threshold,
                out Point[][] contours,
                out _,
                RetrievalModes.External,
                ContourApproximationModes.ApproxSimple
            );

            if (contours.Length == 0)
                return null;

            double bestScore =
                double.MinValue;

            Point? bestCenter = null;

            double centerX =
                eyeRect.Width / 2.0;

            double centerY =
                eyeRect.Height / 2.0;

            foreach (Point[] contour in contours)
            {
                double area =
                    Cv2.ContourArea(contour);

                // 小さすぎるもの
                if (area < 3)
                    continue;

                // 大きすぎるもの
                if (area >
                    eyeRect.Width *
                    eyeRect.Height *
                    0.30)
                {
                    continue;
                }

                Moments moments =
                    Cv2.Moments(contour);

                if (Math.Abs(moments.M00) < 0.001)
                    continue;

                double px =
                    moments.M10 /
                    moments.M00;

                double py =
                    moments.M01 /
                    moments.M00;

                // 目の中心からの距離
                double dx =
                    px - centerX;

                double dy =
                    py - centerY;

                double distance =
                    Math.Sqrt(
                        dx * dx +
                        dy * dy
                    );

                // 中心に近いほど高評価
                double centerScore =
                    1.0 /
                    (1.0 + distance);

                // 適度な大きさを評価
                double sizeScore =
                    Math.Min(
                        1.0,
                        area /
                        (
                            eyeRect.Width *
                            eyeRect.Height *
                            0.05
                        )
                    );

                double score =
                    centerScore * 2.0 +
                    sizeScore;

                if (score > bestScore)
                {
                    bestScore = score;

                    bestCenter =
                        new Point(
                            (int)(
                                eyeRect.X + px
                            ),
                            (int)(
                                eyeRect.Y + py
                            )
                        );
                }
            }

            return bestCenter;
        }

        // ==================================================
        // Rectを画像内に収める
        // ==================================================

        private static Rect ClampRect(
            Rect rect,
            int width,
            int height)
        {
            int x =
                Math.Max(
                    0,
                    Math.Min(
                        rect.X,
                        width - 1
                    )
                );

            int y =
                Math.Max(
                    0,
                    Math.Min(
                        rect.Y,
                        height - 1
                    )
                );

            int w =
                Math.Min(
                    rect.Width,
                    width - x
                );

            int h =
                Math.Min(
                    rect.Height,
                    height - y
                );

            return new Rect(
                x,
                y,
                Math.Max(0, w),
                Math.Max(0, h)
            );
        }

        // ==================================================
        // リセット
        // ==================================================

        public void Reset()
        {
            trackedLeftEye = null;
            trackedRightEye = null;

            lostFrames = 0;

            smoothX = 0;
            smoothY = 0;
        }
    }
}
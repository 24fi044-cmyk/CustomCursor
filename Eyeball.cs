using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using Brush = System.Windows.Media.Brush;
using Point = System.Windows.Point;

namespace CustomCursor
{
    public class EyeBall
    {
        private readonly Polygon eyePolygon;
        private readonly Ellipse pupil;

        // ========================================
        // 目ん玉君の大きさ
        // ========================================

        private double size = 20;

        // ========================================
        // 瞳孔位置
        // ========================================

        private double pupilX = 0;
        private double pupilY = 0;

        // ========================================
        // まばたき
        // ========================================

        private double blinkRate = 1.0;

        // ========================================
        // コンストラクタ
        // ========================================

        public EyeBall(
            Polygon eyePolygon,
            Ellipse pupil)
        {
            this.eyePolygon = eyePolygon;
            this.pupil = pupil;

            Draw();
        }

        // ========================================
        // サイズ変更
        // ========================================

        public void SetSize(double size)
        {
            this.size = size;

            Draw();
        }

        // ========================================
        // 瞳孔位置
        // ========================================

        public void SetPupilPosition(
            double x,
            double y)
        {
            pupilX = x;
            pupilY = y;

            Draw();
        }

        // ========================================
        // マウス方向を見る
        // ========================================

        public void LookAt(
            double targetX,
            double targetY)
        {
            double centerX = 50;
            double centerY = 50;

            double dx =
                targetX - centerX;

            double dy =
                targetY - centerY;

            double distance =
                Math.Sqrt(
                    dx * dx +
                    dy * dy
                );

            if (distance < 0.001)
            {
                return;
            }

            // 正規化
            dx /= distance;
            dy /= distance;

            // ========================================
            // 瞳孔の移動量
            // ========================================

            double pupilMove =
                size * 0.18;

            pupilX =
                dx * pupilMove;

            pupilY =
                dy * pupilMove;

            Draw();
        }

        // ========================================
        // 色
        // ========================================

        public void SetFillColor(
            Brush color)
        {
            eyePolygon.Fill = color;
        }

        public void SetStrokeColor(
            Brush color)
        {
            eyePolygon.Stroke = color;
        }

        public void SetPupilColor(
            Brush color)
        {
            pupil.Fill = color;
        }

        // ========================================
        // まばたき
        // ========================================

        public void SetBlinkRate(
            double rate)
        {
            blinkRate =
                Math.Max(
                    0.0,
                    Math.Min(
                        1.0,
                        rate
                    )
                );

            Draw();
        }

        public void StartBlink()
        {
        }

        public void StopBlink()
        {
            blinkRate = 1.0;

            Draw();
        }

        // ========================================
        // 描画
        // ========================================

        private void Draw()
        {
            double centerX = 50;
            double centerY = 50;

            // ========================================
            // 六角形の大きさ
            // ========================================

            double halfWidth =
                size * 0.75;

            double halfHeight =
                size * 0.35;

            // ========================================
            // 六角形
            // ========================================

            eyePolygon.Points.Clear();

            eyePolygon.Points.Add(
                new Point(
                    centerX -
                    halfWidth * 0.55,
                    centerY -
                    halfHeight
                )
            );

            eyePolygon.Points.Add(
                new Point(
                    centerX +
                    halfWidth * 0.55,
                    centerY -
                    halfHeight
                )
            );

            eyePolygon.Points.Add(
                new Point(
                    centerX +
                    halfWidth,
                    centerY
                )
            );

            eyePolygon.Points.Add(
                new Point(
                    centerX +
                    halfWidth * 0.55,
                    centerY +
                    halfHeight
                )
            );

            eyePolygon.Points.Add(
                new Point(
                    centerX -
                    halfWidth * 0.55,
                    centerY +
                    halfHeight
                )
            );

            eyePolygon.Points.Add(
                new Point(
                    centerX -
                    halfWidth,
                    centerY
                )
            );

            // ========================================
            // 瞳孔
            // ========================================

            // 以前より小さくする
            double pupilWidth =
                size * 0.2;

            double pupilHeight =
                size * 0.6 *
                blinkRate;

            // ========================================
            // 瞳孔位置
            // ========================================

            Canvas.SetLeft(
                pupil,
                centerX +
                pupilX -
                pupilWidth / 2
            );

            Canvas.SetTop(
                pupil,
                centerY +
                pupilY -
                pupilHeight / 2
            );

            // ========================================
            // 瞳孔サイズ
            // ========================================

            pupil.Width =
                pupilWidth;

            pupil.Height =
                pupilHeight;
        }
    }
}
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace CustomCursor
{
    public partial class MainWindow : Window
    {
        // ==================================================
        // タイマー
        // ==================================================

        private readonly DispatcherTimer timer;

        // ==================================================
        // タスクトレイ
        // ==================================================

        private readonly Forms.NotifyIcon trayIcon;

        // ==================================================
        // 目ん玉君
        // ==================================================

        private readonly EyeBall eyeBall;

        // ==================================================
        // 目ん玉君の位置
        // ==================================================

        private double eyeX;
        private double eyeY;

        private double eyeTargetX;
        private double eyeTargetY;

        // マウスへの追従速度
        private const double EyeFollowSpeed = 0.12;

        // カーソルから右へ何ピクセル離すか
        private const double EyeOffsetX = 35;
        private const double EyeOffsetY = 25;

        // ==================================================
        // ホバリング
        // ==================================================

        private double hoverTime = 0;

        private const double HoverHeight = 10;
        private const double HoverSpeed = 0.04;

        // ==================================================
        // まばたき
        // ==================================================

        private double blinkTimer = 0;

        private double nextBlinkTime = 2.0;

        private double blinkProgress = 0;

        private bool isBlinking = false;

        private readonly Random random =
            new Random();

        // ==================================================
        // Windows API
        // ==================================================

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(
            out POINT lpPoint
        );

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        // ==================================================
        // コンストラクタ
        // ==================================================

        public MainWindow()
        {
            InitializeComponent();

            // ==================================================
            // EyeBall
            // ==================================================

            eyeBall =
                new EyeBall(
                    EyePolygon,
                    Pupil
                );

            // 過去の動作版と同じ
            eyeBall.SetSize(15);

            // ==================================================
            // 初期位置
            // ==================================================

            eyeX =
                (
                    SystemParameters.PrimaryScreenWidth
                    - Width
                ) / 2;

            eyeY =
                (
                    SystemParameters.PrimaryScreenHeight
                    - Height
                ) / 2;

            Left = eyeX;
            Top = eyeY;

            // ==================================================
            // タスクトレイ
            // ==================================================

            trayIcon =
                new Forms.NotifyIcon
                {
                    Icon = SystemIcons.Application,
                    Visible = true,
                    Text = "CustomCursor"
                };

            var menu =
                new Forms.ContextMenuStrip();

            var exitItem =
                new Forms.ToolStripMenuItem(
                    "終了"
                );

            exitItem.Click +=
                ExitApplication;

            menu.Items.Add(exitItem);

            trayIcon.ContextMenuStrip =
                menu;

            // ==================================================
            // タイマー
            // ==================================================

            timer =
                new DispatcherTimer
                {
                    Interval =
                        TimeSpan.FromMilliseconds(16)
                };

            timer.Tick +=
                UpdateCursor;

            timer.Start();
        }

        // ==================================================
        // メイン更新
        // ==================================================

        private void UpdateCursor(
            object? sender,
            EventArgs e)
        {
            // ==================================================
            // マウス位置取得
            // ==================================================

            if (!GetCursorPos(
                    out POINT point))
            {
                return;
            }

            // ==================================================
            // マウスの画面座標
            // ==================================================

            System.Windows.Point screenPoint =
                new System.Windows.Point(
                    point.X,
                    point.Y
                );

            // ==================================================
            // DPI変換
            // ==================================================

            PresentationSource source =
                PresentationSource.FromVisual(
                    this
                );

            if (source?.CompositionTarget != null)
            {
                screenPoint =
                    source
                        .CompositionTarget
                        .TransformFromDevice
                        .Transform(
                            screenPoint
                        );
            }

            // ==================================================
            // ホバリング
            // ==================================================

            hoverTime +=
                HoverSpeed;

            double hoverOffset =
                Math.Sin(
                    hoverTime
                )
                * HoverHeight;

            // ==================================================
            // 目ん玉君の目標位置
            // ==================================================

            eyeTargetX =
                screenPoint.X
                - Width / 2
                + EyeOffsetX;

            eyeTargetY =
                screenPoint.Y
                - Height / 2
                + EyeOffsetY
                + hoverOffset;

            // ==================================================
            // 滑らかに追従
            // ==================================================

            eyeX +=
                (
                    eyeTargetX
                    - eyeX
                )
                * EyeFollowSpeed;

            eyeY +=
                (
                    eyeTargetY
                    - eyeY
                )
                * EyeFollowSpeed;

            // ==================================================
            // ウィンドウ位置
            // ==================================================

            Left = eyeX;
            Top = eyeY;

            // ==================================================
            // 瞳孔をマウス方向へ向ける
            // ==================================================

            double eyeCenterX =
                eyeX
                + Width / 2;

            double eyeCenterY =
                eyeY
                + Height / 2;

            double targetX =
                screenPoint.X
                - eyeCenterX;

            double targetY =
                screenPoint.Y
                - eyeCenterY;

            eyeBall.LookAt(
                targetX,
                targetY
            );

            // ==================================================
            // まばたき
            // ==================================================

            UpdateBlink();
        }

        // ==================================================
        // まばたき
        // ==================================================

        private void UpdateBlink()
        {
            blinkTimer += 0.016;

            // ==================================================
            // 通常
            // ==================================================

            if (!isBlinking)
            {
                if (blinkTimer >= nextBlinkTime)
                {
                    isBlinking = true;

                    blinkProgress = 0;

                    blinkTimer = 0;
                }

                return;
            }

            // ==================================================
            // まばたき進行
            // ==================================================

            blinkProgress += 0.12;

            // ==================================================
            // 閉じる
            // ==================================================

            if (blinkProgress < 0.5)
            {
                double t =
                    blinkProgress / 0.5;

                double blinkRate =
                    1.0 - t;

                eyeBall.SetBlinkRate(
                    blinkRate
                );
            }

            // ==================================================
            // 開く
            // ==================================================

            else
            {
                double t =
                    (
                        blinkProgress
                        - 0.5
                    )
                    / 0.5;

                double blinkRate =
                    t;

                eyeBall.SetBlinkRate(
                    blinkRate
                );

                // ==================================================
                // 終了
                // ==================================================

                if (blinkProgress >= 1.0)
                {
                    isBlinking = false;

                    blinkTimer = 0;

                    nextBlinkTime =
                        2.0
                        + random.NextDouble()
                        * 4.0;

                    eyeBall.StopBlink();
                }
            }
        }

        // ==================================================
        // 終了
        // ==================================================

        private void ExitApplication(
            object? sender,
            EventArgs e)
        {
            timer.Stop();

            trayIcon.Visible = false;

            trayIcon.Dispose();

            System.Windows.Application.Current.Shutdown();
        }
    }
}
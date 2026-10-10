using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Point = System.Windows.Point;

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
        private Forms.ToolStripMenuItem? gazeFollowMenuItem;
        private Forms.ToolStripMenuItem? mouseFollowMenuItem;
        private Forms.ToolStripMenuItem? mouseWithGazeMenuItem;

        private GazeCalibration calibration = GazeCalibration.Load();
        private CameraService? cameraService;
        private FaceDetector? faceDetector;
        private GazeDetector? gazeDetector;
        private CalibrationWindow? calibrationWindow;
        private readonly List<CalibrationObservation> calibrationObservations = new();
        private Task? activeFrameTask;
        private bool isShuttingDown;
        private bool cameraFramesPaused;
        private bool calibrationCapturePaused;
        private bool calibrationCompletedSuccessfully;
        private TrackingMode trackingMode = TrackingMode.Mouse;
        private TrackingMode modeBeforeCalibration = TrackingMode.Mouse;
        private TrackingMode modeAfterCalibration = TrackingMode.Gaze;
        private int calibrationPointIndex;
        private int calibrationSamplesAtPoint;
        private double calibrationGazeXSum;
        private double calibrationGazeYSum;
        private DateTime calibrationPointStarted;
        private DateTime lastValidGazeTime;
        private GazeResult? latestGaze;

        private const int SamplesPerCalibrationPoint = 20;
        private static readonly TimeSpan CalibrationSettleTime = TimeSpan.FromMilliseconds(600);

        private enum TrackingMode
        {
            Gaze,
            Mouse,
            MouseWithGazeDirection
        }

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

            gazeFollowMenuItem = new Forms.ToolStripMenuItem("視線に追従させる");
            gazeFollowMenuItem.Click += SelectGazeTracking;
            menu.Items.Add(gazeFollowMenuItem);

            mouseFollowMenuItem = new Forms.ToolStripMenuItem("マウスに追従させる");
            mouseFollowMenuItem.Click += SelectMouseTracking;
            menu.Items.Add(mouseFollowMenuItem);

            mouseWithGazeMenuItem = new Forms.ToolStripMenuItem("マウス追従＋視線の方向を向かせる");
            mouseWithGazeMenuItem.Click += SelectMouseWithGazeDirection;
            menu.Items.Add(mouseWithGazeMenuItem);

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

            UpdateModeMenu();
        }

        // ==================================================
        // メイン更新
        // ==================================================

        private void UpdateCursor(object? sender, EventArgs e)
        {
            if (gazeDetector != null && activeFrameTask == null &&
                !cameraFramesPaused && trackingMode != TrackingMode.Mouse && !isShuttingDown)
            {
                Task frameTask = ProcessCameraFrameAsync();
                activeFrameTask = frameTask.IsCompleted ? null : frameTask;
            }

            if (calibrationWindow != null)
            {
                if (!calibrationCapturePaused &&
                    DateTime.UtcNow - calibrationPointStarted > TimeSpan.FromSeconds(20))
                {
                    calibrationWindow.SetStatus(
                        $"赤い点を見つめてください  ({calibrationPointIndex + 1} / 9)\n" +
                        "顔をカメラに向け、明るさと距離を調整してください。読み取りをやり直します。");
                    gazeDetector?.Reset();
                    calibrationSamplesAtPoint = 0;
                    calibrationGazeXSum = 0;
                    calibrationGazeYSum = 0;
                    calibrationPointStarted = DateTime.UtcNow;
                }

                UpdateBlink();
                return;
            }

            if (!GetCursorPos(out POINT point))
                return;

            Point screenPoint = new Point(point.X, point.Y);
            PresentationSource? source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                screenPoint = source.CompositionTarget.TransformFromDevice.Transform(screenPoint);
            }

            GazeResult? gazeResult = latestGaze;
            bool gazeIsFresh = gazeResult?.IsValid == true &&
                               DateTime.UtcNow - lastValidGazeTime < TimeSpan.FromMilliseconds(500);

            if (trackingMode == TrackingMode.Gaze && calibration.IsCalibrated && gazeIsFresh)
            {
                Point normalizedPoint = calibration.Map(gazeResult!.X, gazeResult.Y);
                screenPoint = new Point(
                    Math.Clamp(normalizedPoint.X, 0, 1) * SystemParameters.PrimaryScreenWidth,
                    Math.Clamp(normalizedPoint.Y, 0, 1) * SystemParameters.PrimaryScreenHeight);
            }

            hoverTime += HoverSpeed;
            double hoverOffset = Math.Sin(hoverTime) * HoverHeight;

            eyeTargetX = screenPoint.X - Width / 2 + EyeOffsetX;
            eyeTargetY = screenPoint.Y - Height / 2 + EyeOffsetY + hoverOffset;

            eyeX += (eyeTargetX - eyeX) * EyeFollowSpeed;
            eyeY += (eyeTargetY - eyeY) * EyeFollowSpeed;

            Left = eyeX;
            Top = eyeY;

            double targetX = screenPoint.X - (eyeX + Width / 2);
            double targetY = screenPoint.Y - (eyeY + Height / 2);

            if (gazeIsFresh &&
                (trackingMode == TrackingMode.Gaze ||
                 trackingMode == TrackingMode.MouseWithGazeDirection))
            {
                eyeBall.LookAt(gazeResult!.X, gazeResult.Y);
            }
            else
                eyeBall.LookAt(targetX, targetY);

            UpdateBlink();
        }

        private async Task ProcessCameraFrameAsync()
        {
            if (cameraService == null || gazeDetector == null)
                return;

            try
            {
                CameraService camera = cameraService;
                GazeDetector detector = gazeDetector;
                GazeResult? result = await Task.Run(() =>
                {
                    using var frame = camera.CaptureFrame();
                    return frame == null ? null : detector.Detect(frame);
                });

                if (result == null)
                    return;

                if (result.IsValid)
                {
                    latestGaze = result;
                    lastValidGazeTime = DateTime.UtcNow;
                    CollectCalibrationSample(result);
                }
            }
            catch (Exception exception)
            {
                if (calibrationCapturePaused)
                    return;

                calibrationWindow?.SetStatus(
                    $"カメラまたは検出処理でエラーが発生しました。\n{exception.Message}");
            }
            finally
            {
                activeFrameTask = null;
            }
        }

        private bool TryStartVision(out string error)
        {
            try
            {
                faceDetector ??= new FaceDetector();
                gazeDetector ??= new GazeDetector(faceDetector);
                cameraService ??= new CameraService();

                if (!cameraService.IsOpened && !cameraService.Start())
                {
                    error = "カメラを開けませんでした。接続と、他のアプリで使用中でないことを確認してください。";
                    StopVision();
                    return false;
                }

                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = $"カメラまたは顔検出器を初期化できませんでした。\n{exception.Message}";
                StopVision();
                return false;
            }
        }

        private void StartCalibration(TrackingMode targetMode)
        {
            if (calibrationWindow != null)
                return;

            modeBeforeCalibration = trackingMode;
            modeAfterCalibration = targetMode;
            calibrationCapturePaused = false;
            calibrationCompletedSuccessfully = false;

            if (!TryStartVision(out string error))
            {
                System.Windows.MessageBox.Show(error, "9点キャリブレーション",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                UpdateModeMenu();
                return;
            }

            trackingMode = targetMode;
            cameraFramesPaused = false;
            UpdateModeMenu();
            latestGaze = null;
            gazeDetector?.Reset();
            calibrationObservations.Clear();
            calibrationPointIndex = 0;

            calibrationWindow = new CalibrationWindow();
            calibrationWindow.CancelRequested += CancelCalibration;
            calibrationWindow.Closed += CalibrationWindow_Closed;
            calibrationWindow.Show();
            Hide();
            ShowCalibrationPoint();
        }

        private void ShowCalibrationPoint()
        {
            if (calibrationWindow == null)
                return;

            if (calibrationPointIndex >= 9)
            {
                FinishCalibration();
                return;
            }

            double[] positions = { 0.15, 0.50, 0.85 };
            calibrationSamplesAtPoint = 0;
            calibrationGazeXSum = 0;
            calibrationGazeYSum = 0;
            calibrationPointStarted = DateTime.UtcNow;

            calibrationWindow.SetTarget(
                calibrationPointIndex + 1,
                positions[calibrationPointIndex % 3],
                positions[calibrationPointIndex / 3]);
        }

        private void CollectCalibrationSample(GazeResult result)
        {
            if (calibrationWindow == null || calibrationCapturePaused ||
                calibrationCompletedSuccessfully || calibrationPointIndex >= 9 ||
                DateTime.UtcNow - calibrationPointStarted < CalibrationSettleTime)
            {
                return;
            }

            calibrationGazeXSum += result.X;
            calibrationGazeYSum += result.Y;
            calibrationSamplesAtPoint++;
            calibrationWindow.SetStatus(
                $"赤い点を見つめてください  ({calibrationPointIndex + 1} / 9)\n" +
                $"視線を読み取り中… {calibrationSamplesAtPoint} / {SamplesPerCalibrationPoint}");

            if (calibrationSamplesAtPoint < SamplesPerCalibrationPoint)
                return;

            double[] positions = { 0.15, 0.50, 0.85 };
            calibrationObservations.Add(new CalibrationObservation(
                calibrationGazeXSum / calibrationSamplesAtPoint,
                calibrationGazeYSum / calibrationSamplesAtPoint,
                positions[calibrationPointIndex % 3],
                positions[calibrationPointIndex / 3]));

            calibrationPointIndex++;
            ShowCalibrationPoint();
        }

        private void FinishCalibration()
        {
            if (calibrationWindow == null)
                return;

            calibrationCapturePaused = true;

            if (!calibration.TryFit(calibrationObservations, out string error))
            {
                calibrationWindow.SetStatus(
                    $"キャリブレーションを完了できませんでした。\n{error}\n最初からやり直します。");
                calibrationObservations.Clear();
                calibrationPointIndex = 0;
                _ = RestartCalibrationAfterDelayAsync();
                return;
            }

            try
            {
                calibration.Save();
            }
            catch (Exception exception) when (
                exception is IOException || exception is UnauthorizedAccessException)
            {
                trayIcon.ShowBalloonTip(4000, "目ん玉君",
                    "校正結果は今回の起動中のみ使用します。設定ファイルに保存できませんでした。",
                    Forms.ToolTipIcon.Warning);
            }

            calibrationWindow.SetStatus("キャリブレーション完了！\n目ん玉君が視線を追いかけます");
            calibrationCompletedSuccessfully = true;
            trackingMode = modeAfterCalibration;
            trayIcon.Text = trackingMode == TrackingMode.Gaze
                ? "目ん玉君 - 視線追従"
                : "目ん玉君 - マウス＋視線方向";
            UpdateModeMenu();
            _ = CloseCalibrationWindowAfterDelayAsync();
        }

        private async Task RestartCalibrationAfterDelayAsync()
        {
            await Task.Delay(1800);
            if (calibrationWindow == null || isShuttingDown)
                return;

            gazeDetector?.Reset();
            calibrationCapturePaused = false;
            ShowCalibrationPoint();
        }

        private async Task CloseCalibrationWindowAfterDelayAsync()
        {
            await Task.Delay(1400);
            calibrationWindow?.Close();
        }

        private void CancelCalibration(object? sender, EventArgs e)
        {
            calibrationWindow?.Close();
        }

        private async void CalibrationWindow_Closed(object? sender, EventArgs e)
        {
            if (calibrationWindow != null)
            {
                calibrationWindow.CancelRequested -= CancelCalibration;
                calibrationWindow.Closed -= CalibrationWindow_Closed;
                calibrationWindow = null;
            }

            if (!isShuttingDown)
                Show();

            if (!calibrationCompletedSuccessfully)
                trackingMode = modeBeforeCalibration;

            calibrationCompletedSuccessfully = false;
            UpdateModeMenu();

            bool needsVision = trackingMode == TrackingMode.MouseWithGazeDirection ||
                               (trackingMode == TrackingMode.Gaze && calibration.IsCalibrated);

            if (!isShuttingDown && !needsVision)
            {
                cameraFramesPaused = true;
                if (activeFrameTask != null)
                    await activeFrameTask;

                StopVision();
            }
        }

        private void SelectGazeTracking(object? sender, EventArgs e)
        {
            StartCalibration(TrackingMode.Gaze);
        }

        private void SelectMouseWithGazeDirection(object? sender, EventArgs e)
        {
            StartCalibration(TrackingMode.MouseWithGazeDirection);
        }

        private async void SelectMouseTracking(object? sender, EventArgs e)
        {
            if (calibrationWindow != null)
                return;

            trackingMode = TrackingMode.Mouse;
            cameraFramesPaused = true;
            latestGaze = null;
            UpdateModeMenu();
            trayIcon.Text = "目ん玉君 - マウス追従";

            if (activeFrameTask != null)
                await activeFrameTask;

            StopVision();
        }

        private void UpdateModeMenu()
        {
            if (gazeFollowMenuItem != null)
                gazeFollowMenuItem.Checked = trackingMode == TrackingMode.Gaze;

            if (mouseFollowMenuItem != null)
                mouseFollowMenuItem.Checked = trackingMode == TrackingMode.Mouse;

            if (mouseWithGazeMenuItem != null)
                mouseWithGazeMenuItem.Checked = trackingMode == TrackingMode.MouseWithGazeDirection;
        }

        private void StopVision()
        {
            cameraService?.Stop();
            cameraService?.Dispose();
            cameraService = null;

            faceDetector?.Dispose();
            faceDetector = null;
            gazeDetector = null;
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

        private async void ExitApplication(
            object? sender,
            EventArgs e)
        {
            isShuttingDown = true;
            timer.Stop();

            calibrationWindow?.Close();

            if (activeFrameTask != null)
                await activeFrameTask;

            StopVision();

            trayIcon.Visible = false;

            trayIcon.Dispose();

            System.Windows.Application.Current.Shutdown();
        }
    }
}

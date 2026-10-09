using System.IO;
using System.Text.Json;
using System.Windows;
using Point = System.Windows.Point;

namespace CustomCursor
{
    public readonly record struct CalibrationObservation(
        double GazeX,
        double GazeY,
        double ScreenX,
        double ScreenY);

    public sealed class GazeCalibration
    {
        private const int SampleCount = 9;
        private double[]? screenXCoefficients;
        private double[]? screenYCoefficients;

        private static string SettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CustomCursor",
            "gaze-calibration.json");

        public bool IsCalibrated =>
            screenXCoefficients is { Length: 3 } &&
            screenYCoefficients is { Length: 3 };

        public Point Map(double gazeX, double gazeY)
        {
            if (!IsCalibrated)
                throw new InvalidOperationException("視線キャリブレーションが完了していません。");

            return new Point(
                Evaluate(screenXCoefficients!, gazeX, gazeY),
                Evaluate(screenYCoefficients!, gazeX, gazeY));
        }

        public bool TryFit(
            IReadOnlyList<CalibrationObservation> observations,
            out string error)
        {
            if (observations.Count != SampleCount)
            {
                error = "9点すべてのデータが必要です。";
                return false;
            }

            double gazeRangeX = observations.Max(sample => sample.GazeX) -
                                observations.Min(sample => sample.GazeX);
            double gazeRangeY = observations.Max(sample => sample.GazeY) -
                                observations.Min(sample => sample.GazeY);

            if (gazeRangeX < 0.08 || gazeRangeY < 0.08)
            {
                error = "視線の移動を十分に検出できませんでした。明るさとカメラ位置を確認して、もう一度試してください。";
                return false;
            }

            double[,] normalMatrix = new double[3, 3];
            double[] rightX = new double[3];
            double[] rightY = new double[3];

            foreach (CalibrationObservation sample in observations)
            {
                double[] row = { sample.GazeX, sample.GazeY, 1.0 };

                for (int r = 0; r < 3; r++)
                {
                    rightX[r] += row[r] * sample.ScreenX;
                    rightY[r] += row[r] * sample.ScreenY;

                    for (int c = 0; c < 3; c++)
                        normalMatrix[r, c] += row[r] * row[c];
                }
            }

            if (!TrySolve(normalMatrix, rightX, out double[] coefficientsX) ||
                !TrySolve(normalMatrix, rightY, out double[] coefficientsY))
            {
                error = "視線データが安定しませんでした。顔を正面に向けて、もう一度試してください。";
                return false;
            }

            screenXCoefficients = coefficientsX;
            screenYCoefficients = coefficientsY;
            error = string.Empty;
            return true;
        }

        public void Save()
        {
            if (!IsCalibrated)
                return;

            string? directory = Path.GetDirectoryName(SettingsPath);
            if (directory == null)
                return;

            Directory.CreateDirectory(directory);
            string json = JsonSerializer.Serialize(new[]
            {
                screenXCoefficients!,
                screenYCoefficients!
            });
            File.WriteAllText(SettingsPath, json);
        }

        public static GazeCalibration Load()
        {
            var calibration = new GazeCalibration();

            try
            {
                if (!File.Exists(SettingsPath))
                    return calibration;

                double[][]? data = JsonSerializer.Deserialize<double[][]>(
                    File.ReadAllText(SettingsPath));

                if (data is { Length: 2 } && data[0] is { Length: 3 } &&
                    data[1] is { Length: 3 } && data[0].All(double.IsFinite) &&
                    data[1].All(double.IsFinite))
                {
                    calibration.screenXCoefficients = data[0];
                    calibration.screenYCoefficients = data[1];
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (JsonException)
            {
            }

            return calibration;
        }

        private static double Evaluate(double[] coefficients, double x, double y) =>
            coefficients[0] * x + coefficients[1] * y + coefficients[2];

        private static bool TrySolve(double[,] matrix, double[] values, out double[] solution)
        {
            int size = values.Length;
            double[,] augmented = new double[size, size + 1];

            for (int row = 0; row < size; row++)
            {
                for (int column = 0; column < size; column++)
                    augmented[row, column] = matrix[row, column];

                augmented[row, size] = values[row];
            }

            for (int pivot = 0; pivot < size; pivot++)
            {
                int bestRow = pivot;
                for (int row = pivot + 1; row < size; row++)
                {
                    if (Math.Abs(augmented[row, pivot]) > Math.Abs(augmented[bestRow, pivot]))
                        bestRow = row;
                }

                if (Math.Abs(augmented[bestRow, pivot]) < 1e-10)
                {
                    solution = Array.Empty<double>();
                    return false;
                }

                for (int column = pivot; column <= size; column++)
                    (augmented[pivot, column], augmented[bestRow, column]) =
                        (augmented[bestRow, column], augmented[pivot, column]);

                double divisor = augmented[pivot, pivot];
                for (int column = pivot; column <= size; column++)
                    augmented[pivot, column] /= divisor;

                for (int row = 0; row < size; row++)
                {
                    if (row == pivot)
                        continue;

                    double factor = augmented[row, pivot];
                    for (int column = pivot; column <= size; column++)
                        augmented[row, column] -= factor * augmented[pivot, column];
                }
            }

            solution = new double[size];
            for (int row = 0; row < size; row++)
                solution[row] = augmented[row, size];

            return solution.All(double.IsFinite);
        }
    }
}

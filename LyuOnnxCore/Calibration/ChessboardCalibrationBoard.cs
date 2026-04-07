using OpenCvSharp;

namespace LyuOnnxCore.Calibration;

/// <summary>
/// Defines a standard chessboard calibration target.
/// </summary>
public sealed class ChessboardCalibrationBoard
{
    public ChessboardCalibrationBoard(int innerCornerColumns, int innerCornerRows, double squareSize)
    {
        if (innerCornerColumns <= 1)
            throw new ArgumentOutOfRangeException(nameof(innerCornerColumns), "Inner corner columns must be greater than 1.");

        if (innerCornerRows <= 1)
            throw new ArgumentOutOfRangeException(nameof(innerCornerRows), "Inner corner rows must be greater than 1.");

        if (squareSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(squareSize), "Square size must be greater than 0.");

        InnerCornerColumns = innerCornerColumns;
        InnerCornerRows = innerCornerRows;
        SquareSize = squareSize;
    }

    public int InnerCornerColumns { get; }

    public int InnerCornerRows { get; }

    /// <summary>
    /// Physical side length of one square. Unit is user-defined, usually mm.
    /// </summary>
    public double SquareSize { get; }

    public int CornerCount => InnerCornerColumns * InnerCornerRows;

    public Size PatternSize => new(InnerCornerColumns, InnerCornerRows);

    public Point3f[] CreateObjectPoints()
    {
        var points = new Point3f[CornerCount];
        int index = 0;

        for (int row = 0; row < InnerCornerRows; row++)
        {
            for (int column = 0; column < InnerCornerColumns; column++)
            {
                points[index++] = new Point3f(
                    (float)(column * SquareSize),
                    (float)(row * SquareSize),
                    0f);
            }
        }

        return points;
    }

    public Point2d[] CreatePlanarPoints()
    {
        var objectPoints = CreateObjectPoints();
        return [.. objectPoints.Select(point => new Point2d(point.X, point.Y))];
    }
}

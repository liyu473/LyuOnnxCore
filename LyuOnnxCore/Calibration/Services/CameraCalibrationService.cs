using LyuOnnxCore.Calibration.Interface;
using LyuOnnxCore.Calibration.Models;
using LyuOnnxCore.Calibration.Serialization;
using OpenCvSharp;
using System.Text.Json;

namespace LyuOnnxCore.Calibration.Services;

internal sealed class CameraCalibrationService : ICameraCalibration
{
    public CameraCalibrateResult Calibrate(
        IEnumerable<string> imagePaths,
        Size patternSize,
        float squareSizeMm
    ) => Calibrate(imagePaths, patternSize, squareSizeMm, CalibrationPatternType.Chessboard);

    public CameraCalibrateResult Calibrate(
        IEnumerable<string> imagePaths,
        Size patternSize,
        float pointSpacingMm,
        CalibrationPatternType patternType
    )
    {
        ArgumentNullException.ThrowIfNull(imagePaths);

        var mats = new List<Mat>();
        try
        {
            foreach (var imagePath in imagePaths.Where(static path => !string.IsNullOrWhiteSpace(path)))
            {
                if (!File.Exists(imagePath))
                {
                    throw new FileNotFoundException($"Calibration image was not found: {imagePath}", imagePath);
                }

                mats.Add(Cv2.ImRead(imagePath, ImreadModes.Color));
            }

            return Calibrate(mats, patternSize, pointSpacingMm, patternType);
        }
        finally
        {
            foreach (var mat in mats)
            {
                mat.Dispose();
            }
        }
    }

    public CameraCalibrateResult Calibrate(
        IEnumerable<Mat> images,
        Size patternSize,
        float squareSizeMm
    ) => Calibrate(images, patternSize, squareSizeMm, CalibrationPatternType.Chessboard);

    public CameraCalibrateResult Calibrate(
        IEnumerable<Mat> images,
        Size patternSize,
        float pointSpacingMm,
        CalibrationPatternType patternType
    )
    {
        ArgumentNullException.ThrowIfNull(images);

        ValidatePatternSize(patternSize);
        ValidatePatternType(patternType);
        if (pointSpacingMm <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pointSpacingMm), "Pattern point spacing must be greater than 0.");
        }

        var imageList = images.Where(static image => image is not null && !image.Empty()).ToList();
        if (imageList.Count == 0)
        {
            throw new ArgumentException("At least one valid calibration image is required.", nameof(images));
        }

        var dominantResolutionGroup = imageList
            .GroupBy(static image => (image.Width, image.Height))
            .OrderByDescending(static group => group.Count())
            .ThenByDescending(static group => group.Key.Width * group.Key.Height)
            .FirstOrDefault() ?? throw new InvalidOperationException("Unable to determine an effective image resolution from the input images.");
        var imageSize = new Size(dominantResolutionGroup.Key.Width, dominantResolutionGroup.Key.Height);
        var objectTemplate = CreateObjectPoints(patternSize, pointSpacingMm, patternType);
        var objectPoints = new List<IEnumerable<Point3f>>();
        var imagePoints = new List<IEnumerable<Point2f>>();
        int skippedMismatchedResolutionCount = 0;

        foreach (var image in imageList)
        {
            if (image.Size() != imageSize)
            {
                skippedMismatchedResolutionCount++;
                continue;
            }

            if (!TryFindPatternPoints(image, patternSize, patternType, out var corners))
            {
                continue;
            }

            objectPoints.Add(objectTemplate);
            imagePoints.Add(corners);
        }

        if (imagePoints.Count < 3)
        {
            throw new InvalidOperationException(
                $"Camera calibration needs at least 3 images with a full {patternType} detection, but only {imagePoints.Count} were valid."
            );
        }

        using var cameraMatrixMat = CreateMatrixMat(CreateIdentityMatrix());
        using var distortionCoefficientsMat = new Mat();
        var objectPointMats = objectPoints.Select(CreateObjectPointMat).ToList();
        var imagePointMats = imagePoints.Select(CreateImagePointMat).ToList();

        try
        {
            double reprojectionError = Cv2.CalibrateCamera(
                objectPointMats,
                imagePointMats,
                imageSize,
                cameraMatrixMat,
                distortionCoefficientsMat,
                out Mat[] rotationVectorMats,
                out Mat[] translationVectorMats,
                CalibrationFlags.None,
                new TermCriteria(CriteriaTypes.Eps | CriteriaTypes.MaxIter, 30, 1e-6)
            );

            try
            {
                var cameraMatrix = ToDoubleMatrix(cameraMatrixMat);
                var distortionCoefficients = ToDoubleVector(distortionCoefficientsMat);
                var rotationVectors = rotationVectorMats.Select(ReadVec3d).ToArray();
                var translationVectors = translationVectorMats.Select(ReadVec3d).ToArray();

                return new CameraCalibrateResult
                {
                    ImageSize = imageSize,
                    PatternType = patternType,
                    CameraMatrix = cameraMatrix,
                    DistortionCoefficients = distortionCoefficients,
                    RotationVectors = rotationVectors,
                    TranslationVectors = translationVectors,
                    ReprojectionError = reprojectionError,
                    PerViewErrors = CalculatePerViewErrors(
                        objectPoints,
                        imagePoints,
                        cameraMatrix,
                        distortionCoefficients,
                        rotationVectors,
                        translationVectors
                    ),
                    SuccessfulImageCount = imagePoints.Count,
                    InputImageCount = imageList.Count,
                    SkippedMismatchedResolutionCount = skippedMismatchedResolutionCount
                };
            }
            finally
            {
                foreach (var mat in rotationVectorMats)
                {
                    mat.Dispose();
                }

                foreach (var mat in translationVectorMats)
                {
                    mat.Dispose();
                }
            }
        }
        finally
        {
            foreach (var mat in objectPointMats)
            {
                mat.Dispose();
            }

            foreach (var mat in imagePointMats)
            {
                mat.Dispose();
            }
        }
    }

    public string SerializeResult(
        CameraCalibrateResult result,
        bool writeIndented = true
    )
    {
        ArgumentNullException.ThrowIfNull(result);

        return JsonSerializer.Serialize(
            result,
            CalibrationJsonSerializer.CreateOptions(writeIndented)
        );
    }

    public CameraCalibrateResult DeserializeResult(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Calibration result JSON cannot be null or empty.", nameof(json));
        }

        return JsonSerializer.Deserialize<CameraCalibrateResult>(
            json,
            CalibrationJsonSerializer.CreateOptions(writeIndented: false)
        ) ?? throw new InvalidOperationException("Failed to deserialize camera calibration result.");
    }

    private static void ValidatePatternType(CalibrationPatternType patternType)
    {
        if (!Enum.IsDefined(patternType))
        {
            throw new ArgumentOutOfRangeException(nameof(patternType), patternType, "Unsupported calibration pattern type.");
        }
    }

    private static void ValidatePatternSize(Size patternSize)
    {
        if (patternSize.Width < 2 || patternSize.Height < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(patternSize), "Pattern width and height must both be at least 2.");
        }
    }

    private static Point3f[] CreateObjectPoints(
        Size patternSize,
        float pointSpacingMm,
        CalibrationPatternType patternType
    )
    {
        var points = new Point3f[patternSize.Width * patternSize.Height];
        int index = 0;

        for (int row = 0; row < patternSize.Height; row++)
        {
            for (int column = 0; column < patternSize.Width; column++)
            {
                points[index++] = patternType == CalibrationPatternType.AsymmetricCirclesGrid
                    ? new Point3f((2 * column + row % 2) * pointSpacingMm, row * pointSpacingMm, 0)
                    : new Point3f(column * pointSpacingMm, row * pointSpacingMm, 0);
            }
        }

        return points;
    }

    private static bool TryFindPatternPoints(
        Mat image,
        Size patternSize,
        CalibrationPatternType patternType,
        out Point2f[] points
    )
    {
        return patternType switch
        {
            CalibrationPatternType.Chessboard => TryFindChessboardCorners(image, patternSize, out points),
            CalibrationPatternType.SymmetricCirclesGrid => TryFindCircleGridCenters(image, patternSize, patternType, out points),
            CalibrationPatternType.AsymmetricCirclesGrid => TryFindCircleGridCenters(image, patternSize, patternType, out points),
            _ => throw new ArgumentOutOfRangeException(nameof(patternType), patternType, "Unsupported calibration pattern type.")
        };
    }

    private static bool TryFindChessboardCorners(Mat image, Size patternSize, out Point2f[] corners)
    {
        using var gray = PrepareGrayImage(image);
        const ChessboardFlags legacyFlags =
            ChessboardFlags.AdaptiveThresh |
            ChessboardFlags.NormalizeImage |
            ChessboardFlags.FastCheck;

        if (Cv2.FindChessboardCornersSB(
            gray,
            patternSize,
            out corners,
            ChessboardFlags.NormalizeImage | ChessboardFlags.Exhaustive | ChessboardFlags.Accuracy
        ))
        {
            return corners.Length == patternSize.Width * patternSize.Height;
        }

        if (!Cv2.FindChessboardCorners(gray, patternSize, out corners, legacyFlags))
        {
            corners = [];
            return false;
        }

        Cv2.CornerSubPix(
            gray,
            corners,
            new Size(11, 11),
            new Size(-1, -1),
            new TermCriteria(CriteriaTypes.Eps | CriteriaTypes.MaxIter, 30, 0.1)
        );

        return corners.Length == patternSize.Width * patternSize.Height;
    }

    private static bool TryFindCircleGridCenters(
        Mat image,
        Size patternSize,
        CalibrationPatternType patternType,
        out Point2f[] centers
    )
    {
        using var gray = PrepareGrayImage(image);
        using var blobDetector = SimpleBlobDetector.Create(new SimpleBlobDetector.Params());
        var flags = patternType switch
        {
            CalibrationPatternType.SymmetricCirclesGrid => FindCirclesGridFlags.SymmetricGrid,
            CalibrationPatternType.AsymmetricCirclesGrid => FindCirclesGridFlags.AsymmetricGrid | FindCirclesGridFlags.Clustering,
            _ => throw new ArgumentOutOfRangeException(nameof(patternType), patternType, "Unsupported circle grid pattern type.")
        };

        if (TryFindCirclesGrid(gray, patternSize, flags, blobDetector, out centers))
        {
            return centers.Length == patternSize.Width * patternSize.Height;
        }

        // 圆形孔透光时可能是亮点，反色后再尝试一次。
        using var inverted = new Mat();
        Cv2.BitwiseNot(gray, inverted);
        if (!TryFindCirclesGrid(inverted, patternSize, flags, blobDetector, out centers))
        {
            centers = [];
            return false;
        }

        return centers.Length == patternSize.Width * patternSize.Height;
    }

    private static bool TryFindCirclesGrid(
        Mat gray,
        Size patternSize,
        FindCirclesGridFlags flags,
        Feature2D blobDetector,
        out Point2f[] centers
    )
    {
        return Cv2.FindCirclesGrid(gray, patternSize, out centers, flags, blobDetector);
    }

    private static Mat PrepareGrayImage(Mat image)
    {
        if (image.Channels() == 1)
        {
            return image.Clone();
        }

        var gray = new Mat();
        Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
        return gray;
    }

    private static Mat CreateObjectPointMat(IEnumerable<Point3f> points)
    {
        var pointArray = points.ToArray();
        var mat = new Mat(pointArray.Length, 1, MatType.CV_32FC3);
        for (int i = 0; i < pointArray.Length; i++)
        {
            mat.Set(i, 0, pointArray[i]);
        }

        return mat;
    }

    private static Mat CreateImagePointMat(IEnumerable<Point2f> points)
    {
        var pointArray = points.ToArray();
        var mat = new Mat(pointArray.Length, 1, MatType.CV_32FC2);
        for (int i = 0; i < pointArray.Length; i++)
        {
            mat.Set(i, 0, pointArray[i]);
        }

        return mat;
    }

    private static double[] CalculatePerViewErrors(
        IReadOnlyList<IEnumerable<Point3f>> objectPoints,
        IReadOnlyList<IEnumerable<Point2f>> imagePoints,
        double[,] cameraMatrix,
        double[] distortionCoefficients,
        IReadOnlyList<Vec3d> rotationVectors,
        IReadOnlyList<Vec3d> translationVectors
    )
    {
        var errors = new double[imagePoints.Count];

        for (int i = 0; i < imagePoints.Count; i++)
        {
            var objectPointArray = objectPoints[i].ToArray();
            var imagePointArray = imagePoints[i].ToArray();

            Cv2.ProjectPoints(
                objectPointArray,
                ToVectorArray(rotationVectors[i]),
                ToVectorArray(translationVectors[i]),
                cameraMatrix,
                distortionCoefficients,
                out Point2f[] projectedPoints,
                out _,
                0
            );

            double squaredError = 0;
            for (int pointIndex = 0; pointIndex < imagePointArray.Length; pointIndex++)
            {
                double dx = projectedPoints[pointIndex].X - imagePointArray[pointIndex].X;
                double dy = projectedPoints[pointIndex].Y - imagePointArray[pointIndex].Y;
                squaredError += (dx * dx) + (dy * dy);
            }

            errors[i] = Math.Sqrt(squaredError / imagePointArray.Length);
        }

        return errors;
    }

    private static double[,] CreateIdentityMatrix()
    {
        var matrix = new double[3, 3];
        matrix[0, 0] = 1;
        matrix[1, 1] = 1;
        matrix[2, 2] = 1;
        return matrix;
    }

    private static Mat CreateMatrixMat(double[,] values)
    {
        var rowCount = values.GetLength(0);
        var columnCount = values.GetLength(1);
        var mat = new Mat(rowCount, columnCount, MatType.CV_64FC1);

        for (int row = 0; row < rowCount; row++)
        {
            for (int column = 0; column < columnCount; column++)
            {
                mat.Set(row, column, values[row, column]);
            }
        }

        return mat;
    }

    private static double[,] ToDoubleMatrix(Mat mat)
    {
        var values = new double[mat.Rows, mat.Cols];
        for (int row = 0; row < mat.Rows; row++)
        {
            for (int column = 0; column < mat.Cols; column++)
            {
                values[row, column] = mat.Get<double>(row, column);
            }
        }

        return values;
    }

    private static double[] ToDoubleVector(Mat mat)
    {
        var values = new double[mat.Rows * mat.Cols];
        int index = 0;
        for (int row = 0; row < mat.Rows; row++)
        {
            for (int column = 0; column < mat.Cols; column++)
            {
                values[index++] = mat.Get<double>(row, column);
            }
        }

        return values;
    }

    private static Vec3d ReadVec3d(Mat mat)
    {
        var values = ToDoubleVector(mat);
        return new Vec3d(values[0], values[1], values[2]);
    }

    private static double[] ToVectorArray(Vec3d vector) => [vector.Item0, vector.Item1, vector.Item2];
}

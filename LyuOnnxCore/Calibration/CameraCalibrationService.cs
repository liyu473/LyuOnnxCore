using OpenCvSharp;

namespace LyuOnnxCore.Calibration;

/// <summary>
/// Provides OpenCV-based camera calibration helpers.
/// </summary>
public static class CameraCalibrationService
{
    public static ChessboardDetectionResult DetectChessboardCorners(
        Mat image,
        ChessboardCalibrationBoard board,
        ChessboardDetectionOptions? options = null)
    {
        if (image is null || image.Empty())
            throw new ArgumentException("Image cannot be null or empty.", nameof(image));

        ArgumentNullException.ThrowIfNull(board);
        options ??= new ChessboardDetectionOptions();

        using var gray = PrepareGrayImage(image, options.ConvertToGrayFirst);
        var effectiveFlags = GetEffectiveFlags(options);

        Point2f[] corners;
        bool found = options.UseSectorBasedDetector
            ? Cv2.FindChessboardCornersSB(gray, board.PatternSize, out corners, effectiveFlags)
            : Cv2.FindChessboardCorners(gray, board.PatternSize, out corners, effectiveFlags);

        if (found && corners.Length > 0 && options.RefineCornersWithSubPixel && !options.UseSectorBasedDetector)
        {
            Cv2.CornerSubPix(
                gray,
                corners,
                options.SubPixelWindowSize,
                options.SubPixelZeroZone,
                options.SubPixelCriteria);
        }

        return new ChessboardDetectionResult(found, image.Size(), corners);
    }

    public static CameraCalibrationResult CalibrateFromChessboardImages(
        IEnumerable<Mat> images,
        ChessboardCalibrationBoard board,
        ChessboardDetectionOptions? detectionOptions = null,
        CalibrationFlags calibrationFlags = CalibrationFlags.None,
        TermCriteria? criteria = null)
    {
        ArgumentNullException.ThrowIfNull(images);
        ArgumentNullException.ThrowIfNull(board);

        detectionOptions ??= new ChessboardDetectionOptions();

        var imageList = images.Where(static image => image is not null).ToList();
        if (imageList.Count == 0)
            throw new ArgumentException("At least one image is required.", nameof(images));

        var dominantResolutionGroup = imageList
            .Where(static image => !image.Empty())
            .GroupBy(static image => (image.Width, image.Height))
            .OrderByDescending(static group => group.Count())
            .ThenByDescending(static group => group.Key.Width * group.Key.Height)
            .FirstOrDefault();

        if (dominantResolutionGroup is null)
            throw new InvalidOperationException("Unable to infer image size from calibration images.");

        var imageSize = new Size(dominantResolutionGroup.Key.Width, dominantResolutionGroup.Key.Height);
        var objectPoints = new List<IEnumerable<Point3f>>();
        var imagePoints = new List<IEnumerable<Point2f>>();
        var boardPoints = board.CreateObjectPoints();
        int skippedMismatchedResolutionCount = 0;

        foreach (var image in imageList)
        {
            if (image.Empty())
                continue;

            if (image.Size() != imageSize)
            {
                skippedMismatchedResolutionCount++;
                continue;
            }

            var detection = DetectChessboardCorners(image, board, detectionOptions);
            if (!detection.IsSuccess || detection.Corners.Length != board.CornerCount)
                continue;

            objectPoints.Add(boardPoints);
            imagePoints.Add(detection.Corners);
        }

        if (imagePoints.Count < 3)
        {
            throw new InvalidOperationException(
                $"Calibration needs at least 3 valid images, but only {imagePoints.Count} images produced a full board detection for the dominant resolution {imageSize.Width}x{imageSize.Height}. " +
                $"Skipped {skippedMismatchedResolutionCount} images due to mismatched resolution.");
        }

        var cameraMatrix = CreateIdentityMatrix();
        var distortionCoefficients = new double[8];

        double reprojectionError = Cv2.CalibrateCamera(
            objectPoints,
            imagePoints,
            imageSize,
            cameraMatrix,
            distortionCoefficients,
            out Vec3d[] rotationVectors,
            out Vec3d[] translationVectors,
            calibrationFlags,
            criteria ?? new TermCriteria(CriteriaTypes.Eps | CriteriaTypes.MaxIter, 30, 1e-6));

        var perViewErrors = CalculatePerViewErrors(
            objectPoints,
            imagePoints,
            cameraMatrix,
            distortionCoefficients,
            rotationVectors,
            translationVectors);

        return new CameraCalibrationResult(
            imageSize,
            cameraMatrix,
            distortionCoefficients,
            rotationVectors,
            translationVectors,
            reprojectionError,
            perViewErrors,
            imagePoints.Count,
            imageList.Count,
            skippedMismatchedResolutionCount);
    }

    public static CameraCalibrationResult CalibrateFromChessboardImageFiles(
        IEnumerable<string> imagePaths,
        ChessboardCalibrationBoard board,
        ChessboardDetectionOptions? detectionOptions = null,
        CalibrationFlags calibrationFlags = CalibrationFlags.None,
        TermCriteria? criteria = null)
    {
        ArgumentNullException.ThrowIfNull(imagePaths);

        var mats = new List<Mat>();
        try
        {
            foreach (var imagePath in imagePaths.Where(static path => !string.IsNullOrWhiteSpace(path)))
            {
                mats.Add(Cv2.ImRead(imagePath, ImreadModes.Color));
            }

            return CalibrateFromChessboardImages(mats, board, detectionOptions, calibrationFlags, criteria);
        }
        finally
        {
            foreach (var mat in mats)
            {
                mat.Dispose();
            }
        }
    }

    private static double[] CalculatePerViewErrors(
        IReadOnlyList<IEnumerable<Point3f>> objectPoints,
        IReadOnlyList<IEnumerable<Point2f>> imagePoints,
        double[,] cameraMatrix,
        double[] distortionCoefficients,
        IReadOnlyList<Vec3d> rotationVectors,
        IReadOnlyList<Vec3d> translationVectors)
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
                0);

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

    private static Mat PrepareGrayImage(Mat image, bool convertToGrayFirst)
    {
        if (!convertToGrayFirst || image.Channels() == 1)
            return image.Clone();

        var gray = new Mat();
        Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
        return gray;
    }

    private static ChessboardFlags GetEffectiveFlags(ChessboardDetectionOptions options)
    {
        if (!options.UseSectorBasedDetector)
            return options.Flags;

        // The SB detector rejects legacy flags such as AdaptiveThresh / FilterQuads / FastCheck.
        return options.Flags & (
            ChessboardFlags.NormalizeImage |
            ChessboardFlags.Exhaustive |
            ChessboardFlags.Accuracy);
    }

    private static double[,] CreateIdentityMatrix()
    {
        var matrix = new double[3, 3];
        matrix[0, 0] = 1;
        matrix[1, 1] = 1;
        matrix[2, 2] = 1;
        return matrix;
    }

    private static double[] ToVectorArray(Vec3d vector) => [vector.Item0, vector.Item1, vector.Item2];
}

namespace LyuOnnxCore.Models;

/// <summary>
/// 旋转边界框 (Oriented Bounding Box)
/// </summary>
public readonly struct OrientedBoundingBox(float centerX, float centerY, float width, float height, float angle)
{
    /// <summary>
    /// 中心点 X 坐标
    /// </summary>
    public float CenterX { get; init; } = centerX;

    /// <summary>
    /// 中心点 Y 坐标
    /// </summary>
    public float CenterY { get; init; } = centerY;

    /// <summary>
    /// 宽度
    /// </summary>
    public float Width { get; init; } = width;

    /// <summary>
    /// 高度
    /// </summary>
    public float Height { get; init; } = height;

    /// <summary>
    /// 旋转角度（弧度）
    /// </summary>
    public float Angle { get; init; } = angle;

    /// <summary>
    /// 获取旋转角度（度数）
    /// </summary>
    public float AngleDegrees => Angle * 180f / MathF.PI;

    /// <summary>
    /// 获取四个角点坐标
    /// </summary>
    public (float X, float Y)[] GetCornerPoints()
    {
        float cos = MathF.Cos(Angle);
        float sin = MathF.Sin(Angle);
        float halfW = Width / 2;
        float halfH = Height / 2;

        var corners = new (float X, float Y)[4];

        // 左上角
        corners[0] = (
            CenterX + (-halfW * cos - (-halfH) * sin),
            CenterY + (-halfW * sin + (-halfH) * cos)
        );

        // 右上角
        corners[1] = (
            CenterX + (halfW * cos - (-halfH) * sin),
            CenterY + (halfW * sin + (-halfH) * cos)
        );

        // 右下角
        corners[2] = (
            CenterX + (halfW * cos - halfH * sin),
            CenterY + (halfW * sin + halfH * cos)
        );

        // 左下角
        corners[3] = (
            CenterX + (-halfW * cos - halfH * sin),
            CenterY + (-halfW * sin + halfH * cos)
        );

        return corners;
    }

    /// <summary>
    /// 获取包围此旋转框的最小轴对齐边界框
    /// </summary>
    public BoundingBox GetBoundingBox()
    {
        var corners = GetCornerPoints();
        
        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;

        foreach (var (x, y) in corners)
        {
            minX = MathF.Min(minX, x);
            minY = MathF.Min(minY, y);
            maxX = MathF.Max(maxX, x);
            maxY = MathF.Max(maxY, y);
        }

        return new BoundingBox(
            (int)minX,
            (int)minY,
            (int)(maxX - minX),
            (int)(maxY - minY)
        );
    }

    /// <summary>
    /// 面积
    /// </summary>
    public float Area => Width * Height;
}

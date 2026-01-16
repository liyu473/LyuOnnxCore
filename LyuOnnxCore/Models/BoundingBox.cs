namespace LyuOnnxCore.Models;

/// <summary>
/// 边界框
/// </summary>
public readonly struct BoundingBox(int x, int y, int width, int height)
{
    public int X { get; } = x;
    public int Y { get; } = y;
    public int Width { get; } = width;
    public int Height { get; } = height;

    public int Left => X;
    public int Top => Y;
    public int Right => X + Width;
    public int Bottom => Y + Height;
    public int Area => Width * Height;
    public (int X, int Y) Center => (X + Width / 2, Y + Height / 2);
}

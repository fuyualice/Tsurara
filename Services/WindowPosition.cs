namespace Whiteboard.Services;

/// <summary>窓の位置と大きさ（物理ピクセル、仮想スクリーン座標）。</summary>
public sealed record WindowPosition(int Left, int Top, int Width, int Height);

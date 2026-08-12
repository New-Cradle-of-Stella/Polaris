using UnityEngine;

namespace Polaris.API
{
    /// <summary>
    /// 二维坐标/速度，与引擎类型 <see cref="Vector2"/> 解耦并支持隐式互转。
    /// </summary>
    public readonly struct GameVector2
    {
        public float X { get; }

        public float Y { get; }

        public GameVector2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static GameVector2 Zero => new GameVector2(0f, 0f);

        public float Length => Mathf.Sqrt(X * X + Y * Y);

        public static implicit operator Vector2(GameVector2 v) => new Vector2(v.X, v.Y);

        public static implicit operator GameVector2(Vector2 v) => new GameVector2(v.x, v.y);

        public static GameVector2 operator +(GameVector2 a, GameVector2 b) => new GameVector2(a.X + b.X, a.Y + b.Y);

        public static GameVector2 operator -(GameVector2 a, GameVector2 b) => new GameVector2(a.X - b.X, a.Y - b.Y);

        public static GameVector2 operator *(GameVector2 a, float k) => new GameVector2(a.X * k, a.Y * k);

        public override string ToString() => $"({X:0.###}, {Y:0.###})";
    }
}

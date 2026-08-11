using UnityEngine;

namespace Polaris.API
{
    /// <summary>
    /// 二维坐标/速度。用自己的类型而不是直接暴露 <see cref="Vector2"/>，是为了让本层的公开签名
    /// 与"游戏用什么引擎表示位置"解耦：地图坐标在游戏里是以格长 <c>rCLEN</c> 为单位的 float，
    /// 换算规则属于兼容层的内部知识。与 <see cref="Vector2"/> 之间可以隐式转换，
    /// 调用方需要做 Unity 侧的数学时不必写转换代码。
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

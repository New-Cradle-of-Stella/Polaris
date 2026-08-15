using System;
using UnityEngine;
using XX;

namespace Polaris.PUI
{
    /// <summary>
    /// 把一张 <see cref="MImage"/> 装配进 <see cref="DsnDataImg"/> 的唯一实现，编译期路径与热重载路径共用。
    /// <c>UvRect</c> 实际是纹理像素矩形（非归一化 UV），绘制尺寸由 UvRect × scale 决定（与 swidth/sheight 无关）；
    /// 这里把归一化 Uv 换算成像素矩形，并把 scale 乘上"等比缩放到声明 Width×Height"的系数。
    /// </summary>
    public static class PuiImage
    {
        /// <summary>把 <paramref name="image"/> 装进 <paramref name="data"/>，并换算 UvRect 与 scale。</summary>
        /// <param name="image">为 null 时保持 MI = null，不绘制也不抛异常。</param>
        /// <param name="uvW">归一化 Uv 宽；&lt;= 0 视为 1。</param>
        /// <param name="uvH">归一化 Uv 高；&lt;= 0 视为 1。</param>
        /// <param name="boxWidth">声明宽（swidth）；&lt;= 0 表示不做缩放适配。</param>
        /// <param name="boxHeight">声明高（sheight），同上。</param>
        /// <param name="scale">用户缩放倍数，作用在"铺满声明尺寸"基准之上。</param>
        public static void Assign(DsnDataImg data, MImage image,
            float uvX, float uvY, float uvW, float uvH,
            float boxWidth, float boxHeight, float scale)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            data.MI = image;
            data.scale = scale;

            if (image == null)
            {
                return;
            }

            int textureWidth = image.width;
            int textureHeight = image.height;
            if (textureWidth <= 0 || textureHeight <= 0)
            {
                return;
            }

            if (uvW <= 0f)
            {
                uvW = 1f;
            }
            if (uvH <= 0f)
            {
                uvH = 1f;
            }

            float sourceWidth = uvW * textureWidth;
            float sourceHeight = uvH * textureHeight;
            data.UvRect = new Rect(uvX * textureWidth, uvY * textureHeight, sourceWidth, sourceHeight);

            if (boxWidth > 0f && boxHeight > 0f)
            {
                data.scale = scale * Mathf.Min(boxWidth / sourceWidth, boxHeight / sourceHeight);
            }
        }
    }
}

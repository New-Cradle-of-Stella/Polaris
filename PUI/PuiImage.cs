using System;
using UnityEngine;
using XX;

namespace Polaris.PUI
{
    /// <summary>
    /// 把一张 <see cref="MImage"/> 装配进 <see cref="DsnDataImg"/> 的唯一一份实现：编译期路径
    /// （PolarisTools 的 <c>CSharpTextEmitter</c> 生成的 <c>BuildUI</c> 里调用这里）和热重载路径
    /// （<c>PuiHotReloadBridge</c>）共用，两条链路不会一边显示对一边显示错。
    /// <para>
    /// <b>为什么需要这一层：</b><c>DsnDataImg</c> 的两个字段语义都反直觉，直接照字面填会画不出东西
    /// （已用 ILSpy 反编译 unsafeAssem.dll 核实）：
    /// </para>
    /// <list type="number">
    /// <item><description>
    /// <c>UvRect</c> <b>不是</b>归一化 UV，而是<b>纹理像素矩形</b>。<c>FillImageBlock.redrawMesh</c>
    /// 走的是 <c>Md.initForImg(MI.Tx, UvRect)</c>，那个重载的 <c>divide_texture_wh</c> 默认为
    /// <c>true</c>，会把传进去的矩形<b>再除一次</b>纹理宽高。所以字段默认值 <c>(0,0,1,1)</c> 的实际
    /// 含义是"取左下角 1×1 像素"——照默认值绘制只会得到一个 1 像素的点。
    /// </description></item>
    /// <item><description>
    /// 绘制尺寸 = <c>UvRect</c> 的尺寸 × <c>scale</c>（<c>Md.DrawScaleGraph(0,0,scale,scale)</c>
    /// → <c>texture_w * uv_width * scale</c>），跟 <c>swidth</c>/<c>sheight</c> <b>无关</b>：那两个
    /// 只进 <c>FillBlock.widthPixel/heightPixel</c>，是布局占位框的尺寸。一张 1024×1024 的图按
    /// <c>scale = 1</c> 会画成 1024×1024，把窗口整个盖住。
    /// </description></item>
    /// </list>
    /// <para>
    /// 因此这里做两件换算：把编辑器里 0..1 的归一化 Uv 乘成像素矩形；再把 <c>scale</c> 乘上一个
    /// "等比缩放到声明的 Width×Height 之内"的系数——PUI 编辑器里画布上的方框就是这个尺寸，
    /// 所见即所得。用户自己填的 <c>Scale</c> 保留成这个基准之上的倍数（1 = 正好铺满声明尺寸，
    /// 2 = 溢出到两倍，游戏里会被窗口遮罩裁掉）。<c>FillImageBlock</c> 只有一个 <c>scale</c>
    /// 同时作用于两个轴，所以是等比（留白）而不是拉伸。
    /// </para>
    /// </summary>
    public static class PuiImage
    {
        /// <summary>
        /// 把 <paramref name="image"/> 装进 <paramref name="data"/>，并按上面说明换算
        /// <c>UvRect</c> 与 <c>scale</c>。
        /// </summary>
        /// <param name="data">还没交给 <c>addImg</c> 的描述对象。</param>
        /// <param name="image">资源字段里的 <see cref="MImage"/>；为 null 表示资源没绑上，
        /// 此时保持 <c>MI = null</c>，<c>FillImageBlock</c> 什么都不画（不抛异常，别的元素照常显示）。</param>
        /// <param name="uvX">归一化 Uv 左边界（0..1，纹理坐标原点在左下）。</param>
        /// <param name="uvY">归一化 Uv 下边界（0..1）。</param>
        /// <param name="uvW">归一化 Uv 宽；&lt;= 0 视为 1（整张图）。</param>
        /// <param name="uvH">归一化 Uv 高；&lt;= 0 视为 1。</param>
        /// <param name="boxWidth">声明的宽（<c>swidth</c>）：等比缩放的目标框宽。&lt;= 0 表示
        /// 不做缩放适配，按纹理原始像素尺寸 × <paramref name="scale"/> 绘制。</param>
        /// <param name="boxHeight">声明的高（<c>sheight</c>），同上。</param>
        /// <param name="scale">用户填的缩放倍数，作用在"铺满声明尺寸"这个基准之上。</param>
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

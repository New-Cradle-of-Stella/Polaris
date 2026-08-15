// 这个文件同时编进 nullable 关/开两个项目，DTO 字段留给读写端逐个填充，故禁用 nullable 警告。
#nullable disable

using System.Globalization;

namespace Polaris.PUI.Wire
{
    // ============================================================================
    //  PUI 热重载线协议：编辑器（PolarisTools）与游戏进程（Polaris）共享的唯一契约，
    //  通过文件链接确保两侧 opcode/字段定义一致，故不引用 UnityEngine/WPF 类型。
    //  改动规则：枚举只在末尾追加，不复用/挪动数值；改变字节序列需同时 +1 PuiProtocol.Version。
    //  可见性为 public：编辑器侧 WPF 绑定要求这些枚举类型是 public。
    // ============================================================================

    /// <summary>线协议版本。握手时校验，不匹配就明确报错而不是静默按错误的字节序列解析。</summary>
    public static class PuiProtocol
    {
        // v2：AddImage 载荷追加了 PuiImageParams.ImageResource。
        public const int Version = 2;
    }

    /// <summary>线协议操作码：与 <c>IPuiEmitter</c> 方法一一对应，由编辑器写出、游戏进程读回执行。</summary>
    public enum PuiWireOpcode
    {
        CreateWindow = 0,
        SetFrameType = 1,
        SetFocusable = 2,
        AddText = 3,
        AddButton = 4,
        AddSeparator = 5,
        Br = 6,
        SetLineAlign = 7,
        SetDefaultLineAlign = 8,
        AddButtonMulti = 9,
        AddChecks = 10,
        AddRadio = 11,
        AddSlider = 12,
        AddInput = 13,
        AddNumCounter = 14,
        AddColorCell = 15,
        AddImage = 16,
        OnBuildCompleted = 17,
    }

    /// <summary>对应 <c>nel.UiBoxDesignerFamily.MASKTYPE</c>。</summary>
    public enum PuiMaskType { NoMask, Box, Scroll }

    /// <summary>对应 <c>nel.UiBox.FRAMETYPE</c>；NoOverride 表示不改动，用建好时的默认值。</summary>
    public enum PuiFrameType { None, Main, OneLine, Dark, DarkSimple, NoOverride }

    public enum PuiTextAlign { Left, Center, Right, Auto }

    public enum PuiLineAlign { Left, Center, Right }

    /// <summary>解析后的 RGBA 颜色；不用 Color32/WPF Color，因为本文件要跨 netstandard2.1 与 net472 编译。</summary>
    public readonly struct PuiColor
    {
        public readonly byte R, G, B, A;

        public PuiColor(byte r, byte g, byte b, byte a)
        {
            R = r; G = g; B = b; A = a;
        }

        /// <summary>把 "RRGGBBAA" 解析成颜色；格式不对时回退到 fallbackHex（同样失败则纯黑不透明）。</summary>
        public static PuiColor Parse(string hex, string fallbackHex)
        {
            if (TryParse(hex, out PuiColor color) || TryParse(fallbackHex, out color))
            {
                return color;
            }

            return new PuiColor(0, 0, 0, 255);
        }

        /// <summary>解析 "RRGGBBAA"（不是 WPF 惯用的 AARRGGBB）。</summary>
        public static bool TryParse(string hex, out PuiColor color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(hex))
            {
                return false;
            }

            string h = hex.Trim();
            if (h.Length != 8)
            {
                return false;
            }

            if (!TryParseByte(h, 0, out byte r) || !TryParseByte(h, 2, out byte g)
                || !TryParseByte(h, 4, out byte b) || !TryParseByte(h, 6, out byte a))
            {
                return false;
            }

            color = new PuiColor(r, g, b, a);
            return true;
        }

        static bool TryParseByte(string hex, int offset, out byte value) =>
            byte.TryParse(hex.Substring(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
    }

    // 尺寸类字段统一用 double（而非 nel 实际的 float/int），取整/精度转换留给各自的落地代码做。

    /// <summary>CreateWindow 的载荷。</summary>
    public sealed class PuiCreateWindowParams
    {
        public string Name;
        public double PixelX;
        public double PixelY;
        public double Width;
        public double Height;
        public int AppearDir;
        public double AppearLen;
        public PuiMaskType Mask;
    }

    public sealed class PuiFrameTypeParams
    {
        public PuiFrameType FrameType;
    }

    public sealed class PuiLineAlignParams
    {
        public PuiLineAlign Align;
    }

    /// <summary>OnBuildCompleted 的载荷：只有一个方法名。</summary>
    public sealed class PuiMethodNameParams
    {
        public string MethodName;
    }

    public sealed class PuiTextParams
    {
        public string Name;
        public string Text;
        public PuiTextAlign Align;
        public double Width;
        public double Height;
        public bool Html;
        public double Size;
        public double LineSpacing;
        public double LetterSpacing;
        public PuiColor TextColor;
        public PuiColor BackgroundColor;
        public PuiColor BorderColor;
    }

    public sealed class PuiButtonParams
    {
        public string Name;
        public string Title;
        public string Skin;
        public double Width;
        public double Height;
        public string OnClick;

        /// <summary>非空表示该按钮同时是状态连接点的触发点，值即触发 key。</summary>
        public string TransitionTriggerKey;
    }

    public sealed class PuiSeparatorParams
    {
        /// <summary>竖线取元素 Height；横线固定为 0（独占一整行，宽度由剩余可用空间决定）。</summary>
        public double Width;
        public bool Vertical;
        public double LineHeight;
        public double MarginBefore;
        public double MarginAfter;
        public double DashedLength;
        public double DrawWidthRate;
        public PuiColor Color;
    }

    public sealed class PuiButtonMultiParams
    {
        public string Name;
        public string[] Titles;
        public string Skin;
        public double Width;
        public double Height;
        public int Columns;
        public double MarginW;
        public double MarginH;
        public int NaviLoop;
        public int DefMask;
        public int LockedMask;
        public string OnClick;
    }

    public sealed class PuiChecksParams
    {
        public string Name;
        public string[] Keys;
        public string[] Descs;
        public string Skin;
        public double Width;
        public double Height;
        public double Scale;
        public int Columns;
        /// <summary>已取整（Checks 的 margin_w/h 是 int 字段）。</summary>
        public int MarginW;
        public int MarginH;
        public int NaviLoop;
        public int DefMask;
        public string OnClick;
    }

    public sealed class PuiRadioParams
    {
        public string Name;
        public string[] Keys;
        public string[] Descs;
        public string Skin;
        public double Width;
        public double Height;
        public int Columns;
        public double Scale;
        /// <summary>已取整（Radio 的 margin_w/h 是 int 字段）。</summary>
        public int MarginW;
        public int MarginH;
        /// <summary>已取整（Radio.def 是索引）。</summary>
        public int Def;
        public bool ValueReturnName;
        public bool AllFunctionSame;
        public int NaviLoop;
        public bool RowMode;
        public string OnClick;
        public string OnChanged;
    }

    public sealed class PuiSliderParams
    {
        public string Name;
        public string Title;
        public string Skin;
        public string SkinTitle;
        public double Min;
        public double Max;
        public double Step;
        public double Width;
        public double Height;
        /// <summary>Slider.def 是数值，不取整。</summary>
        public double Def;
        public bool SubmitHolding;
        public int CheckboxMode;
        public string[] DescKeys;
        public double SetterWidth;
        public string OnClick;
        public string OnChanged;
    }

    public sealed class PuiInputParams
    {
        public string Name;
        /// <summary>输入框默认文本（对应元素的 Text，不是 Def）。</summary>
        public string Def;
        public string Label;
        public string Skin;
        public double Width;
        public double BoundsWidth;
        public int FontSize;
        public double Height;
        public int MaxLen;
        /// <summary>Input.min/max 是 double 字段（不带 f 后缀）。</summary>
        public double Min;
        public double Max;
        public bool Integer;
        public bool HexInteger;
        public bool Number;
        public int MultiLine;
        public bool LabelTop;
        public bool ReturnBlur;
        public bool Editable;
        public bool AllocEmpty;
        public int ChangedDelayMaxT;
        public string OnChanged;
        public string OnChangedDelay;
    }

    public sealed class PuiNumCounterParams
    {
        public string Name;
        /// <summary>已取整（NumCounter.def 是整数）。</summary>
        public int Def;
        public bool Locked;
        public string Skin;
        public double Width;
        public double Height;
        public int NaviLoop;
        public int MinVal;
        public int MaxVal;
        public int Digit;
        public bool SlideCurDigitOnly;
        public string OnClick;
    }

    public sealed class PuiColorCellParams
    {
        public string Name;
        public PuiColor DefColor;
        public bool OpenPrompt;
        public bool UseText;
        public bool UseAlpha;
        public string Title;
        public string Skin;
        public string SkinTitle;
        public double Width;
        public double Height;
        public string OnColorPromptDone;
    }

    public sealed class PuiImageParams
    {
        public string Name;
        public double Width;
        public double Height;
        public double Scale;
        public bool StencilLessEqual;
        public double UvX;
        public double UvY;
        public double UvW;
        public double UvH;

        /// <summary>PolarisRes 挂载相对路径；空表示不走这条路径。见 <see cref="ImageResource"/>。</summary>
        public string ImageSource;

        /// <summary><c>[PolarisResource]</c> static 字段引用（如 <c>MyMod.Res.testImage</c>），非空时优先于 <see cref="ImageSource"/>。两者皆空则不设置图片。</summary>
        public string ImageResource;
    }

    /// <summary>一条热重载指令：操作码 + 载荷；SetFocusable/Br/SetDefaultLineAlign 没有载荷（Payload 为 null）。</summary>
    public sealed class PuiWireCommand
    {
        public PuiWireOpcode Opcode;
        public object Payload;
    }
}

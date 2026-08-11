// 这个文件被编进两个 nullable 设置不同的项目（Polaris 关、PolarisTools 开），
// 所以自己固定语境，免得同一份源码在一边干净、在另一边刷一屏 CS8618/CS8625。
// 载荷类型是线协议 DTO：字段由读写端逐个填充，"未赋值即 null/default" 本来就是预期语义。
#nullable disable

using System.Globalization;

namespace Polaris.PUI.Wire
{
    // ============================================================================
    //  PUI 热重载线协议 —— 编辑器（PolarisTools，net472 VSIX）与游戏进程
    //  （Polaris，netstandard2.1）之间唯一的共享契约。
    //
    //  本文件是**唯一的一份定义**：PolarisTools 通过 <Compile Include Link="..."/>
    //  把它链接进自己的编译（见 PolarisTools.csproj 与 Directory.Build.props 里的
    //  $(PolarisDir)），因此两侧的 opcode 数值、载荷字段与字段顺序天然一致，
    //  不再需要靠人眼比对两份手抄。
    //
    //  这也是它不能引用 UnityEngine / WPF 任何类型的原因——颜色因此用中立的
    //  PuiColor，两侧各自提供到本框架类型的转换（游戏侧见 PuiColorExtensions）。
    //
    //  改动规则：
    //   - PuiWireOpcode 只在末尾追加，永远不复用/挪动已有数值；
    //   - 四个镜像枚举同样只在末尾追加（线上按 int 传输）；
    //   - 任何会改变读写字节序列的改动，都必须同时 +1 PuiProtocol.Version。
    //
    //  可见性为 public 而不是 internal：编辑器侧的 PuiElement / PuiVisualEditorViewModel
    //  是 WPF 数据绑定要求的 public 类型，它们的属性直接用这里的枚举，internal 会触发
    //  CS0053。放在独立的 Polaris.PUI.Wire 命名空间下，`using Polaris.PUI;` 的模组作者
    //  看不到这些类型；把线协议当作公开契约本身也说得通——第三方可以据此写自己的客户端。
    // ============================================================================

    /// <summary>线协议版本。握手时校验，不匹配就明确报错而不是静默按错误的字节序列解析。</summary>
    public static class PuiProtocol
    {
        // v2：AddImage 载荷末尾追加了 PuiImageParams.ImageResource（资源字段引用）。
        public const int Version = 2;
    }

    /// <summary>
    /// 线协议操作码：一份和 <c>IPuiEmitter</c> 方法一一对应的原语指令集，
    /// 由编辑器写出、游戏进程读回并照单执行。
    /// </summary>
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

    /// <summary>
    /// 解析后的 RGBA 颜色（已套用过默认值回退）。刻意不用 <c>UnityEngine.Color32</c> 或
    /// WPF 的 <c>Color</c>：这个文件要能同时编进 netstandard2.1 与 net472 两侧。
    /// </summary>
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

        /// <summary>
        /// 解析 "RRGGBBAA"（注意不是 WPF 惯用的 AARRGGBB）。编辑器的色块预览与颜色选择控件
        /// 都走这一份实现，保证"编辑时看到的颜色"跟"生成进代码/发上线的颜色"按同一套规则解析。
        /// </summary>
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

    // 注意：尺寸类字段一律用 double（而不是 nel 里实际的 float/int），并且直接原样传递
    // PuiElement 的原始 double 值，不在这一层做取整/精度转换——是 float 还是 int、
    // 要不要 Math.Round，属于"落地方式"的决定（CSharpTextEmitter 用 F()/I() 格式化成
    // 对应字面量文本；热重载桥在真正调用 nel API 时再做 (float)/(int) 转换）。
    // 早转换会让 CSharpTextEmitter 输出的字面量文本出现精度偏差。

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

        /// <summary>
        /// 非空表示这个按钮同时是某条状态连接点（PuiStateTransition，TriggerType==ButtonClick）
        /// 的触发点，值即触发 key（目前就是按钮自身的 Name）。
        /// </summary>
        public string TransitionTriggerKey;
    }

    public sealed class PuiSeparatorParams
    {
        /// <summary>
        /// 竖线取元素的 Height；横线固定为 0，表示"让真机按当前行剩余可用宽度决定占位宽度"
        /// （横线永远独占一整行，跟 Ratio 无关）。
        /// </summary>
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

        /// <summary>
        /// <c>[PolarisResource]</c> <c>MImage</c> static 字段的引用，形如
        /// <c>MyMod.Res.testImage</c>（编辑器的资源下拉框选出来的那个字段）。非空时优先于
        /// <see cref="ImageSource"/>：字段的值由 PolarisRes 的 <c>AutoBindScanner</c> 在插件
        /// 加载时就按 <c>[PolarisResourceFolder]</c> 挂载并回填好了，热重载侧只是反射把它读出来。
        /// 两者都为空表示未设置图片来源，MI 保持 null。
        /// </summary>
        public string ImageResource;
    }

    /// <summary>
    /// 一条热重载指令：操作码 + 载荷。载荷类型跟 <see cref="PuiWireOpcode"/> 一一对应
    /// （见 PuiWireWriter / PuiWireReader 的 switch）；SetFocusable/Br/SetDefaultLineAlign
    /// 没有载荷（Payload 为 null）。
    /// </summary>
    public sealed class PuiWireCommand
    {
        public PuiWireOpcode Opcode;
        public object Payload;
    }
}

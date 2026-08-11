using System;
using Polaris.PUI.Wire;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Polaris.PUI.HotReload
{
    /// <summary>
    /// <see cref="PuiWireWriter"/>（PolarisSourceCodeGenerator 项目）写出的字节流的读取端。
    /// 字段顺序、类型必须逐条对应；改一边务必同步改另一边。
    /// 每种载荷都用单个对象初始化器一次读完：C# 保证对象初始化器里的赋值按书写顺序求值，
    /// 因此下面每个初始化器里字段自上而下的顺序就是线协议的字节顺序，可以直接跟写入端对读。
    /// </summary>
    internal static class PuiWireReader
    {
        public static (string puiName, List<PuiWireCommand> commands) Read(BinaryReader r)
        {
            // 版本号是帧里的第一个字段，先校验再解析，别拿错误的字节布局去填载荷。
            int version = r.ReadInt32();
            if (version != PuiProtocol.Version)
            {
                throw new InvalidOperationException(
                    $"PUI 热重载线协议版本不匹配：编辑器发来 v{version}，本游戏侧是 v{PuiProtocol.Version}。" +
                    "请让 PolarisTools 扩展与 Polaris 保持同一次构建。");
            }

            string puiName = r.ReadString();
            int count = r.ReadInt32();
            var commands = new List<PuiWireCommand>(count);

            for (int i = 0; i < count; i++)
            {
                var opcode = (PuiWireOpcode)r.ReadInt32();
                object payload = null;

                switch (opcode)
                {
                    case PuiWireOpcode.CreateWindow:
                        payload = new PuiCreateWindowParams
                        {
                            Name = r.ReadString(),
                            PixelX = r.ReadDouble(),
                            PixelY = r.ReadDouble(),
                            Width = r.ReadDouble(),
                            Height = r.ReadDouble(),
                            AppearDir = r.ReadInt32(),
                            AppearLen = r.ReadDouble(),
                            Mask = (PuiMaskType)r.ReadInt32(),
                        };
                        break;

                    case PuiWireOpcode.SetFrameType:
                        payload = new PuiFrameTypeParams { FrameType = (PuiFrameType)r.ReadInt32() };
                        break;

                    case PuiWireOpcode.AddText:
                        payload = new PuiTextParams
                        {
                            Name = r.ReadString(),
                            Text = r.ReadString(),
                            Align = (PuiTextAlign)r.ReadInt32(),
                            Width = r.ReadDouble(),
                            Height = r.ReadDouble(),
                            Html = r.ReadBoolean(),
                            Size = r.ReadDouble(),
                            LineSpacing = r.ReadDouble(),
                            LetterSpacing = r.ReadDouble(),
                            TextColor = ReadColor(r),
                            BackgroundColor = ReadColor(r),
                            BorderColor = ReadColor(r),
                        };
                        break;

                    case PuiWireOpcode.AddButton:
                        payload = new PuiButtonParams
                        {
                            Name = r.ReadString(),
                            Title = r.ReadString(),
                            Skin = r.ReadString(),
                            Width = r.ReadDouble(),
                            Height = r.ReadDouble(),
                            OnClick = r.ReadString(),
                            TransitionTriggerKey = r.ReadString(),
                        };
                        break;

                    case PuiWireOpcode.AddSeparator:
                        payload = new PuiSeparatorParams
                        {
                            Width = r.ReadDouble(),
                            Vertical = r.ReadBoolean(),
                            LineHeight = r.ReadDouble(),
                            MarginBefore = r.ReadDouble(),
                            MarginAfter = r.ReadDouble(),
                            DashedLength = r.ReadDouble(),
                            DrawWidthRate = r.ReadDouble(),
                            Color = ReadColor(r),
                        };
                        break;

                    case PuiWireOpcode.SetLineAlign:
                        payload = new PuiLineAlignParams { Align = (PuiLineAlign)r.ReadInt32() };
                        break;

                    case PuiWireOpcode.AddButtonMulti:
                        payload = new PuiButtonMultiParams
                        {
                            Name = r.ReadString(),
                            Titles = ReadStringArray(r),
                            Skin = r.ReadString(),
                            Width = r.ReadDouble(),
                            Height = r.ReadDouble(),
                            Columns = r.ReadInt32(),
                            MarginW = r.ReadDouble(),
                            MarginH = r.ReadDouble(),
                            NaviLoop = r.ReadInt32(),
                            DefMask = r.ReadInt32(),
                            LockedMask = r.ReadInt32(),
                            OnClick = r.ReadString(),
                        };
                        break;

                    case PuiWireOpcode.AddChecks:
                        payload = new PuiChecksParams
                        {
                            Name = r.ReadString(),
                            Keys = ReadStringArray(r),
                            Descs = ReadStringArray(r),
                            Skin = r.ReadString(),
                            Width = r.ReadDouble(),
                            Height = r.ReadDouble(),
                            Scale = r.ReadDouble(),
                            Columns = r.ReadInt32(),
                            MarginW = r.ReadInt32(),
                            MarginH = r.ReadInt32(),
                            NaviLoop = r.ReadInt32(),
                            DefMask = r.ReadInt32(),
                            OnClick = r.ReadString(),
                        };
                        break;

                    case PuiWireOpcode.AddRadio:
                        payload = new PuiRadioParams
                        {
                            Name = r.ReadString(),
                            Keys = ReadStringArray(r),
                            Descs = ReadStringArray(r),
                            Skin = r.ReadString(),
                            Width = r.ReadDouble(),
                            Height = r.ReadDouble(),
                            Columns = r.ReadInt32(),
                            Scale = r.ReadDouble(),
                            MarginW = r.ReadInt32(),
                            MarginH = r.ReadInt32(),
                            Def = r.ReadInt32(),
                            ValueReturnName = r.ReadBoolean(),
                            AllFunctionSame = r.ReadBoolean(),
                            NaviLoop = r.ReadInt32(),
                            RowMode = r.ReadBoolean(),
                            OnClick = r.ReadString(),
                            OnChanged = r.ReadString(),
                        };
                        break;

                    case PuiWireOpcode.AddSlider:
                        payload = new PuiSliderParams
                        {
                            Name = r.ReadString(),
                            Title = r.ReadString(),
                            Skin = r.ReadString(),
                            SkinTitle = r.ReadString(),
                            Min = r.ReadDouble(),
                            Max = r.ReadDouble(),
                            Step = r.ReadDouble(),
                            Width = r.ReadDouble(),
                            Height = r.ReadDouble(),
                            Def = r.ReadDouble(),
                            SubmitHolding = r.ReadBoolean(),
                            CheckboxMode = r.ReadInt32(),
                            DescKeys = ReadStringArray(r),
                            SetterWidth = r.ReadDouble(),
                            OnClick = r.ReadString(),
                            OnChanged = r.ReadString(),
                        };
                        break;

                    case PuiWireOpcode.AddInput:
                        payload = new PuiInputParams
                        {
                            Name = r.ReadString(),
                            Def = r.ReadString(),
                            Label = r.ReadString(),
                            Skin = r.ReadString(),
                            Width = r.ReadDouble(),
                            BoundsWidth = r.ReadDouble(),
                            FontSize = r.ReadInt32(),
                            Height = r.ReadDouble(),
                            MaxLen = r.ReadInt32(),
                            Min = r.ReadDouble(),
                            Max = r.ReadDouble(),
                            Integer = r.ReadBoolean(),
                            HexInteger = r.ReadBoolean(),
                            Number = r.ReadBoolean(),
                            MultiLine = r.ReadInt32(),
                            LabelTop = r.ReadBoolean(),
                            ReturnBlur = r.ReadBoolean(),
                            Editable = r.ReadBoolean(),
                            AllocEmpty = r.ReadBoolean(),
                            ChangedDelayMaxT = r.ReadInt32(),
                            OnChanged = r.ReadString(),
                            OnChangedDelay = r.ReadString(),
                        };
                        break;

                    case PuiWireOpcode.AddNumCounter:
                        payload = new PuiNumCounterParams
                        {
                            Name = r.ReadString(),
                            Def = r.ReadInt32(),
                            Locked = r.ReadBoolean(),
                            Skin = r.ReadString(),
                            Width = r.ReadDouble(),
                            Height = r.ReadDouble(),
                            NaviLoop = r.ReadInt32(),
                            MinVal = r.ReadInt32(),
                            MaxVal = r.ReadInt32(),
                            Digit = r.ReadInt32(),
                            SlideCurDigitOnly = r.ReadBoolean(),
                            OnClick = r.ReadString(),
                        };
                        break;

                    case PuiWireOpcode.AddColorCell:
                        payload = new PuiColorCellParams
                        {
                            Name = r.ReadString(),
                            DefColor = ReadColor(r),
                            OpenPrompt = r.ReadBoolean(),
                            UseText = r.ReadBoolean(),
                            UseAlpha = r.ReadBoolean(),
                            Title = r.ReadString(),
                            Skin = r.ReadString(),
                            SkinTitle = r.ReadString(),
                            Width = r.ReadDouble(),
                            Height = r.ReadDouble(),
                            OnColorPromptDone = r.ReadString(),
                        };
                        break;

                    case PuiWireOpcode.AddImage:
                        payload = new PuiImageParams
                        {
                            Name = r.ReadString(),
                            Width = r.ReadDouble(),
                            Height = r.ReadDouble(),
                            Scale = r.ReadDouble(),
                            StencilLessEqual = r.ReadBoolean(),
                            UvX = r.ReadDouble(),
                            UvY = r.ReadDouble(),
                            UvW = r.ReadDouble(),
                            UvH = r.ReadDouble(),
                            ImageSource = r.ReadString(),
                        };
                        break;

                    case PuiWireOpcode.OnBuildCompleted:
                        payload = new PuiMethodNameParams { MethodName = r.ReadString() };
                        break;

                    // 无载荷的操作码；未知操作码（新版编辑器 + 旧版游戏）同样只能当作无载荷跳过。
                    case PuiWireOpcode.SetFocusable:
                    case PuiWireOpcode.Br:
                    case PuiWireOpcode.SetDefaultLineAlign:
                    default:
                        break;
                }

                commands.Add(new PuiWireCommand { Opcode = opcode, Payload = payload });
            }

            return (puiName, commands);
        }

        private static PuiColor ReadColor(BinaryReader r)
        {
            byte red = r.ReadByte();
            byte green = r.ReadByte();
            byte blue = r.ReadByte();
            byte alpha = r.ReadByte();
            return new PuiColor(red, green, blue, alpha);
        }

        private static string[] ReadStringArray(BinaryReader r)
        {
            bool hasValue = r.ReadBoolean();
            if (!hasValue)
            {
                return null;
            }

            int length = r.ReadInt32();
            var items = new string[length];
            for (int i = 0; i < length; i++)
            {
                items[i] = r.ReadString();
            }

            return items;
        }
    }
}

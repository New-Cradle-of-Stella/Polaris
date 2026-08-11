using Polaris.PUI.Wire;
using System;
using System.Collections.Generic;
using System.Reflection;
using nel;
using UnityEngine;
using XX;

namespace Polaris.PUI.HotReload
{
    /// <summary>
    /// 热重载的"debug 桥"：逐条执行 <see cref="PuiWireCommand"/>，直接调用对应的 nel API
    /// （family.Create / designer.addP / designer.addButton / ...）——不做任何"这个 XML
    /// 属性是什么意思、默认值该是多少"的判断，这些判断已经在编辑器侧的 PuiTreeWalker 做完了。
    /// 回调方法名（OnClick/OnChanged/...）通过反射解析到 <paramref name="handler"/>
    /// 已经编译好的方法上；解析失败（方法不存在/签名不匹配）会抛出异常，调用方
    /// （<see cref="PUIHotReloadRuntime"/>）负责在临时对象上捕获它并回滚，不影响当前显示的 UI。
    /// </summary>
    internal static class PuiHotReloadBridge
    {
        public static UiBoxDesigner Apply(UiBoxDesignerFamily family, IReadOnlyList<PuiWireCommand> commands, IPUI handler)
        {
            UiBoxDesigner designer = null;

            foreach (PuiWireCommand cmd in commands)
            {
                switch (cmd.Opcode)
                {
                    case PuiWireOpcode.CreateWindow:
                    {
                        var p = (PuiCreateWindowParams)cmd.Payload;
                        designer = family.Create(
                            p.Name ?? "",
                            (float)p.PixelX,
                            (float)p.PixelY,
                            (float)p.Width,
                            (float)p.Height,
                            p.AppearDir,
                            (float)p.AppearLen,
                            ToMask(p.Mask));
                        break;
                    }

                    case PuiWireOpcode.SetFrameType:
                        designer.getBox().frametype = ToFrameType(((PuiFrameTypeParams)cmd.Payload).FrameType);
                        break;

                    case PuiWireOpcode.SetFocusable:
                        designer.Focusable();
                        break;

                    case PuiWireOpcode.AddText:
                    {
                        var p = (PuiTextParams)cmd.Payload;
                        var data = new DsnDataP
                        {
                            name = p.Name ?? "",
                            // 显示用字符串统一过 PuiText.Resolve（& 开头查 TX.Get、&& 脱转义）。
                            // 编译期那条路径由 CSharpTextEmitter 静态展开，两边共用
                            // PuiLocalizedString 的判定，生成的窗口和热重载推上去的窗口文案一致。
                            text = PuiText.Resolve(p.Text),
                            alignx = ToAlign(p.Align),
                            swidth = (float)p.Width,
                            sheight = (float)p.Height,
                            html = p.Html,
                            size = (float)p.Size,
                            lineSpacing = (float)p.LineSpacing,
                            letterSpacing = (float)p.LetterSpacing,
                            TxCol = p.TextColor.ToColor32(),
                            Col = p.BackgroundColor.ToColor32(),
                            TxBorderCol = p.BorderColor.ToColor32(),
                        };
                        designer.addP(data);
                        break;
                    }

                    case PuiWireOpcode.AddButton:
                    {
                        var p = (PuiButtonParams)cmd.Payload;
                        var data = new DsnDataButton { w = (float)p.Width, h = (float)p.Height };
                        if (!string.IsNullOrEmpty(p.Name)) data.name = p.Name;
                        // 判空判的仍是**原始串**，不是解析后的结果：编译期
                        // CSharpTextEmitter.AddButton 也是"原始串非空才发 title"，
                        // 这样即使某个键解析出空文案，两条路径的行为也还是一样的。
                        if (!string.IsNullOrEmpty(p.Title)) data.title = PuiText.Resolve(p.Title);
                        if (!string.IsNullOrEmpty(p.Skin)) data.skin = p.Skin;
                        if (!string.IsNullOrEmpty(p.TransitionTriggerKey))
                        {
                            // 同时是状态连接点触发点：不能再直接把 fnClick 指向用户 OnClick——
                            // 包一层闭包，先调用原 OnClick（如果有），再喊 PUIRuntime.RaiseEvent，
                            // 跟编译期 codegen（CSharpTextEmitter.ResolveFnClick）的语义保持一致。
                            FnBtnBindings userClick = string.IsNullOrEmpty(p.OnClick)
                                ? null
                                : (FnBtnBindings)Delegate.CreateDelegate(typeof(FnBtnBindings), handler, p.OnClick);
                            string triggerKey = p.TransitionTriggerKey;
                            data.fnClick = b =>
                            {
                                bool result = userClick?.Invoke(b) ?? true;
                                PUIRuntime.Of(handler)?.RaiseEvent(triggerKey);
                                return result;
                            };
                        }
                        else
                        {
                            SetDelegateField(data, "fnClick", handler, p.OnClick);
                        }
                        designer.addButton(data);
                        break;
                    }

                    case PuiWireOpcode.AddSeparator:
                    {
                        var p = (PuiSeparatorParams)cmd.Payload;
                        var data = new DsnDataHr
                        {
                            swidth = (float)p.Width,
                            vertical = p.Vertical,
                            line_height = (float)p.LineHeight,
                            margin_t = (float)p.MarginBefore,
                            margin_b = (float)p.MarginAfter,
                            dashed_oneline_lgt = (float)p.DashedLength,
                            draw_width_rate = (float)p.DrawWidthRate,
                            Col = p.Color.ToColor32(),
                        };
                        designer.addHr(data);
                        break;
                    }

                    case PuiWireOpcode.Br:
                        designer.Br();
                        break;

                    case PuiWireOpcode.SetLineAlign:
                        designer.alignx = ToAlign(((PuiLineAlignParams)cmd.Payload).Align);
                        break;

                    case PuiWireOpcode.SetDefaultLineAlign:
                        designer.alignx = ALIGN.LEFT;
                        break;

                    case PuiWireOpcode.AddButtonMulti:
                    {
                        var p = (PuiButtonMultiParams)cmd.Payload;
                        var data = new DsnDataButtonMulti
                        {
                            name = p.Name ?? "",
                            titles = PuiText.ResolveAll(p.Titles),
                            skin = p.Skin ?? "",
                            w = (float)p.Width,
                            h = (float)p.Height,
                            clms = p.Columns,
                            margin_w = (float)p.MarginW,
                            margin_h = (float)p.MarginH,
                            navi_loop = p.NaviLoop,
                            def = p.DefMask,
                            locked = p.LockedMask,
                        };
                        SetDelegateField(data, "fnClick", handler, p.OnClick);
                        designer.addButtonMulti(data);
                        break;
                    }

                    case PuiWireOpcode.AddChecks:
                    {
                        var p = (PuiChecksParams)cmd.Payload;
                        var data = new DsnDataChecks
                        {
                            name = p.Name ?? "",
                            keys = p.Keys,
                            skin = p.Skin ?? "",
                            w = (float)p.Width,
                            h = (float)p.Height,
                            scale = (float)p.Scale,
                            clms = p.Columns,
                            margin_w = p.MarginW,
                            margin_h = p.MarginH,
                            navi_loop = p.NaviLoop,
                            def = p.DefMask,
                        };
                        // descs 是显示给玩家的说明文字，过 Resolve；同结构里的 keys 是回调
                        // 返回值用的标识符，保持原样不解析。
                        if (p.Descs != null) data.descs = PuiText.ResolveAll(p.Descs);
                        SetDelegateField(data, "fnClick", handler, p.OnClick);
                        designer.addChecks(data);
                        break;
                    }

                    case PuiWireOpcode.AddRadio:
                    {
                        var p = (PuiRadioParams)cmd.Payload;
                        var data = new DsnDataRadio
                        {
                            name = p.Name ?? "",
                            keys = p.Keys,
                            skin = p.Skin ?? "",
                            w = (float)p.Width,
                            h = (float)p.Height,
                            clms = p.Columns,
                            scale = (float)p.Scale,
                            margin_w = p.MarginW,
                            margin_h = p.MarginH,
                            def = p.Def,
                            value_return_name = p.ValueReturnName,
                            all_function_same = p.AllFunctionSame,
                            navi_loop = p.NaviLoop,
                        };
                        // descs 是显示给玩家的说明文字，过 Resolve；同结构里的 keys 是回调
                        // 返回值用的标识符，保持原样不解析。
                        if (p.Descs != null) data.descs = PuiText.ResolveAll(p.Descs);
                        SetDelegateField(data, "fnClick", handler, p.OnClick);
                        SetDelegateField(data, "fnChanged", handler, p.OnChanged);
                        if (p.RowMode) data = data.RowMode(p.Skin ?? "");
                        designer.addRadio(data);
                        break;
                    }

                    case PuiWireOpcode.AddSlider:
                    {
                        var p = (PuiSliderParams)cmd.Payload;
                        var data = new DsnDataSlider
                        {
                            name = p.Name ?? "",
                            // title 是滑条标题（显示用），过 Resolve；同结构的 Adesc_keys
                            // 名字即 keys，不在本地化范围内。
                            title = PuiText.Resolve(p.Title),
                            skin = p.Skin ?? "",
                            skin_title = p.SkinTitle ?? "",
                            mn = (float)p.Min,
                            mx = (float)p.Max,
                            valintv = (float)p.Step,
                            w = (float)p.Width,
                            h = (float)p.Height,
                            def = (float)p.Def,
                            submit_holding = p.SubmitHolding,
                            checkbox_mode = (byte)p.CheckboxMode,
                            Adesc_keys = p.DescKeys,
                        };
                        SetDelegateField(data, "fnClick", handler, p.OnClick);
                        SetDelegateField(data, "fnChanged", handler, p.OnChanged);
                        designer.addSliderCT(data, (float)p.SetterWidth);
                        break;
                    }

                    case PuiWireOpcode.AddInput:
                    {
                        var p = (PuiInputParams)cmd.Payload;
                        var data = new DsnDataInput
                        {
                            name = p.Name ?? "",
                            // def 是输入框初始值（数据，不是标签），保持原样；
                            // label 才是显示给玩家的那行字。
                            def = p.Def ?? "",
                            label = PuiText.Resolve(p.Label),
                            skin = p.Skin ?? "",
                            w = (float)p.Width,
                            bounds_w = (float)p.BoundsWidth,
                            size = p.FontSize,
                            h = (float)p.Height,
                            max_len = p.MaxLen,
                            min = p.Min,
                            max = p.Max,
                            integer = p.Integer,
                            hex_integer = p.HexInteger,
                            number = p.Number,
                            multi_line = p.MultiLine,
                            label_top = p.LabelTop,
                            return_blur = p.ReturnBlur,
                            editable = p.Editable,
                            alloc_empty = p.AllocEmpty,
                            changed_delay_maxt = p.ChangedDelayMaxT,
                        };
                        SetDelegateField(data, "fnChanged", handler, p.OnChanged);
                        SetDelegateField(data, "fnChangedDelay", handler, p.OnChangedDelay);
                        designer.addInput(data);
                        break;
                    }

                    case PuiWireOpcode.AddNumCounter:
                    {
                        var p = (PuiNumCounterParams)cmd.Payload;
                        var data = new DsnDataNumCounter
                        {
                            name = p.Name ?? "",
                            def = p.Def,
                            locked = p.Locked,
                            skin = p.Skin ?? "",
                            w = (float)p.Width,
                            h = (float)p.Height,
                            navi_loop = p.NaviLoop,
                            minval = p.MinVal,
                            maxval = p.MaxVal,
                            digit = p.Digit,
                            slide_cur_digit_only = p.SlideCurDigitOnly,
                        };
                        SetDelegateField(data, "fnClick", handler, p.OnClick);
                        designer.addNumCounterT<aBtnNumCounter>(data);
                        break;
                    }

                    case PuiWireOpcode.AddColorCell:
                    {
                        var p = (PuiColorCellParams)cmd.Payload;
                        var data = new DsnDataColorCell
                        {
                            name = p.Name ?? "",
                            def = p.DefColor.ToColor32(),
                            open_prompt = p.OpenPrompt,
                            use_text = p.UseText,
                            use_alpha = p.UseAlpha,
                            title = PuiText.Resolve(p.Title),
                            skin = p.Skin ?? "",
                            skin_title = p.SkinTitle ?? "",
                            w = (float)p.Width,
                            h = (float)p.Height,
                        };
                        SetDelegateField(data, "fnPromptDone", handler, p.OnColorPromptDone);
                        designer.addColorCell(data);
                        break;
                    }

                    case PuiWireOpcode.AddImage:
                    {
                        var p = (PuiImageParams)cmd.Payload;
                        var data = new DsnDataImg
                        {
                            name = p.Name ?? "",
                            swidth = (float)p.Width,
                            sheight = (float)p.Height,
                            stencil_lessequal = p.StencilLessEqual,
                        };

                        // UvRect / scale 不在初始化器里直接填：那两个字段的语义（像素矩形、
                        // 绘制尺寸与 swidth/sheight 无关）都要换算，统一交给 PuiImage.Assign，
                        // 和编译期生成的代码走同一份实现。
                        MImage image = null;
                        if (!string.IsNullOrEmpty(p.ImageResource))
                        {
                            image = ResolveImageField(p.ImageResource, handler);
                        }
                        else if (!string.IsNullOrEmpty(p.ImageSource))
                        {
                            image = ResolveImage(p.ImageSource, handler);
                        }

                        PuiImage.Assign(data, image,
                            (float)p.UvX, (float)p.UvY, (float)p.UvW, (float)p.UvH,
                            (float)p.Width, (float)p.Height, (float)p.Scale);

                        designer.addImg(data);
                        break;
                    }

                    case PuiWireOpcode.OnBuildCompleted:
                        InvokeOnBuildCompleted(handler, ((PuiMethodNameParams)cmd.Payload).MethodName, designer);
                        break;
                }
            }

            return designer;
        }

        private static UiBoxDesignerFamily.MASKTYPE ToMask(PuiMaskType mask) => mask switch
        {
            PuiMaskType.NoMask => UiBoxDesignerFamily.MASKTYPE.NO_MASK,
            PuiMaskType.Scroll => UiBoxDesignerFamily.MASKTYPE.SCROLL,
            _ => UiBoxDesignerFamily.MASKTYPE.BOX,
        };

        private static UiBox.FRAMETYPE ToFrameType(PuiFrameType frame) => frame switch
        {
            PuiFrameType.None => UiBox.FRAMETYPE.NONE,
            PuiFrameType.OneLine => UiBox.FRAMETYPE.ONELINE,
            PuiFrameType.Dark => UiBox.FRAMETYPE.DARK,
            PuiFrameType.DarkSimple => UiBox.FRAMETYPE.DARK_SIMPLE,
            PuiFrameType.NoOverride => UiBox.FRAMETYPE.NO_OVERRIDE,
            _ => UiBox.FRAMETYPE.MAIN,
        };

        private static ALIGN ToAlign(PuiTextAlign align) => align switch
        {
            PuiTextAlign.Center => ALIGN.CENTER,
            PuiTextAlign.Right => ALIGN.RIGHT,
            PuiTextAlign.Auto => ALIGN._AUTO,
            _ => ALIGN.LEFT,
        };

        private static ALIGN ToAlign(PuiLineAlign align) => align switch
        {
            PuiLineAlign.Center => ALIGN.CENTER,
            PuiLineAlign.Right => ALIGN.RIGHT,
            _ => ALIGN.LEFT,
        };

        /// <summary>
        /// 把 data 上名为 fieldName 的委托字段指向 handler 身上名为 methodName 的方法；
        /// 用反射读字段类型再 CreateDelegate，不需要在这里写死每个 hook 对应的具体委托类型名。
        /// methodName 为空表示这个 hook 未绑定，什么都不做。
        /// </summary>
        private static void SetDelegateField(object data, string fieldName, IPUI handler, string methodName)
        {
            if (string.IsNullOrEmpty(methodName))
            {
                return;
            }

            FieldInfo field = data.GetType().GetField(fieldName);
            if (field == null)
            {
                throw new InvalidOperationException($"{data.GetType().Name} has no field {fieldName}; cannot bind callback {methodName}");
            }

            Delegate del;
            try
            {
                del = Delegate.CreateDelegate(field.FieldType, handler, methodName);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Callback method {methodName} was not found, or its signature does not match {data.GetType().Name}.{fieldName}: {ex.Message}", ex);
            }

            field.SetValue(data, del);
        }

        /// <summary>
        /// 把 <c>.pui</c> 里的图片来源解析成 <c>MImage</c>，走资源子系统。
        /// modId 取 <paramref name="handler"/>（生成的 partial class 实例）所在程序集的名字，
        /// 和 <c>AutoBindScanner</c> 用 <c>assembly.GetName().Name</c> 当 modId 的约定一致。
        /// </summary>
        private static MImage ResolveImage(string imageSource, IPUI handler)
        {
            string modId = handler.GetType().Assembly.GetName().Name;
            return Polaris.Res.PolarisResAPI.For(modId).Own.Image(imageSource);
        }

        /// <summary>
        /// 把编辑器选中的资源字段引用（形如 <c>MyMod.Res.testImage</c>）解析成 <c>MImage</c>：
        /// 在 <paramref name="handler"/> 所在程序集里找到那个类型，反射读它的 static 字段。
        /// 编译期路径（<c>CSharpTextEmitter.AddImage</c>）生成的是同一个字段的直接引用，
        /// 两条路径拿到的是同一个由 <c>AutoBindScanner</c> 回填的 <c>MImage</c> 实例——热重载
        /// 不会重新挂载目录、也不会重复解码图片。
        /// <para>
        /// 解析不出来一律抛异常：调用方（<c>PUIHotReloadRuntime</c>）会在临时对象上捕获并回滚，
        /// 作者能立刻看到是哪个引用的问题，比静默画一个空图好。
        /// </para>
        /// </summary>
        private static MImage ResolveImageField(string reference, IPUI handler)
        {
            Assembly assembly = handler.GetType().Assembly;

            int lastDot = reference.LastIndexOf('.');
            if (lastDot <= 0 || lastDot == reference.Length - 1)
            {
                throw new InvalidOperationException(
                    $"Image resource reference \"{reference}\" is not a Type.field reference.");
            }

            string typeName = reference.Substring(0, lastDot);
            string fieldName = reference.Substring(lastDot + 1);

            Type type = ResolveNestedType(assembly, typeName);
            if (type == null)
            {
                throw new InvalidOperationException(
                    $"Image resource reference \"{reference}\": type {typeName} was not found in assembly {assembly.GetName().Name}.");
            }

            FieldInfo field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (field == null)
            {
                throw new InvalidOperationException(
                    $"Image resource reference \"{reference}\": {typeName} has no static field {fieldName}.");
            }

            if (!typeof(MImage).IsAssignableFrom(field.FieldType))
            {
                throw new InvalidOperationException(
                    $"Image resource reference \"{reference}\": field type is {field.FieldType.Name}, but DsnDataImg.MI requires MImage.");
            }

            var image = (MImage)field.GetValue(null);
            if (image == null)
            {
                throw new InvalidOperationException(
                    $"Image resource reference \"{reference}\" is still null. PolarisRes auto-binding fills it in at load time: " +
                    "check that the class has [PolarisResourceFolder], the field has [PolarisResource], and the image file is deployed next to the dll.");
            }

            return image;
        }

        /// <summary>
        /// 反射里嵌套类型用 <c>'+'</c> 分隔（<c>MyMod.Outer+Inner</c>），编辑器给的是 C# 源码写法的
        /// <c>'.'</c>，所以从右往左逐个把 <c>'.'</c> 换成 <c>'+'</c> 再试一次，直到找到或试完。
        /// </summary>
        private static Type ResolveNestedType(Assembly assembly, string typeName)
        {
            string candidate = typeName;
            while (true)
            {
                Type type = assembly.GetType(candidate, false);
                if (type != null)
                {
                    return type;
                }

                int dot = candidate.LastIndexOf('.');
                if (dot < 0)
                {
                    return null;
                }
                candidate = candidate.Substring(0, dot) + "+" + candidate.Substring(dot + 1);
            }
        }

        private static void InvokeOnBuildCompleted(IPUI handler, string methodName, UiBoxDesigner designer)
        {
            if (string.IsNullOrEmpty(methodName))
            {
                return;
            }

            MethodInfo method = handler.GetType().GetMethod(methodName, new[] { typeof(UiBoxDesigner) });
            if (method == null)
            {
                throw new InvalidOperationException($"Could not find the OnBuildCompleted method {methodName}(UiBoxDesigner designer)");
            }

            method.Invoke(handler, new object[] { designer });
        }
    }
}

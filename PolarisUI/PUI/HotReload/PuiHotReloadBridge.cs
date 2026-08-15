using System;
using System.Collections.Generic;
using System.Reflection;
using nel;
using Polaris.PUI.Wire;
using XX;

namespace Polaris.PUI.HotReload
{
    /// <summary>逐条执行 <see cref="PuiWireCommand"/>，直接调用对应 nel API；回调名通过反射绑定到 handler，解析失败则抛异常交调用方回滚。</summary>
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
                        designer = CreateWindow(family, (PuiCreateWindowParams)cmd.Payload);
                        break;

                    case PuiWireOpcode.SetFrameType:
                        designer.getBox().frametype = ToFrameType(((PuiFrameTypeParams)cmd.Payload).FrameType);
                        break;

                    case PuiWireOpcode.SetFocusable:
                        designer.Focusable();
                        break;

                    case PuiWireOpcode.Br:
                        designer.Br();
                        break;

                    case PuiWireOpcode.SetLineAlign:
                        designer.alignx = ToAlign(((PuiLineAlignParams)cmd.Payload).Align);
                        break;

                    case PuiWireOpcode.SetDefaultLineAlign:
                        designer.alignx = ALIGN.LEFT;
                        break;

                    case PuiWireOpcode.AddText:
                        AddText(designer, (PuiTextParams)cmd.Payload);
                        break;

                    case PuiWireOpcode.AddButton:
                        AddButton(designer, (PuiButtonParams)cmd.Payload, handler);
                        break;

                    case PuiWireOpcode.AddSeparator:
                        AddSeparator(designer, (PuiSeparatorParams)cmd.Payload);
                        break;

                    case PuiWireOpcode.AddButtonMulti:
                        AddButtonMulti(designer, (PuiButtonMultiParams)cmd.Payload, handler);
                        break;

                    case PuiWireOpcode.AddChecks:
                        AddChecks(designer, (PuiChecksParams)cmd.Payload, handler);
                        break;

                    case PuiWireOpcode.AddRadio:
                        AddRadio(designer, (PuiRadioParams)cmd.Payload, handler);
                        break;

                    case PuiWireOpcode.AddSlider:
                        AddSlider(designer, (PuiSliderParams)cmd.Payload, handler);
                        break;

                    case PuiWireOpcode.AddInput:
                        AddInput(designer, (PuiInputParams)cmd.Payload, handler);
                        break;

                    case PuiWireOpcode.AddNumCounter:
                        AddNumCounter(designer, (PuiNumCounterParams)cmd.Payload, handler);
                        break;

                    case PuiWireOpcode.AddColorCell:
                        AddColorCell(designer, (PuiColorCellParams)cmd.Payload, handler);
                        break;

                    case PuiWireOpcode.AddImage:
                        AddImage(designer, (PuiImageParams)cmd.Payload, handler);
                        break;

                    case PuiWireOpcode.OnBuildCompleted:
                        InvokeOnBuildCompleted(handler, ((PuiMethodNameParams)cmd.Payload).MethodName, designer);
                        break;
                }
            }

            return designer;
        }

        private static UiBoxDesigner CreateWindow(UiBoxDesignerFamily family, PuiCreateWindowParams p)
        {
            return family.Create(
                p.Name ?? "",
                (float)p.PixelX,
                (float)p.PixelY,
                (float)p.Width,
                (float)p.Height,
                p.AppearDir,
                (float)p.AppearLen,
                ToMask(p.Mask));
        }

        private static void AddText(UiBoxDesigner designer, PuiTextParams p)
        {
            designer.addP(new DsnDataP
            {
                name = p.Name ?? "",
                // 显示文本统一过 PuiText.Resolve，跟编译期路径的本地化规则保持一致。
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
            });
        }

        private static void AddButton(UiBoxDesigner designer, PuiButtonParams p, IPUI handler)
        {
            var data = new DsnDataButton { w = (float)p.Width, h = (float)p.Height };

            if (!string.IsNullOrEmpty(p.Name))
            {
                data.name = p.Name;
            }

            // 判空判原始串（跟编译期路径一致），非空才发 title。
            if (!string.IsNullOrEmpty(p.Title))
            {
                data.title = PuiText.Resolve(p.Title);
            }

            if (!string.IsNullOrEmpty(p.Skin))
            {
                data.skin = p.Skin;
            }

            if (string.IsNullOrEmpty(p.TransitionTriggerKey))
            {
                SetDelegateField(data, "fnClick", handler, p.OnClick);
            }
            else
            {
                // 同时是状态连接点触发点：包一层闭包，先调用原 OnClick 再触发 RaiseEvent。
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

            designer.addButton(data);
        }

        private static void AddSeparator(UiBoxDesigner designer, PuiSeparatorParams p)
        {
            designer.addHr(new DsnDataHr
            {
                swidth = (float)p.Width,
                vertical = p.Vertical,
                line_height = (float)p.LineHeight,
                margin_t = (float)p.MarginBefore,
                margin_b = (float)p.MarginAfter,
                dashed_oneline_lgt = (float)p.DashedLength,
                draw_width_rate = (float)p.DrawWidthRate,
                Col = p.Color.ToColor32(),
            });
        }

        private static void AddButtonMulti(UiBoxDesigner designer, PuiButtonMultiParams p, IPUI handler)
        {
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
        }

        private static void AddChecks(UiBoxDesigner designer, PuiChecksParams p, IPUI handler)
        {
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

            // descs 是显示文字过 Resolve；keys 是回调标识符，不解析。
            if (p.Descs != null)
            {
                data.descs = PuiText.ResolveAll(p.Descs);
            }

            SetDelegateField(data, "fnClick", handler, p.OnClick);
            designer.addChecks(data);
        }

        private static void AddRadio(UiBoxDesigner designer, PuiRadioParams p, IPUI handler)
        {
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

            // descs 是显示文字过 Resolve；keys 是回调标识符，不解析。
            if (p.Descs != null)
            {
                data.descs = PuiText.ResolveAll(p.Descs);
            }

            SetDelegateField(data, "fnClick", handler, p.OnClick);
            SetDelegateField(data, "fnChanged", handler, p.OnChanged);

            if (p.RowMode)
            {
                data = data.RowMode(p.Skin ?? "");
            }

            designer.addRadio(data);
        }

        private static void AddSlider(UiBoxDesigner designer, PuiSliderParams p, IPUI handler)
        {
            var data = new DsnDataSlider
            {
                name = p.Name ?? "",
                // title 是显示用标题，过 Resolve；Adesc_keys 是标识符，不解析。
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
        }

        private static void AddInput(UiBoxDesigner designer, PuiInputParams p, IPUI handler)
        {
            var data = new DsnDataInput
            {
                name = p.Name ?? "",
                // def 是初始值数据，不解析；label 才是显示文字。
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
        }

        private static void AddNumCounter(UiBoxDesigner designer, PuiNumCounterParams p, IPUI handler)
        {
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
        }

        private static void AddColorCell(UiBoxDesigner designer, PuiColorCellParams p, IPUI handler)
        {
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
        }

        private static void AddImage(UiBoxDesigner designer, PuiImageParams p, IPUI handler)
        {
            var data = new DsnDataImg
            {
                name = p.Name ?? "",
                swidth = (float)p.Width,
                sheight = (float)p.Height,
                stencil_lessequal = p.StencilLessEqual,
            };

            MImage image = null;
            if (!string.IsNullOrEmpty(p.ImageResource))
            {
                image = ResolveImageField(p.ImageResource, handler);
            }
            else if (!string.IsNullOrEmpty(p.ImageSource))
            {
                image = ResolveImage(p.ImageSource, handler);
            }

            // UvRect/scale 需要换算，统一交给 PuiImage.Assign（与编译期同一份实现）。
            PuiImage.Assign(data, image,
                (float)p.UvX, (float)p.UvY, (float)p.UvW, (float)p.UvH,
                (float)p.Width, (float)p.Height, (float)p.Scale);

            designer.addImg(data);
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

        /// <summary>把 data 上 fieldName 委托字段绑定到 handler 的 methodName 方法；methodName 为空则不做任何事。</summary>
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

        /// <summary>把 <c>.pui</c> 里的图片来源解析成 <c>MImage</c>；modId 取 handler 所在程序集名。</summary>
        private static MImage ResolveImage(string imageSource, IPUI handler)
        {
            string modId = handler.GetType().Assembly.GetName().Name;
            return Polaris.Res.PolarisResAPI.For(modId).Own.Image(imageSource);
        }

        /// <summary>把资源字段引用（如 <c>MyMod.Res.testImage</c>）反射解析成 <c>MImage</c>；解析失败一律抛异常，交调用方回滚。</summary>
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

        /// <summary>反射嵌套类型用 <c>'+'</c> 分隔而编辑器给的是 <c>'.'</c>，故从右往左尝试替换直到找到。</summary>
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

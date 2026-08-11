using System;

namespace Polaris.Diagnostics
{
    /// <summary>
    /// 按异常"形状"给出诊断。有些异常自己就带着完整的故事，比归因结论更有用——
    /// 一个 <c>MissingMethodException</c> 基本就等于"某个模组是照着另一个版本的游戏编译的"，
    /// 这句话对玩家的价值远超过"堆栈第三帧属于某模组"。
    /// <para>
    /// 判定只看异常类型名（字符串），不看 <see cref="Exception"/> 实例：全局兜底抓到的异常
    /// 大多只有 Unity 给的文本，没有对象。两条来路必须得出同样的诊断。
    /// </para>
    /// </summary>
    internal static class ExceptionShapes
    {
        internal sealed class Shape
        {
            internal string Diagnosis { get; set; }
            internal string Action { get; set; }
        }

        /// <summary>没有特别形状时返回 null——不硬凑诊断，说不出所以然就别说。</summary>
        internal static Shape Of(string exceptionTypeName, string message)
        {
            if (string.IsNullOrEmpty(exceptionTypeName))
            {
                return null;
            }

            string name = Simplify(exceptionTypeName);

            switch (name)
            {
                case "MissingMethodException":
                case "MissingFieldException":
                case "MissingMemberException":
                case "TypeLoadException":
                    return new Shape
                    {
                        Diagnosis = "版本不匹配：有代码在找一个此刻并不存在的方法/字段/类型。"
                                    + "通常是某个模组照着另一个版本的游戏（或另一个版本的 Polaris）编译的。",
                        Action = "确认游戏版本与该模组要求的版本一致；优先更新模组，其次考虑回退游戏版本。",
                    };

                case "ReflectionTypeLoadException":
                    return new Shape
                    {
                        Diagnosis = "某个程序集里有类型加载不了，通常是它依赖的 dll 没装，或版本对不上。",
                        Action = "看下面异常消息里点名的程序集，把缺的依赖补齐。",
                    };

                case "FileNotFoundException":
                    return LooksLikeAssembly(message)
                        ? new Shape
                        {
                            Diagnosis = "缺依赖：有模组要加载一个不存在的程序集。",
                            Action = "按该模组的说明补装它依赖的 dll（Polaris 系列的依赖应放在 plugins/Polaris/libs/）。",
                        }
                        : null;

                case "BadImageFormatException":
                    return new Shape
                    {
                        Diagnosis = "有 dll 读不动：文件损坏、下载不完整，或是给别的运行时/位数编译的。",
                        Action = "重新下载该 dll；确认它是给 BepInEx 6（Mono）用的版本。",
                    };

                case "AmbiguousMatchException":
                    return new Shape
                    {
                        Diagnosis = "反射/Harmony 匹配到了多个同名成员却没指定参数类型。"
                                    + "游戏更新新增一个重载就会突然触发——Polaris 自己在 TX.Get 上栽过一次。",
                        Action = "报给该模组作者，附上下面的方法名；这是模组侧一行代码就能修的问题。",
                    };

                case "InvalidCastException":
                    return SameTypeOnBothSides(message)
                        ? new Shape
                        {
                            Diagnosis = "同一个类型被加载了两遍：多半是同一个依赖 dll 在 plugins 下存在多份拷贝。",
                            Action = "在 plugins 目录里搜一下这个 dll，只保留一份（版本最高的那份）。",
                        }
                        : null;

                case "OutOfMemoryException":
                    return new Shape
                    {
                        Diagnosis = "内存耗尽。装了大量高清素材类模组时最常见。",
                        Action = "减少同时启用的资源类模组；确认游戏跑在 64 位下。",
                    };

                case "DllNotFoundException":
                    return new Shape
                    {
                        Diagnosis = "缺原生库（非托管 dll）。",
                        Action = "按该模组说明补装它的原生依赖，并确认没被杀毒软件拦掉。",
                    };

                default:
                    return null;
            }
        }

        /// <summary>去掉命名空间与泛型标记，只留类名。</summary>
        static string Simplify(string typeName)
        {
            int dot = typeName.LastIndexOf('.');
            return dot >= 0 && dot < typeName.Length - 1 ? typeName.Substring(dot + 1) : typeName;
        }

        /// <summary>
        /// <c>FileNotFoundException</c> 也可能只是模组读不到自己的 png。消息里带程序集全名的
        /// 特征（<c>Culture=</c>/<c>PublicKeyToken=</c>/<c>.dll</c>）才当成缺依赖。
        /// </summary>
        static bool LooksLikeAssembly(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return false;
            }

            return message.IndexOf("Culture=", StringComparison.OrdinalIgnoreCase) >= 0
                   || message.IndexOf("PublicKeyToken=", StringComparison.OrdinalIgnoreCase) >= 0
                   || message.IndexOf("Could not load file or assembly", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// <c>InvalidCastException</c> 的消息形如 "Unable to cast object of type 'X' to type 'X'"。
        /// 两边同名就是典型的"一个类型被两个程序集各加载了一份"。
        /// </summary>
        static bool SameTypeOnBothSides(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return false;
            }

            int first = message.IndexOf('\'');
            if (first < 0)
            {
                return false;
            }

            int firstEnd = message.IndexOf('\'', first + 1);
            if (firstEnd < 0)
            {
                return false;
            }

            int second = message.IndexOf('\'', firstEnd + 1);
            if (second < 0)
            {
                return false;
            }

            int secondEnd = message.IndexOf('\'', second + 1);
            if (secondEnd < 0)
            {
                return false;
            }

            string left = message.Substring(first + 1, firstEnd - first - 1);
            string right = message.Substring(second + 1, secondEnd - second - 1);
            return left.Length > 0 && string.Equals(left, right, StringComparison.Ordinal);
        }
    }
}

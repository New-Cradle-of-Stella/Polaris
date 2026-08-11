using System;
using System.Collections.Generic;

namespace Polaris.Localization
{
    /// <summary>
    /// 本地化 resolver 注册表 + 一张内置文案表，从 <see cref="PolarisAPI.Localization"/> 取。
    /// <para>
    /// 对模组作者来说这里有两件事：
    /// </para>
    /// <list type="bullet">
    /// <item><see cref="RegisterResolver"/>——注册一个查询回调。谁来管哪些 key、按什么规则取哪个
    /// 语言的文案，都是 resolver 自己的事：这一层不关心 <c>.plang</c> 之类的具体文件格式，
    /// 只负责把 <see cref="Patch.Patch_TX_Get"/> 拦下来的 key 依次问一遍已注册的 resolver。</item>
    /// <item><see cref="Register(string, LocalizedText)"/>——直接往内置表里塞一条写死在代码里的
    /// 多语言文案。给的是<b>不方便用 <c>.plang</c> 的场合</b>：Polaris 自己的设置项文案就在
    /// <c>.plang</c> 运行时起来之前就要能查到，却一样要跟着玩家的语言走。</item>
    /// </list>
    /// <para>
    /// resolver 未命中要返回 <c>null</c>（不是空串）：空串会被当成"这个 key 确实是空文案"
    /// 直接采纳，导致后面的 resolver 和原版查表都不会再被问到。
    /// </para>
    /// </summary>
    public sealed class LocalizationAPI
    {
        internal LocalizationAPI() { }

        readonly List<Func<string, string>> resolvers = [];

        /// <summary>内置表。<see cref="Resolve"/> 先问它再问 resolver 链，理由见那里。</summary>
        readonly Dictionary<string, LocalizedText> builtin = new(StringComparer.Ordinal);

        /// <summary>注册一个 resolver；按注册顺序依次尝试，第一个返回非 null 的结果生效。</summary>
        public void RegisterResolver(Func<string, string> resolver)
        {
            if (resolver != null)
            {
                resolvers.Add(resolver);
            }
        }

        /// <summary>
        /// 往内置表里登记一条文案。同一个 key 重复登记时后者覆盖前者并记一行警告——
        /// 内置表里的 key 都是写死在代码里的，撞名一定是笔误。
        /// <para>
        /// 登记时机没有限制，但要早于第一次显示：设置界面的标签是在
        /// <c>UiCFG</c> 构造时求值的，模组在自己的 <c>Awake</c> 里登记就来得及。
        /// </para>
        /// </summary>
        public void Register(string key, LocalizedText text)
        {
            if (string.IsNullOrEmpty(key) || text == null)
            {
                return;
            }

            if (builtin.ContainsKey(key))
            {
                // ?. 而不是直接调：下游模组在自己的 Awake 里登记，而 BepInEx 不保证
                // Polaris 自己的 Awake（Logger 在那里赋值）一定跑在最前面。
                Plugin.Logger?.LogWarning($"[Polaris] Built-in text key \"{key}\" was registered more than once; the later one wins.");
            }

            builtin[key] = text;
        }

        /// <summary>
        /// 把一个"显示用字符串"变成真正要显示的文案：<c>&amp;</c> 开头当本地化键查表，
        /// <c>&amp;&amp;</c> 开头脱一层转义，其余原样返回（判定见 <see cref="LocalizedString"/>）。
        /// <para>
        /// 查表顺序：内置表 / resolver 链（<see cref="Resolve"/>）→ 原版 <c>TX.Get</c> →
        /// <b>key 本身</b>。最后那一档是刻意的：<c>TX.Get</c> 未命中是静默返回空串的，直接采纳
        /// 就意味着界面上画出一片空白，玩家和模组作者都看不出发生了什么；把 key 显示出来，
        /// 至少一眼能看出是"这条文案没登记"而不是"这一行坏了"。
        /// </para>
        /// <para>
        /// <paramref name="raw"/> 为 null 时返回 null（不是空串）——调用方多半在用
        /// <c>IsNullOrEmpty</c> 判断"要不要显示这一块"，把 null 变成空串不会改变结论，
        /// 但把 null 变成别的什么就会。
        /// </para>
        /// </summary>
        public string Text(string raw)
        {
            if (raw == null)
            {
                return null;
            }

            if (!LocalizedString.TryGetKey(raw, out string key))
            {
                return LocalizedString.Unescape(raw);
            }

            // 非 null 就采纳，空串也算：resolver 的契约是"未命中返回 null"，回了空串就是
            // 作者真的想要一段空文案。下面 TX.Get 的空串则相反——原版未命中就是静默返回空串，
            // 分不出"空文案"和"没这条"，所以那一档必须把空串当未命中处理。
            string resolved = Resolve(key);
            if (resolved != null)
            {
                return resolved;
            }

            // 走一遍原版查表，让模组可以直接引用游戏自带的 key。TX 在极早期（family 表还没建好）
            // 读取可能抛异常，那不该让一行标签把整个设置界面掀掉。
            try
            {
                string vanilla = XX.TX.Get(key);
                if (!string.IsNullOrEmpty(vanilla))
                {
                    return vanilla;
                }
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[Polaris] The vanilla lookup threw while querying localization key \"{key}\": {e.Message}");
            }

            return key;
        }

        /// <summary><see cref="Text"/> 的数组版（选项文案之类）。null 进 null 出，返回新数组。</summary>
        public string[] TextAll(string[] raw)
        {
            if (raw == null)
            {
                return null;
            }

            var result = new string[raw.Length];
            for (int i = 0; i < raw.Length; i++)
            {
                result[i] = Text(raw[i]);
            }

            return result;
        }

        /// <summary>
        /// 供 <see cref="Patch.Patch_TX_Get"/> 调用；全部未命中返回 null。
        /// <para>
        /// 内置表排在 resolver 链前面：表里装的是 Polaris 自己各模块的文案，它们的 key 都带
        /// <c>polaris</c> 前缀，本来就不该被别的模组顶掉；而且这条查询在启动极早期（还没有任何
        /// resolver 注册）就要能答得出来。
        /// </para>
        /// </summary>
        internal string Resolve(string key)
        {
            if (key != null && builtin.TryGetValue(key, out LocalizedText text))
            {
                return text.Pick(CurrentLocale);
            }

            foreach (Func<string, string> resolver in resolvers)
            {
                string value;
                try
                {
                    value = resolver(key);
                }
                catch (Exception ex)
                {
                    // Patch_TX_Get 是 Prefix，跑在原版 TX.Get 之前：这里的异常一旦不接住会
                    // 直接从 Harmony 补丁里飞出去，连游戏自己的文案查询（含原版本身的 TX.Get
                    // 调用）一起打断。一个 Mod 的 resolver 写坏，不该连累其它 resolver 和原版查表，
                    // 按"未命中"处理，跳到下一个 resolver 继续。
                    // 责任人就是这个 resolver 委托本身所在的程序集，不必走堆栈推断。
                    PolarisAPI.Errors.Report(ex, $"a localization resolver handling \"{key}\"", resolver.Method?.DeclaringType?.Assembly);
                    Plugin.Logger.LogError($"[Polaris] A localization resolver threw while handling \"{key}\"; skipped.");
                    continue;
                }

                if (value != null)
                {
                    return value;
                }
            }

            return null;
        }

        /// <summary>
        /// 当前语言族。<see cref="Resolve"/> 会在启动极早期被问到（设置项绑定配置文件时要拿
        /// 说明文字去写 <c>.cfg</c> 注释），那时 <c>TX</c> 的 family 表可能还没建好，
        /// 读取会抛异常——按"未知语言"处理，内置文案退回中性值，与 <c>NoticeLocale</c> 一致。
        /// </summary>
        static string CurrentLocale
        {
            get
            {
                try
                {
                    return PolarisAPI.Game.CurrentLocale;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }
    }
}

using System;

namespace Polaris
{
    /// <summary>标题告知页内置文案的三种语言。</summary>
    internal enum NoticeLanguage
    {
        English,
        Chinese,
        Japanese,
    }

    /// <summary>
    /// 把 <see cref="PolarisAPI.Game.Localization.CurrentLocale"/> 归到内置文案的三种语言之一。
    /// <para>
    /// 这几页告知一律内置 zh/ja/en 三份文案、未识别的语言退回英文，不走 <c>.plang</c>：
    /// 它们是 Polaris 自己的界面，而且<b>致命错误页恰恰要在"某个模组的 <c>.plang</c> 撞了
    /// key"时还能显示</b>——那时正是本地化机制自己出了问题。判定规则收在这里而不是各页各写
    /// 一遍：<see cref="PolarisErrorNotice"/> 与 <see cref="PolarisFatalNotice"/> 对
    /// "玩家现在算哪种语言"必须给出同一个答案。
    /// </para>
    /// </summary>
    internal static class NoticeLocale
    {
        internal static NoticeLanguage Current
        {
            get
            {
                string locale = SafeLocale();

                if (locale != null && locale.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                {
                    return NoticeLanguage.Chinese;
                }

                // "_" 是游戏默认语言（日文）；ja/jp 之类的显式命名同样按日文处理。
                if (locale == "_" || (locale != null && locale.StartsWith("ja", StringComparison.OrdinalIgnoreCase)))
                {
                    return NoticeLanguage.Japanese;
                }

                return NoticeLanguage.English;
            }
        }

        /// <summary>
        /// 极早期（TX 的 family 表还没建好）读取语言可能抛异常，也可能拿到空值。
        /// 告知页不该因为读不到语言就建不出来，一律按"未识别"处理、退回英文。
        /// </summary>
        static string SafeLocale()
        {
            try
            {
                return PolarisAPI.Game.Localization.CurrentLocale;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

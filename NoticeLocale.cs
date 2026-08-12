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

    /// <summary>把 <see cref="PolarisAPI.Game.Localization.CurrentLocale"/> 归到内置 zh/ja/en 三种语言之一（不走 .plang，因为致命错误页恰恰要在本地化机制自身出问题时也能显示）。</summary>
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

        /// <summary>极早期读取语言可能抛异常或拿到空值；一律按"未识别"处理、退回英文，不能因此建不出告知页。</summary>
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

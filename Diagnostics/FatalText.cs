namespace Polaris.Diagnostics
{
    /// <summary>
    /// 同时供玩家（告知页）与模组作者（报告文件）看的文案，三语各一份；语言选择推迟到实际显示时。
    /// <see cref="English"/> 必填，作为未识别语言的兜底。
    /// </summary>
    public sealed class FatalText
    {
        /// <param name="english">英文，必填。</param>
        /// <param name="chinese">中文；省略时退回 <paramref name="english"/>。</param>
        /// <param name="japanese">日文；省略时退回 <paramref name="english"/>。</param>
        public FatalText(string english, string chinese = null, string japanese = null)
        {
            English = english ?? "";
            Chinese = chinese;
            Japanese = japanese;
        }

        public string English { get; }

        /// <summary>中文；没给为 null。</summary>
        public string Chinese { get; }

        /// <summary>日文；没给为 null。</summary>
        public string Japanese { get; }

        /// <summary>按语言取文案，缺哪一份就退回英文。</summary>
        internal string Pick(NoticeLanguage language)
        {
            switch (language)
            {
                case NoticeLanguage.Chinese: return Chinese ?? English;
                case NoticeLanguage.Japanese: return Japanese ?? English;
                default: return English;
            }
        }

        /// <summary>报告文件里用的那一份，跟随报告正文走英文。</summary>
        internal string ForReport => English;

        public override string ToString() => English;
    }
}

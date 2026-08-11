namespace Polaris.Diagnostics
{
    /// <summary>
    /// 一段要同时给玩家（标题画面的致命错误页）和模组作者（报告文件）看的文案，三语各一份。
    /// <para>
    /// 之所以由调用方交出三份、而不是交一个已经选好语言的字符串：
    /// <see cref="Infra.ErrorsAPI.Fatal"/> 的调用点在模块初始化阶段，那时游戏的语言表
    /// （<c>TX</c> 的 family）往往还没建好，选语言必须推迟到标题画面真的要显示这一页的时候。
    /// </para>
    /// <para>
    /// <see cref="English"/> 是必填的兜底项：Polaris 的告知页对未识别的语言一律退回英文
    /// （见 <see cref="NoticeLocale"/>），少一份中文或日文只是少一份，少了英文就没得显示了。
    /// </para>
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

        /// <summary>
        /// 报告文件里用的那一份。报告正文通篇是英文（见 <see cref="ErrorReportWriter"/>），
        /// 这一段跟着走英文。
        /// </summary>
        internal string ForReport => English;

        public override string ToString() => English;
    }
}

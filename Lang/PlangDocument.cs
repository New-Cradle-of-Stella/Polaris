using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace Polaris.Lang
{
    /// <summary>一个 <c>.plang</c> 支持的语言：代码、给编辑器看的显示名，以及是否启用。</summary>
    public sealed class PlangLanguage
    {
        /// <summary>语言代码，建议跟 <c>PolarisAPI.Game.CurrentLocale</c>（<c>"zh-cn"</c>/<c>"en"</c>/<c>"ko-kr"</c>...）对齐，<see cref="PlangRuntime"/> 按这个匹配当前游戏语言。</summary>
        public string Code { get; set; }

        /// <summary>编辑器里展示用的名字（如"简体中文"），不参与运行时匹配。</summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 是否启用：启用的语言才会在编辑器表格里出现一列、才会被生成代码/注册进
        /// <see cref="PlangRuntime"/>。关闭不会丢数据，只是隐藏+跳过生成，重新打开能找回来。
        /// </summary>
        public bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// 一个 <c>.plang</c> 文件的内存表示 + 读写，schema（Version 2，多语言）：
    /// <code>
    /// &lt;PolarisLang Version="2"&gt;
    ///   &lt;Languages&gt;
    ///     &lt;Language Code="zh-cn" Name="简体中文" Enabled="true" /&gt;
    ///     &lt;Language Code="en" Name="English" Enabled="true" /&gt;
    ///   &lt;/Languages&gt;
    ///   &lt;Entry Key="mymod.btn_ok" Comment="标题界面继续按钮"&gt;
    ///     &lt;Neutral&gt;&lt;![CDATA[确定]]&gt;&lt;/Neutral&gt;
    ///     &lt;Value Lang="zh-cn"&gt;&lt;![CDATA[确定]]&gt;&lt;/Value&gt;
    ///     &lt;Value Lang="en"&gt;&lt;![CDATA[OK]]&gt;&lt;/Value&gt;
    ///   &lt;/Entry&gt;
    /// &lt;/PolarisLang&gt;
    /// </code>
    /// <para>
    /// 向后兼容 Version 1（没有 <c>Languages</c>、<c>Entry</c> 直接用 <c>Value=""</c> 属性/纯
    /// CDATA 子节点）：<see cref="Parse"/> 遇到旧格式时把唯一的那份文案读进
    /// <see cref="PlangEntry.NeutralValue"/>，<see cref="PlangEntry.Values"/> 留空，不需要手动
    /// 迁移旧文件。<see cref="ToXmlString"/> 一律按 Version 2 写。
    /// </para>
    /// <para>
    /// 文案没有短/长之分，一律按可换行的长文本存成 CDATA 子节点。旧文件里的 <c>Type</c> 属性
    /// 读的时候只用来判断 v1 的那份文案是在属性上还是在子节点里（v1 短文本写 <c>Value=""</c>
    /// 属性），写的时候不再产出，v2 文件里出现也一律忽略。
    /// </para>
    /// <para>
    /// 这份模型同时被 PolarisTool（PolarisSourceCodeGenerator 项目）的编辑器/生成器以源文件
    /// 链接（Link）方式复用，不重复实现一遍读写逻辑。
    /// </para>
    /// </summary>
    public sealed class PlangDocument
    {
        public const int CurrentVersion = 2;

        public List<PlangLanguage> Languages { get; } = new();

        public List<PlangEntry> Entries { get; } = new();

        public static PlangDocument Load(string path)
        {
            return Parse(File.ReadAllText(path));
        }

        public static PlangDocument Parse(string xml)
        {
            var doc = new PlangDocument();
            if (string.IsNullOrWhiteSpace(xml))
            {
                return doc;
            }

            XElement root = XElement.Parse(xml);
            int version = (int?)root.Attribute("Version") ?? 1;

            if (version >= 2)
            {
                ParseLanguages(root, doc);
                foreach (XElement el in root.Elements("Entry"))
                {
                    ParseEntryV2(el, doc);
                }
            }
            else
            {
                foreach (XElement el in root.Elements("Entry"))
                {
                    ParseEntryV1(el, doc);
                }
            }

            return doc;
        }

        static void ParseLanguages(XElement root, PlangDocument doc)
        {
            XElement languagesEl = root.Element("Languages");
            if (languagesEl == null)
            {
                return;
            }

            foreach (XElement el in languagesEl.Elements("Language"))
            {
                string code = (string)el.Attribute("Code");
                if (string.IsNullOrEmpty(code))
                {
                    continue;
                }

                doc.Languages.Add(new PlangLanguage
                {
                    Code = code,
                    DisplayName = (string)el.Attribute("Name") ?? code,
                    Enabled = (bool?)el.Attribute("Enabled") ?? true,
                });
            }
        }

        static void ParseEntryV2(XElement el, PlangDocument doc)
        {
            string key = (string)el.Attribute("Key");
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            var entry = new PlangEntry(key, el.Element("Neutral")?.Value ?? "", (string)el.Attribute("Comment"));

            foreach (XElement valueEl in el.Elements("Value"))
            {
                string lang = (string)valueEl.Attribute("Lang");
                if (string.IsNullOrEmpty(lang))
                {
                    continue;
                }

                entry.Values[lang] = valueEl.Value ?? "";
            }

            doc.Entries.Add(entry);
        }

        // Version 1（旧格式）：Type="Short"（默认）的文案存在 Value 属性里；Type="Long" 没有
        // Value 属性，走子节点（CDATA 或纯文本），XElement.Value 会把子节点的文本内容拼起来、
        // 不含标签本身。Type 在这里只用来决定去哪儿取值，不再进内存模型。
        static void ParseEntryV1(XElement el, PlangDocument doc)
        {
            string key = (string)el.Attribute("Key");
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            bool isLong = (string)el.Attribute("Type") == "Long";
            string value = isLong ? el.Value : (string)el.Attribute("Value") ?? "";

            doc.Entries.Add(new PlangEntry(key, value, (string)el.Attribute("Comment")));
        }

        public string ToXmlString()
        {
            var root = new XElement("PolarisLang", new XAttribute("Version", CurrentVersion));

            if (Languages.Count > 0)
            {
                var languagesEl = new XElement("Languages");
                foreach (PlangLanguage lang in Languages)
                {
                    languagesEl.Add(new XElement("Language",
                        new XAttribute("Code", lang.Code ?? ""),
                        new XAttribute("Name", lang.DisplayName ?? lang.Code ?? ""),
                        new XAttribute("Enabled", lang.Enabled)));
                }
                root.Add(languagesEl);
            }

            foreach (PlangEntry entry in Entries)
            {
                var el = new XElement("Entry", new XAttribute("Key", entry.Key ?? ""));

                if (!string.IsNullOrEmpty(entry.Comment))
                {
                    el.Add(new XAttribute("Comment", entry.Comment));
                }

                el.Add(new XElement("Neutral", new XCData(entry.NeutralValue ?? "")));

                foreach (KeyValuePair<string, string> kv in entry.Values)
                {
                    el.Add(new XElement("Value", new XAttribute("Lang", kv.Key), new XCData(kv.Value ?? "")));
                }

                root.Add(el);
            }

            return new XDocument(root).ToString();
        }

        public void Save(string path)
        {
            File.WriteAllText(path, ToXmlString());
        }
    }
}

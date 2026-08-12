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

        /// <summary>是否启用：只有启用的语言会出现在编辑器表格并被生成/注册；关闭只是隐藏+跳过生成，不丢数据。</summary>
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
    /// 向后兼容 Version 1（旧格式读入 <see cref="PlangEntry.NeutralValue"/>，写出一律按 v2）；
    /// 文案统一存成 CDATA 子节点，不再区分短/长。此模型同时被 PolarisTool 编辑器/生成器以源文件链接复用。
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

        // Version 1：Type="Short"（默认）的文案在 Value 属性里，Type="Long" 走子节点；Type 只决定取值位置，不进内存模型。
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

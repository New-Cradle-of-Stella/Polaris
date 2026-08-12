using System.Collections.Generic;

namespace Polaris.Event.Compiler.Aliases
{
    /// <summary>
    /// 别名 YAML 的内存模型，字段对照《哈++资源别名配置示例-v2.0.yaml》。本轮编译器只消费
    /// <see cref="Actors"/>/<see cref="Positions"/>/<see cref="BoxStyles"/>/<see cref="Audio"/>/
    /// <see cref="Events"/>（<c>BoxStyles</c> 是 @char 输出 HKDS 第四个 token 必需的最小追加，见实现计划
    /// 落地时的说明）；<c>emotes/voices/colors/curves/effects/...</c> 等域暂不建模，交给
    /// <c>IgnoreUnmatchedProperties</c> 静默跳过，避免真实项目的完整别名文件解析失败。
    /// <para>
    /// 别名作用域简化为单一项目级文件，不做设计文档 §5.3 的项目/目录/文件三级覆盖。
    /// </para>
    /// </summary>
    public sealed class AliasDocument
    {
        public AliasSettings Settings { get; set; } = new AliasSettings();
        public Dictionary<string, ActorAlias> Actors { get; set; } = new Dictionary<string, ActorAlias>();
        public Dictionary<string, PositionAlias> Positions { get; set; } = new Dictionary<string, PositionAlias>();
        public Dictionary<string, string> BoxStyles { get; set; } = new Dictionary<string, string>();
        public AudioAliasGroup Audio { get; set; } = new AudioAliasGroup();
        public Dictionary<string, string> Events { get; set; } = new Dictionary<string, string>();
    }

    public sealed class AliasSettings
    {
        public string TextMode { get; set; } = "heredoc";
        public string DefaultPosition { get; set; } = "center";

        /// <summary>
        /// 尚未接入：本轮编译器的别名查找一律大小写敏感。真实项目若依赖大小写不敏感匹配，
        /// 这里解析出来的值目前不会生效，属已知简化，留给后续阶段。
        /// </summary>
        public bool CaseInsensitive { get; set; } = true;
    }

    public sealed class ActorAlias
    {
        public string Raw { get; set; }
        public string Display { get; set; }
        public string DefaultPos { get; set; }
        public string Box { get; set; }
        public Dictionary<string, string> Poses { get; set; } = new Dictionary<string, string>();
    }

    public sealed class PositionAlias
    {
        public string Talker { get; set; }
        public string BoxPos { get; set; }
        public string From { get; set; }
    }

    public sealed class AudioAliasGroup
    {
        public Dictionary<string, string> Sfx { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> Bgm { get; set; } = new Dictionary<string, string>();
    }
}

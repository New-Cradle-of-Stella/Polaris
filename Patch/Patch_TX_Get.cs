using HarmonyLib;
using XX;

namespace Polaris.Patch
{
    /// <summary>
    /// 让通过 <see cref="PolarisAPI.Localization"/> 注册的本地化 key 能够直接从游戏原生
    /// <c>TX.Get</c> 管线里查到文案，不需要模组或游戏原生 UI 代码改用别的查询入口。
    /// <para>
    /// <c>XX.TX</c> 上有<b>两个</b>叫 <c>Get</c> 的方法：
    /// <c>public static string Get(string title, string default_str = "")</c> 与无参的
    /// <c>public static string Get()</c>。所以 <see cref="HarmonyPatch"/> 必须显式给出参数
    /// 类型，否则 <c>PatchAll</c> 会抛 <c>AmbiguousMatchException</c>——而那会中断
    /// <c>Plugin.Awake</c>，Unity 随后跳过 <c>Start</c>，那里的子系统初始化就一个都不会执行。
    /// （这个坑真踩过：早先的注释断言"只有一个重载"，是错的。）
    /// </para>
    /// <para>
    /// 带参的那个未命中原版表时静默返回 <c>default_str</c>（缺省空串），不抛异常、不报错——
    /// 这里的 Prefix 抢在原版查表（<c>getTX</c>，内部按当前语言→英语→默认语言依次回退）之前，
    /// 先问一遍 <see cref="PolarisAPI.Localization"/> 注册的 resolver：命中就把结果写进
    /// <c>__result</c> 并跳过原版（<c>return false</c>）；没有任何 resolver 认这个 key
    /// （返回 null）就放行走原版逻辑（<c>return true</c>），完全不影响游戏自身文案的查找/回退。
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(TX), nameof(TX.Get), new[] { typeof(string), typeof(string) })]
    internal static class Patch_TX_Get
    {
        static bool Prefix(string title, ref string __result)
        {
            string resolved = PolarisAPI.Localization.Resolve(title);
            if (resolved == null)
            {
                return true;
            }

            __result = resolved;
            return false;
        }
    }
}

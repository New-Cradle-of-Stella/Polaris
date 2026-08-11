using System;
using System.Collections.Generic;

namespace Polaris.API
{
    /// <summary>
    /// 一条可以被"权限化"的游戏能力。上层（节点图 Profile、Lua 兼容层、内容模组）用它来声明
    /// "我要用哪些能力"，Polaris 用它来回答"本局游戏版本上这条能力到底通不通"。
    /// <para>
    /// 枚举值只增不改：它会被写进内容定义与图文件，改名或复用数值会让旧内容的权限声明错位。
    /// </para>
    /// </summary>
    public enum GameCapability
    {
        LoopQuery,
        LoopPause,

        InputAction,
        InputRaw,

        WorldQuery,
        WorldDanger,
        WorldWeather,
        WorldTransition,

        CharacterQuery,
        CharacterMove,
        CharacterPose,

        PlayerQuery,
        PlayerActions,

        InventoryQuery,
        InventoryChange,
        InventoryUse,

        EconomyQuery,
        EconomyChange,

        CombatRecover,
        CombatDamage,

        MagicQuery,
        MagicCast,

        AudioQuery,
        AudioPlay,
    }

    /// <summary>某条能力在本局的可用状态。</summary>
    public enum CapabilityAvailability
    {
        /// <summary>已核对当前游戏版本的入口，可用。</summary>
        Available,

        /// <summary>入口存在但只读/只有一半（例如天气能查不能改）。</summary>
        QueryOnly,

        /// <summary>本版本没有找到可靠入口，相关方法一律返回 <see cref="GameActionStatus.UnsupportedInCurrentVersion"/>。</summary>
        Unsupported,
    }

    /// <summary>
    /// 能力兼容表。这是"游戏更新了先失败、再逐条恢复"这条策略的落点：换游戏版本时，
    /// 上层不需要各自去 try/catch 试探，读这张表就知道哪些路今天走得通。
    /// <para>
    /// 表里的状态是<b>静态声明</b>，来自对 0.29j 反编译结果的核对，不做运行时探测——运行时探测
    /// 要么得反射（正是本层想消灭的东西），要么得真的调一次（有副作用）。入口一旦在新版本消失，
    /// 调用会走到 catch 并由 <c>PolarisAPI.Errors</c> 归因，同时这里的声明需要跟着更新。
    /// </para>
    /// </summary>
    public static class GameCapabilities
    {
        static readonly Dictionary<GameCapability, CapabilityAvailability> Table =
            new Dictionary<GameCapability, CapabilityAvailability>
            {
                // ── 已核对入口、可用 ────────────────────────────────────────────────
                { GameCapability.LoopQuery,        CapabilityAvailability.Available },
                { GameCapability.InputAction,      CapabilityAvailability.Available },
                { GameCapability.WorldQuery,       CapabilityAvailability.Available },
                { GameCapability.WorldDanger,      CapabilityAvailability.Available },
                { GameCapability.CharacterQuery,   CapabilityAvailability.Available },
                { GameCapability.CharacterMove,    CapabilityAvailability.Available },
                { GameCapability.CharacterPose,    CapabilityAvailability.Available },
                { GameCapability.PlayerQuery,      CapabilityAvailability.Available },
                { GameCapability.InventoryQuery,   CapabilityAvailability.Available },
                { GameCapability.InventoryChange,  CapabilityAvailability.Available },
                { GameCapability.EconomyQuery,     CapabilityAvailability.Available },
                { GameCapability.EconomyChange,    CapabilityAvailability.Available },
                { GameCapability.CombatRecover,    CapabilityAvailability.Available },
                { GameCapability.CombatDamage,     CapabilityAvailability.Available },
                { GameCapability.MagicQuery,       CapabilityAvailability.Available },
                { GameCapability.AudioQuery,       CapabilityAvailability.Available },
                { GameCapability.AudioPlay,        CapabilityAvailability.Available },

                // ── 只读 ───────────────────────────────────────────────────────────
                // 天气：NightController 只暴露了 hasWeather/current_weather_bit 这些读取口，
                // 写入走的是它自己的日夜与事件推进逻辑，没有可以安全外部调用的 setter。
                { GameCapability.WorldWeather,     CapabilityAvailability.QueryOnly },

                // 原始输入：只有鼠标位置与滚轮可读（游戏自己在维护）。原始键码没有入口——
                // 游戏走的是 Input System 的动作映射，不存在一张"虚拟键码 → 是否按下"的表，
                // 按动作查询请用 InputAction。
                { GameCapability.InputRaw,         CapabilityAvailability.QueryOnly },

                // ── 本版本未支持 ───────────────────────────────────────────────────
                // 暂停：游戏没有一个"全局暂停"开关，XX.PAUSER 是逐对象的暂停记忆器，
                // 真正的暂停是菜单/事件各自停自己那摊东西。要做成带 owner 令牌的全局暂停
                // 得先补一层，不属于本层"如实转译现有能力"的范围。
                { GameCapability.LoopPause,        CapabilityAvailability.Unsupported },

                // 切图：M2LpMapTransferWarp 那条路径带着一整套事件、淡入淡出与存档时机，
                // 直接调用会把游戏留在半切图状态。属于高权限动作，须单独设计。
                { GameCapability.WorldTransition,  CapabilityAvailability.Unsupported },

                // 玩家动作（近战/滑铲/回避/各种突进）：PR.STATE 的迁移由输入与技能状态机驱动，
                // 外部直接写 state 会绕过前后摇、无敌帧与技能锁。须逐个动作核对入口后再开放。
                { GameCapability.PlayerActions,    CapabilityAvailability.Unsupported },

                // 使用/丢弃/提交物品：这三条都带 UI 流程（选择 grade、确认框、NPC 提交对象），
                // NelItem.Use 需要一个真实的 IItemUser 与 ItemStorage 上下文才不会算错消耗。
                { GameCapability.InventoryUse,     CapabilityAvailability.Unsupported },

                // 咏唱/释放：M2PrSkill 的咏唱有 MP 暂存、蓄力等级与手杖修正，
                // 释放还要决定弹体归属。只读部分（是否在咏唱、进度）已开放。
                { GameCapability.MagicCast,        CapabilityAvailability.Unsupported },
            };

        /// <summary>查这条能力本局通不通。未登记的能力按 <see cref="CapabilityAvailability.Unsupported"/> 处理。</summary>
        public static CapabilityAvailability Status(GameCapability capability)
            => Table.TryGetValue(capability, out CapabilityAvailability status)
                ? status
                : CapabilityAvailability.Unsupported;

        /// <summary>能不能执行写操作。<see cref="CapabilityAvailability.QueryOnly"/> 在这里算不能。</summary>
        public static bool CanWrite(GameCapability capability)
            => Status(capability) == CapabilityAvailability.Available;

        /// <summary>能不能读。只读也算能读。</summary>
        public static bool CanRead(GameCapability capability)
        {
            CapabilityAvailability status = Status(capability);
            return status == CapabilityAvailability.Available || status == CapabilityAvailability.QueryOnly;
        }

        /// <summary>整张表的只读快照，供管理页/诊断报告输出"本局哪些能力可用"。</summary>
        public static IEnumerable<KeyValuePair<GameCapability, CapabilityAvailability>> All() => Table;

        /// <summary>报告里那一段的文本，一行一条，只列出不完全可用的。</summary>
        public static string Summary()
        {
            var Sb = new System.Text.StringBuilder();
            foreach (KeyValuePair<GameCapability, CapabilityAvailability> pair in Table)
            {
                if (pair.Value == CapabilityAvailability.Available)
                {
                    continue;
                }

                Sb.Append(pair.Key).Append('=').Append(pair.Value).Append(Environment.NewLine);
            }

            return Sb.Length == 0 ? null : Sb.ToString();
        }
    }
}

namespace Polaris.API
{
    /// <summary>
    /// 全局静态回调的种类：与具体游戏实例无关的事件，经 <see cref="GameCallbacksAPI.Register{TData}"/> 注册。
    /// 每种都绑定唯一的回调数据类型（见 <see cref="GameCallbackContract"/>），类型不符会在注册时立即抛异常。
    /// </summary>
    public enum GameStaticCallbackKind
    {
        /// <summary>游戏场景完成初始化后触发。</summary>
        GameSceneStarted,

        /// <summary>新游戏初始化完成后触发。</summary>
        NewGameStarted,

        /// <summary>存档成功读取并应用后触发。</summary>
        SaveLoaded,

        /// <summary>存档读取失败后触发。</summary>
        SaveFailed,

        /// <summary>存档数据在内存序列化后触发。</summary>
        SaveSerialized,

        /// <summary>存档文件写入完成后触发。</summary>
        SaveWritten,

        /// <summary>自动保存流程结束后触发。</summary>
        AutoSaveCompleted,

        /// <summary>游戏语言切换完成后触发。</summary>
        LocaleChanged,

        /// <summary>任一指定输入动作被按下后触发。</summary>
        ActionPressed,

        /// <summary>任一指定输入动作被释放后触发。</summary>
        ActionReleased,

        /// <summary>当前地图切换完成后触发。</summary>
        MapChanged,

        /// <summary>任一地图打开完成后触发，可取得刚打开的 <see cref="GameMap"/> 实例。</summary>
        MapOpened,

        /// <summary>昼夜状态发生变化后触发。</summary>
        DayNightChanged,

        /// <summary>夜晚等级发生变化后触发。</summary>
        NightLevelChanged,

        /// <summary>危险度发生变化后触发。</summary>
        DangerLevelChanged,

        /// <summary>当前天气组合发生变化后触发。</summary>
        WeatherChanged,

        /// <summary>任一事件成功打开后触发，可取得刚打开的 <see cref="GameEvent"/> 实例。</summary>
        EventOpened,

        /// <summary>任一新物品被记录为已获得后触发。</summary>
        ItemObtained,

        /// <summary>地图上生成掉落物后触发。</summary>
        DropCreated,

        /// <summary>任一货币余额实际变化后触发。</summary>
        MoneyChanged,

        /// <summary>任务首次进入追踪列表后触发，可取得新的 <see cref="GameQuest"/> 实例。</summary>
        QuestStarted,

        /// <summary>重点追踪任务发生变化后触发。</summary>
        FocusedQuestChanged,

        /// <summary>剧情旗标值实际变化后触发。</summary>
        StoryFlagChanged,

        /// <summary>游戏菜单打开完成后触发，可取得新的 <see cref="GameMenu"/> 实例。</summary>
        GameMenuOpened,

        /// <summary>当前背景音乐曲目发生变化后触发。</summary>
        MusicChanged,

        /// <summary>背景音乐播放/停止状态变化后触发。</summary>
        MusicPlaybackChanged,

        /// <summary>任一音效成功开始播放后触发，负荷带 <see cref="GameAudioPlayback"/> 实例。</summary>
        SoundPlayed,

        /// <summary>任一音量通道数值发生变化后触发。</summary>
        VolumeChanged,
    }

    /// <summary>
    /// 实例回调的种类：只发给事件所属那一个实例的订阅者，经各实例类型自己的 <c>Register</c> 方法注册。
    /// 注册表按"种类 + 实例身份"区分，实例失效后其订阅自动停止收事件。
    /// </summary>
    public enum GameInstanceCallbackKind
    {
        /// <summary>该地图实例关闭完成后触发。</summary>
        MapClosed,

        /// <summary>该地图实例的动作逻辑初始化完成后触发。</summary>
        MapActionInitialized,

        /// <summary>该地图实例的动作逻辑关闭完成后触发。</summary>
        MapActionClosed,

        /// <summary>该事件实例成功关闭后触发。</summary>
        EventClosed,

        /// <summary>该玩家实例的状态实际变化后触发。</summary>
        PlayerStateChanged,

        /// <summary>该玩家实例首次进入死亡状态后触发。</summary>
        PlayerDied,

        /// <summary>该玩家实例从死亡状态恢复后触发。</summary>
        PlayerRevived,

        /// <summary>该敌人实例的状态实际变化后触发。</summary>
        EnemyStateChanged,

        /// <summary>该敌人实例首次进入死亡状态后触发。</summary>
        EnemyDied,

        /// <summary>该角色实例被施加击退速度后触发。</summary>
        KnockbackApplied,

        /// <summary>该角色实例新增状态效果后触发。</summary>
        StatusAdded,

        /// <summary>该角色实例已有状态效果被刷新后触发。</summary>
        StatusRefreshed,

        /// <summary>该角色实例的状态效果被移除后触发。</summary>
        StatusRemoved,

        /// <summary>该角色实例的一次多段伤害结算完成后触发。</summary>
        DamageApplied,

        /// <summary>该角色实例实际损失体力值后触发。</summary>
        HpDamageApplied,

        /// <summary>该角色实例实际损失魔力值后触发。</summary>
        MpDamageApplied,

        /// <summary>该角色实例实际恢复体力值或魔力值后触发。</summary>
        RecoveryApplied,

        /// <summary>物品实际加入该存储实例后触发。</summary>
        ItemAdded,

        /// <summary>物品实际从该存储实例移除后触发。</summary>
        ItemRemoved,

        /// <summary>该存储实例参与的物品转移完成后触发。</summary>
        ItemsTransferred,

        /// <summary>该非空存储实例被清空后触发。</summary>
        StorageCleared,

        /// <summary>该物品实例实际被使用后触发。</summary>
        ItemUsed,

        /// <summary>该任务实例的阶段发生变化后触发。</summary>
        QuestUpdated,

        /// <summary>该任务实例首次进入完成状态后触发。</summary>
        QuestCompleted,

        /// <summary>该任务实例从追踪列表移除后触发。</summary>
        QuestRemoved,

        /// <summary>该菜单实例关闭完成后触发。</summary>
        GameMenuClosed,
    }
}

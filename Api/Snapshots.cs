namespace Polaris.API
{
    /// <summary>
    /// 角色状态的一次性快照。<b>拿到就是死的</b>：它不会跟着游戏变，也不能写回去。
    /// 这是刻意的——旧 LuaAiC 把角色属性做成可读可写的字段，作者写进去的值会在游戏下一次
    /// 重算属性时被静默抹掉，或者反过来永久留在存档里。要改数值请走对应的动作 API。
    /// </summary>
    public sealed class CharacterSnapshot
    {
        public CharacterHandle Handle { get; internal set; }

        /// <summary>地图坐标（单位是地图格长换算后的 float，与游戏内部一致）。</summary>
        public GameVector2 Position { get; internal set; }

        public GameVector2 Velocity { get; internal set; }

        public float Hp { get; internal set; }

        public float MaxHp { get; internal set; }

        public float Mp { get; internal set; }

        public float MaxMp { get; internal set; }

        public bool IsAlive { get; internal set; }

        public override string ToString()
            => $"{Handle} hp={Hp:0}/{MaxHp:0} mp={Mp:0}/{MaxMp:0} pos={Position}";
    }

    /// <summary>
    /// 玩家快照。比 <see cref="CharacterSnapshot"/> 多的是只有玩家才有的东西：姿势、咏唱状态、
    /// 状态机当前状态。
    /// <para>
    /// 料理/装备派生出来的战斗数值（攻击力、各属性抗性、掉落率……）<b>暂时不在这里</b>：
    /// 它们在游戏里由多条重算链共同决定，把最终值抄进快照容易给人"读到即可写回"的错觉，
    /// 正确的做法是等 Modifier 通道那一层做出来之后再连同 source 一起暴露。
    /// </para>
    /// </summary>
    public sealed class PlayerSnapshot
    {
        public CharacterHandle Handle { get; internal set; }

        public GameVector2 Position { get; internal set; }

        public GameVector2 Velocity { get; internal set; }

        public float Hp { get; internal set; }

        public float MaxHp { get; internal set; }

        public float Mp { get; internal set; }

        public float MaxMp { get; internal set; }

        public bool IsAlive { get; internal set; }

        /// <summary>当前姿势名（游戏内部的 pose title，如 <c>stand</c>/<c>crouch</c>）。</summary>
        public string PoseTitle { get; internal set; }

        /// <summary>状态机当前状态的名字，如 <c>NORMAL</c>/<c>EVADE</c>/<c>DAMAGE</c>。只读，用于判断"现在能不能动"。</summary>
        public string StateName { get; internal set; }

        public bool IsChanting { get; internal set; }

        /// <summary>咏唱完成度 0–1；没在咏唱时为 0。</summary>
        public float ChantProgress { get; internal set; }

        public override string ToString()
            => $"{Handle} hp={Hp:0}/{MaxHp:0} mp={Mp:0}/{MaxMp:0} pose={PoseTitle} state={StateName}";
    }

    /// <summary>世界状态快照：地图、危险度、日夜与天气。</summary>
    public sealed class WorldSnapshot
    {
        /// <summary>当前地图 key；没有加载地图时为 <c>null</c>。</summary>
        public string MapKey { get; internal set; }

        /// <summary>危险度，玩家在状态页上看到的那个整数（含手动附加值，上限 160）。</summary>
        public int DangerLevel { get; internal set; }

        /// <summary>危险度里由手动附加值贡献的部分（0–45）。</summary>
        public int DangerBonus { get; internal set; }

        /// <summary>危险度的天数尺度（<c>危险度 / 16</c>，上限 10）。算敌人强度用，不是显示值。</summary>
        public float DangerDayScale { get; internal set; }

        public bool IsNight { get; internal set; }

        /// <summary>日夜进度。</summary>
        public float NightLevel { get; internal set; }

        /// <summary>当前生效的天气位掩码，逐位对应 <see cref="GameWeather"/>。</summary>
        public int WeatherBits { get; internal set; }

        public override string ToString()
            => $"map={MapKey} danger={DangerLevel}(+{DangerBonus}) night={IsNight} weather=0x{WeatherBits:X}";
    }

    /// <summary>
    /// 天气种类。与游戏的 <c>WeatherItem.WEATHER</c> 一一对应，但独立定义：
    /// 天气 key 会被写进内容定义，不应该跟着游戏枚举的增删一起漂。
    /// <para>
    /// 旧 LuaAiC 把它拼成 <c>Wether</c>，本层一律用正确拼写 <c>Weather</c>；
    /// 拼错的别名只在将来的 Lua 兼容层里保留。
    /// </para>
    /// </summary>
    public enum GameWeather
    {
        Normal = 0,
        Wind = 1,
        Thunder = 2,
        Mist = 3,
        Drought = 4,
        MistDense = 5,
        Plague = 6,
    }
}

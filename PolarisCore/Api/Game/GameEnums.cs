namespace Polaris.API
{
    /// <summary>角色朝向。游戏内部用布尔 <c>is_right</c> 表示，这里改用具名类型以避免调用点含义不明。</summary>
    public enum GameFacing
    {
        Left = 0,
        Right = 1,
    }

    /// <summary>天气种类，与游戏的 <c>WeatherItem.WEATHER</c> 一一对应但独立定义，避免跟随游戏枚举变动漂移。</summary>
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

    /// <summary>
    /// 游戏动作，对应内部按键映射的动作槽而非虚拟键码，天然跟随玩家改键设置。
    /// <c>ButtonZ</c>–<c>ButtonD</c> 保留游戏内部字母命名，因为其含义随场景变化，不适合起具体名字。
    /// </summary>
    public enum GameInputAction
    {
        Left,
        Right,
        Up,
        Down,
        Jump,
        Run,
        Check,
        Menu,
        Submit,
        Cancel,
        TabLeft,
        TabRight,
        Add,
        Remove,
        ButtonZ,
        ButtonX,
        ButtonC,
        ButtonA,
        ButtonS,
        ButtonD,
        Shift,
    }

    /// <summary>游戏里的几种货币。与 <c>CoinStorage.CTYPE</c> 一一对应。</summary>
    public enum GameCurrency
    {
        Gold = 0,
        Crafts = 1,
        Juice = 2,
    }

    /// <summary>
    /// 玩家状态机的状态，数值与游戏的 <c>PR.STATE</c> 一致（不可重新编号）。
    /// 本枚举<b>不是</b>穷尽的，<see cref="GamePlayer.State"/> 遇到未知值会原样返回该数值。
    /// </summary>
    public enum GamePlayerState
    {
        Offline = -1,
        Normal = 0,
        MagicExplodePrepare = 1,
        MagicExploded = 2,
        Evade = 10,
        Ukemi = 11,
        EvadeShotgun = 12,
        UkemiShotgun = 13,
        EvadeJump = 14,
        Punch = 20,
        Burst = 22,
        Sliding = 23,
        Wheel = 24,
        WheelShotgun = 25,
        Comet = 26,
        CometShotgun = 27,
        DashPunch = 28,
        DashPunchShotgun = 29,
        AirPunch = 30,
        AirPunchShotgun = 31,
        ShieldBush = 40,
        ShieldLariat = 41,
        EvadeCounter = 42,
        EvadeCounterShotgun = 43,
        Smash = 44,
        SmashShotgun = 45,
        BurstScapecat = 200,
        SpecialRun = 250,
        UseBomb = 390,
        EnemySink = 430,
        ShieldBreakStun = 431,
        LayingEgg = 432,
        Orgasm = 433,
        GameOverRecovery = 434,
        WaterChokedRelease = 436,
        Sleep = 437,
        Frozen = 438,
        Onnie = 500,
        EventGacha = 501,
        Damage = 4000,
        DamageLarge = 4010,
        DamageLargeHitWall = 4011,
        DamageLargeLand = 4015,
        DamageLargeDownAbsorbAfter = 4016,
        DamageLaunch = 4020,
        DamageLaunchSpin = 4022,
        DamageLaunchLand = 4025,
        DamagePressLeftRight = 4030,
        DamagePressTopBottom = 4031,
        DamageOtherStun = 4050,
        DownStun = 4100,
        DamageBurned = 4150,
        DamageWebTrapped = 4200,
        DamageWebTrappedLand = 4201,
        Absorb = 4600,
        WormTrapped = 4980,
        WaterChoked = 4981,
        WaterChokedDown = 4982,
        Bench = 10000,
        BenchLoadAfter = 10001,
        BenchOnnie = 10002,
        BenchSitDownWait = 100003,
    }

    /// <summary>
    /// 敌人状态机的状态。数值与游戏的 <c>NelEnemy.STATE</c> 一致，理由同
    /// <see cref="GamePlayerState"/>；同样<b>不是</b>穷尽枚举。
    /// </summary>
    public enum GameEnemyState
    {
        Stand = 0,
        Special = 500,
        Stun = 900,
        Absorb = 1000,
        OverdriveActivate = 2000,
        Damage = 4000,
        DamageLaunch = 4010,
        Die = 5000,
        Summoned = 10000,
        RingOutResume = 10001,
    }

    /// <summary>
    /// 敌人种类编号，数值与游戏的 <c>nel.ENEMYID</c> 一致。高位 <c>0x80000000</c> 是狂暴形态附加位
    /// （非独立种类），<see cref="GameEnemy.EnemyId"/> 会剥掉它，狂暴状态请看 <see cref="GameEnemy.State"/>。
    /// </summary>
    public enum GameEnemyId
    {
        Slime = 256,
        SlimeTutorial = 496,
        SlimeFollower = 497,
        SlimeTutorialGarage = 498,
        Mushroom = 512,
        MushroomFollower = 753,
        Puppy = 768,
        PuppyEvent = 1008,
        Golem = 1024,
        GolemNightmare = 1152,
        Snake = 1280,
        SnakeTutorial = 1520,
        Sponge = 1536,
        Sponge1 = 1537,
        Uni = 1792,
        Mage = 4096,
        Fox = 4352,
        GolemToyMkb = 4608,
        GolemToyRm = 4609,
        GolemToyPod = 4610,
        GolemToyBow = 4611,
        GolemToyCatapult = 4816,
        GolemToyCatapult0 = 4817,
        Gecko = 4864,
        GeckoFollower = 5105,
        Frog = 5120,
        Pentapod = 5376,
        PentapodNightmare = 5504,
        PentapodHead = 5632,
        Pig = 5888,
        Roaper = 6144,
        Ramda = 6400,
        EHome = 8192,
        Honeycomb = 8448,
        Syabon = 8704,
        SyabonGimmick = 8944,
        Leech = 8960,
        LeechWcnc = 9216,
        Empress = 9472,
        LeechQueen = 9728,
        LeechQueenWcnc = 9984,
        SgWall = 10240,
        SgWallCnc = 10496,
        Mimic = 12288,
        MechGolem = 393216,
        MechGolem1 = 393217,
        WanderPuppetNpc = 458752,
        MgmFarmCowNpc = 458753,
        MgmFarmChickenNpc = 458754,
        BossNusi = 524288,
        BossNusiCage = 524289,
        BossNusiTentacle = 524290,
        BossSpider = 524544,
    }

    /// <summary>物品分类（位标志，数值与 <c>NelItem.CATEG</c> 一致）；判断单项属性优先用 <see cref="GameItem"/> 上的 <c>IsFood</c>/<c>IsTool</c> 等属性。</summary>
    [System.Flags]
    public enum GameItemCategory
    {
        Other = 0,
        IndividualGrade = 1,
        CureHp = 16,
        CureMp = 32,
        CureEp = 64,
        Material = 128,
        ForFishing = 256,
        Water = 4096,
        CureMpCrack = 8192,
        StatusApply = 16384,
        StatusCure = 32768,
        Fruit = 65536,
        Food = 131072,
        Dust = 262144,
        Ancient = 524288,
        Tool = 1048576,
        Special = 2097152,
        SpecialUse = 4194304,
        Enhancer = 8388608,
        Bomb = 16777216,
        Bottle = 268435456,
    }
}

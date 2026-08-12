using System;

namespace Polaris.API
{
    /// <summary>
    /// 所有"活实例"包装器的公共基类：地图、角色、玩家、敌人、物品、存储、音频播放、菜单、
    /// 事件、任务都是它的子类。
    /// <para>
    /// 三条贯穿全部实例类型的规则，子类不得各自另立一套：
    /// </para>
    /// <list type="number">
    /// <item>
    /// <b>身份稳定</b>：同一个游戏对象在存活期内，反复取到的永远是同一个包装器实例，
    /// 因此可以直接用 <c>ReferenceEquals</c> 比较，也可以安全地当字典键。
    /// </item>
    /// <item>
    /// <b>失效即拒绝</b>：地图切换、菜单关闭、事件结束、敌人销毁之后，旧包装器进入失效状态。
    /// 失效后读取只读成员返回零值/空值，任何<b>写操作</b>抛
    /// <see cref="InvalidGameInstanceException"/>——安静地作用到"下一任住客"身上是这类
    /// 对象池复用 API 最难查的一类 bug，宁可吵。
    /// </item>
    /// <item>
    /// <b>回调随实例失效</b>：注册在该实例上的回调在它失效时一并停止，不需要调用方善后。
    /// </item>
    /// </list>
    /// </summary>
    public abstract class GameInstance
    {
        static long nextId;

        bool invalidated;

        private protected GameInstance()
        {
            InstanceId = System.Threading.Interlocked.Increment(ref nextId);
        }

        /// <summary>进程内唯一的实例编号。回调注册表用它作键的一半。</summary>
        internal long InstanceId { get; }

        /// <summary>
        /// 这个包装器是否仍指向一个活着的游戏对象。
        /// <para>
        /// 除了显式失效之外，还会问一次子类的 <see cref="IsNativeAlive"/>：
        /// 游戏对象经常是"没人通知就没了"的（对象池回收、场景卸载），
        /// 只靠事件驱动的失效标记会漏。
        /// </para>
        /// </summary>
        public bool IsValid
        {
            get
            {
                if (invalidated)
                {
                    return false;
                }

                bool alive;
                try
                {
                    alive = IsNativeAlive;
                }
                catch (Exception)
                {
                    alive = false;
                }

                if (!alive)
                {
                    Invalidate();
                }

                return alive;
            }
        }

        /// <summary>子类回答"底层游戏对象还在不在"。不要在这里抛异常，基类会兜底当成已失效。</summary>
        private protected abstract bool IsNativeAlive { get; }

        /// <summary>诊断用的一句话，出现在异常消息里。</summary>
        private protected abstract string Describe();

        /// <summary>
        /// 在这个实例上注册回调，只会收到发生在<b>它自己</b>身上的事件。
        /// <para>
        /// <paramref name="kind"/> 与 <typeparamref name="TData"/> 不匹配、
        /// 或者这种回调根本不属于当前实例类型（例如把敌人回调注册到存储上）时，
        /// 立即抛 <see cref="ArgumentException"/>，不会安静地注册一个永远收不到事件的回调。
        /// </para>
        /// </summary>
        public GameCallbackRegistration Register<TData>(
            GameInstanceCallbackKind kind, Action<TData> callback, GameCallbackOptions options = default)
            where TData : GameCallbackData
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            GameCallbackContract.EnsureInstance<TData>(kind, this);
            return GameCallbackHub.RegisterInstance(kind, this, callback, options);
        }

        /// <summary>让这个包装器失效，并停掉挂在它上面的全部回调。可重复调用。</summary>
        internal void Invalidate()
        {
            if (invalidated)
            {
                return;
            }

            invalidated = true;
            GameCallbackHub.ReleaseInstance(InstanceId);
        }

        /// <summary>写操作的统一入口检查：失效就抛，而不是安静地作用到别的对象上。</summary>
        private protected void EnsureUsable()
        {
            if (!IsValid)
            {
                throw new InvalidGameInstanceException(Describe());
            }
        }

        public override string ToString() => IsValid ? Describe() : $"{Describe()} (invalid)";
    }

    /// <summary>
    /// 对一个已经失效的游戏实例执行写操作时抛出。
    /// <para>
    /// 只读成员不抛这个异常——查询在任何时刻都应该能安全地问一句，失效时返回零值/空值即可。
    /// 会抛的是"改变游戏状态"的那一类调用：它们如果被安静地忽略，调用方会以为自己改成功了。
    /// </para>
    /// </summary>
    public sealed class InvalidGameInstanceException : InvalidOperationException
    {
        internal InvalidGameInstanceException(string what)
            : base($"This game instance is no longer valid: {what}. It was released by a map change, a close, or the object being destroyed.")
        {
        }
    }
}

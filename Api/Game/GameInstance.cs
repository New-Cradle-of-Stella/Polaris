using System;

namespace Polaris.API
{
    /// <summary>
    /// 所有"活实例"包装器的公共基类（地图、角色、玩家、敌人、物品、存储、音频播放、菜单、事件、任务）。
    /// 同一游戏对象始终对应同一包装器实例；失效后只读成员返回零值/空值，写操作抛
    /// <see cref="InvalidGameInstanceException"/>；实例失效时其上注册的回调一并停止。
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

        /// <summary>这个包装器是否仍指向一个活着的游戏对象；也会主动查一次 <see cref="IsNativeAlive"/>，因为对象池回收等场景不总会显式通知失效。</summary>
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

        /// <summary>在这个实例上注册回调，只收到发生在它自己身上的事件；<paramref name="kind"/> 与 <typeparamref name="TData"/> 或实例类型不匹配时立即抛 <see cref="ArgumentException"/>。</summary>
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

    /// <summary>对一个已失效的游戏实例执行写操作时抛出；只读成员失效时只返回零值/空值，不抛异常。</summary>
    public sealed class InvalidGameInstanceException : InvalidOperationException
    {
        internal InvalidGameInstanceException(string what)
            : base($"This game instance is no longer valid: {what}. It was released by a map change, a close, or the object being destroyed.")
        {
        }
    }
}

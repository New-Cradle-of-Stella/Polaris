using System;
using nel;

namespace Polaris.API
{
    /// <summary>游戏里的几种货币。与 <c>CoinStorage.CTYPE</c> 一一对应。</summary>
    public enum GameCurrency
    {
        Gold = 0,
        Crafts = 1,
        Juice = 2,
    }

    /// <summary>
    /// 金钱。旧 LuaAiC 只有一个 <c>GetMoney(v)</c>，正负都往里塞；这里拆成 <see cref="Add"/> 与
    /// <see cref="Spend"/>，因为"付不起"是一条调用方必须处理的正常分支，而不是一次失败。
    /// </summary>
    public sealed class EconomyGameAPI
    {
        /// <summary>当前持有量。</summary>
        public long Amount(GameCurrency currency = GameCurrency.Gold)
        {
            try
            {
                return CoinStorage.getCount((CoinStorage.CTYPE)(int)currency);
            }
            catch (Exception)
            {
                return 0L;
            }
        }

        /// <summary>增加。<paramref name="amount"/> 必须为正。</summary>
        public MoneyChangeResult Add(int amount, GameCurrency currency = GameCurrency.Gold)
        {
            if (amount <= 0)
            {
                return new MoneyChangeResult(
                    GameActionResult.Fail(GameActionStatus.InvalidArgument, "Amount to add must be positive; use Spend to deduct."),
                    Amount(currency));
            }

            try
            {
                CoinStorage.addCount(amount, (CoinStorage.CTYPE)(int)currency);
                return new MoneyChangeResult(GameActionResult.Ok(), Amount(currency));
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "Economy.Add");
                return new MoneyChangeResult(GameActionResult.Fail(GameActionStatus.Failed, ex.Message), Amount(currency));
            }
        }

        /// <summary>
        /// 扣除。余额不足时<b>一分不扣</b>并返回 <see cref="GameActionStatus.InsufficientResource"/>；
        /// 先扣一部分再报失败会让调用方的事务无从回滚。
        /// </summary>
        public MoneyChangeResult Spend(int amount, GameCurrency currency = GameCurrency.Gold)
        {
            if (amount <= 0)
            {
                return new MoneyChangeResult(
                    GameActionResult.Fail(GameActionStatus.InvalidArgument, "Amount to deduct must be positive; use Add to credit."),
                    Amount(currency));
            }

            long balance = Amount(currency);
            if (balance < amount)
            {
                return new MoneyChangeResult(
                    GameActionResult.Fail(GameActionStatus.InsufficientResource, $"Insufficient balance: have {balance}, need {amount}."),
                    balance);
            }

            try
            {
                CoinStorage.reduceCount(amount, (CoinStorage.CTYPE)(int)currency);
                return new MoneyChangeResult(GameActionResult.Ok(), Amount(currency));
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "Economy.Spend");
                return new MoneyChangeResult(GameActionResult.Fail(GameActionStatus.Failed, ex.Message), Amount(currency));
            }
        }
    }

    /// <summary>金钱变动结果，带上变动后的余额，省得调用方再查一次。</summary>
    public readonly struct MoneyChangeResult
    {
        public GameActionResult Outcome { get; }

        public long Balance { get; }

        internal MoneyChangeResult(GameActionResult outcome, long balance)
        {
            Outcome = outcome;
            Balance = balance;
        }

        public bool Succeeded => Outcome.Succeeded;

        public override string ToString() => $"{Outcome} balance={Balance}";
    }
}

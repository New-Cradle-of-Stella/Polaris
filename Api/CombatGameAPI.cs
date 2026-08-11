using System;
using m2d;

namespace Polaris.API
{
    /// <summary>
    /// 伤害与恢复。两者刻意分成两个方法而不是一个带正负号的数值：它们在游戏里走不同的处理链，
    /// 混成一个会让"回血"意外触发受击硬直、或者让"扣血"绕过护盾。
    /// </summary>
    public sealed class CombatGameAPI
    {
        /// <summary>给玩家回复。玩家不在场时返回 <see cref="GameActionStatus.TargetUnavailable"/>。</summary>
        public RecoveryResult RecoverPlayer(RecoveryRequest request)
            => Recover(PolarisAPI.Game.Player.Handle, request);

        /// <summary>给某个角色回复。</summary>
        public RecoveryResult Recover(CharacterHandle handle, RecoveryRequest request)
        {
            if (request.Hp < 0 || request.Mp < 0)
            {
                return new RecoveryResult(
                    GameActionResult.Fail(GameActionStatus.InvalidArgument, "Recovery amount cannot be negative; use ApplyDamage to subtract health."),
                    0f, 0f);
            }

            M2Attackable Target = CharacterRegistry.Resolve(handle);
            if (Target == null)
            {
                return new RecoveryResult(CharacterGameAPI.Expired(), 0f, 0f);
            }

            try
            {
                // 前后各读一次实际值：游戏会按上限、溢出与蓄能槽规则裁剪，请求量不等于到账量，
                // 调用方（尤其是"回满才算成功"的物品）需要知道真的回了多少。
                float hpBefore = Target.get_hp();
                float mpBefore = Target.get_mp();

                if (request.Hp > 0)
                {
                    Target.cureHp(request.Hp);
                }

                if (request.Mp > 0)
                {
                    Target.cureMp(request.Mp);
                }

                return new RecoveryResult(
                    GameActionResult.Ok(),
                    Target.get_hp() - hpBefore,
                    Target.get_mp() - mpBefore);
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "Combat.Recover");
                return new RecoveryResult(GameActionResult.Fail(GameActionStatus.Failed, ex.Message), 0f, 0f);
            }
        }

        /// <summary>对某个角色造成伤害。</summary>
        public DamageResult ApplyDamage(CharacterHandle handle, DamageRequest request)
        {
            if (request.HpDamage < 0 || request.MpDamage < 0)
            {
                return new DamageResult(
                    GameActionResult.Fail(GameActionStatus.InvalidArgument, "Damage amount cannot be negative; use Recover to restore health."),
                    0, 0);
            }

            M2Attackable Target = CharacterRegistry.Resolve(handle);
            if (Target == null)
            {
                return new DamageResult(CharacterGameAPI.Expired(), 0, 0);
            }

            try
            {
                int hp = request.HpDamage > 0 ? Target.applyHpDamage(request.HpDamage, request.Force) : 0;
                int mp = request.MpDamage > 0 ? Target.applyMpDamage(request.MpDamage, request.Force) : 0;
                return new DamageResult(GameActionResult.Ok(), hp, mp);
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "Combat.ApplyDamage");
                return new DamageResult(GameActionResult.Fail(GameActionStatus.Failed, ex.Message), 0, 0);
            }
        }
    }
}

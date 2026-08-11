using System.Collections.Generic;
using PixelLiner;

namespace Polaris.Res.Pxls
{
    /// <summary>
    /// 帧名注册策略。<c>XX.MTRX.OMeshImages</c> 是全局扁平表，与原版共享——同一个帧名
    /// 撞车会静默覆盖谁后写谁赢，见 <see cref="PxlsRegistration"/> 里的取舍。
    /// <para>公开——<see cref="Import.PxlsImportSettings.FrameNamePolicy"/> 是模组作者可以直接
    /// 设置的字段，公开类型的公开字段不能用不可见的枚举类型。</para>
    /// </summary>
    public enum FrameNamePolicy
    {
        /// <summary>默认：每个帧名前面加 <c>"&lt;modId&gt;/"</c> 前缀，天然避免撞原版或撞其它模组。</summary>
        Prefixed,

        /// <summary>原样调用 <c>MTRX.assignPxlImages(pc)</c>，给确实想替换原版帧的模组用；撞车只警告不阻止。</summary>
        Raw,

        /// <summary>完全不注册帧名——角色仍然可以通过 <see cref="PxlsCharacterHandle.GetPose"/>/<c>GetFrame</c> 用，只是不会出现在 <c>MTRX.getPF</c> 的全局命名空间里。</summary>
        None,
    }

    /// <summary>
    /// 包装 <c>XX.MTRX.assignPxlImages</c> 的注册/撤销。必须在 <c>MTRX.assignMI</c> **之后**调用
    /// ——帧名一旦公开进 <c>OMeshImages</c>，任何人都能经 <c>MTRX.getPF</c> 间接触发
    /// <c>MTRX.getMI(pChar)</c>，见旧计划"已核实的关键事实 #12"。
    /// </summary>
    internal static class PxlsRegistration
    {
        /// <summary>
        /// 按策略注册帧名，返回本次实际写入 <c>OMeshImages</c> 的键（<see cref="FrameNamePolicy.Prefixed"/>
        /// 用于卸载时撤销；<see cref="FrameNamePolicy.Raw"/>/<see cref="FrameNamePolicy.None"/> 返回
        /// <c>null</c>——<c>Raw</c> 故意不追踪撞车前的旧值以支持恢复，见计划里"不做完整撤销/恢复"的取舍；
        /// 撞车时已经有醒目警告，模组作者自己承担后果）。
        /// </summary>
        internal static List<string> Register(PxlCharacter pc, FrameNamePolicy policy, string prefix)
        {
            switch (policy)
            {
                case FrameNamePolicy.None:
                    return null;

                case FrameNamePolicy.Raw:
                    XX.MTRX.assignPxlImages(pc);
                    return null;

                case FrameNamePolicy.Prefixed:
                default:
                    return RegisterPrefixed(pc, prefix);
            }
        }

        /// <summary>把注册过的键从 <c>OMeshImages</c> 里摘掉。<c>MTRX</c> 没有公开的 remove API，
        /// 只有 <c>assignPxlImages(name, frame)</c> 这一个设值入口——把值设成 <c>null</c> 是唯一能用的
        /// 撤销方式；残留的 null 值键本身不会再被 <c>getPF</c> 当成有效帧返回，功能上等价于删除。</summary>
        internal static void Unregister(List<string> writtenKeys)
        {
            if (writtenKeys == null)
            {
                return;
            }

            foreach (string key in writtenKeys)
            {
                XX.MTRX.assignPxlImages(key, null);
            }
        }

        private static List<string> RegisterPrefixed(PxlCharacter pc, string prefix)
        {
            List<string> written = new List<string>();

            int poseCount = pc.countPoses();
            for (int p = 0; p < poseCount; p++)
            {
                PxlPose pose = pc.getPose(p);

                // 8 个朝向槽位是 XX.MTRX.assignPxlImages(PxlPose, bool) 自己遍历时用的同一个上限
                // （反编译确认的字面常量），这里照抄同样的范围，不额外发明新的常量来源。
                for (int aim = 0; aim < 8; aim++)
                {
                    if (!pose.isValidAim(aim) || pose.isFlipped(aim))
                    {
                        continue;
                    }

                    PxlSequence sequence = pose.getSequence(aim);
                    int frameCount = sequence.countFrames();
                    for (int f = 0; f < frameCount; f++)
                    {
                        PxlFrame frame = sequence.getFrame(f);
                        string baseName = string.IsNullOrEmpty(frame.name) ? pose.title + "." + f : frame.name;
                        string qualified = prefix + baseName;
                        XX.MTRX.assignPxlImages(qualified, frame);
                        written.Add(qualified);
                    }
                }
            }

            return written;
        }
    }
}

using System.Collections.Generic;
using PixelLiner;

namespace Polaris.Res.Pxls
{
    /// <summary>帧名注册策略。<c>XX.MTRX.OMeshImages</c> 是全局扁平表，同名帧会被后写者静默覆盖。</summary>
    public enum FrameNamePolicy
    {
        /// <summary>默认：每个帧名前加 <c>"&lt;modId&gt;/"</c> 前缀，避免撞名。</summary>
        Prefixed,

        /// <summary>原样调用 <c>MTRX.assignPxlImages(pc)</c>，用于替换原版帧；撞车只警告不阻止。</summary>
        Raw,

        /// <summary>不注册帧名；角色仍可通过 <see cref="PxlsCharacterHandle.GetPose"/>/<c>GetFrame</c> 使用。</summary>
        None,
    }

    /// <summary>包装 <c>XX.MTRX.assignPxlImages</c> 的注册/撤销。必须在 <c>MTRX.assignMI</c> 之后调用。</summary>
    internal static class PxlsRegistration
    {
        /// <summary>按策略注册帧名，返回实际写入 <c>OMeshImages</c> 的键（<see cref="FrameNamePolicy.Prefixed"/> 用于卸载时撤销，其余策略返回 <c>null</c>）。</summary>
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

        /// <summary>把注册过的键从 <c>OMeshImages</c> 里摘掉；<c>MTRX</c> 没有 remove API，只能把值设为 <c>null</c>，等效于删除。</summary>
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

                // 8 个朝向槽位与 XX.MTRX.assignPxlImages(PxlPose, bool) 的遍历上限一致。
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

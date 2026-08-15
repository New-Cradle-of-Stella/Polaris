using System;

namespace Polaris.PUI
{
    /// <summary>
    /// 把已注册的 PUI 内容直接接到 <see cref="GameMenuAPI"/> 的分类内容区上；只接受 <see cref="IPUI"/>，
    /// 分类内容区尺寸固定，不调用 <see cref="IPUI.GetUIWindow"/>，.pui 里声明的 Window 宽高不生效。
    /// </summary>
    public sealed class GameMenuPuiFacade
    {
        internal GameMenuPuiFacade() { }

        /// <summary>
        /// 在游戏菜单左侧追加一个分类，内容由 <paramref name="pui"/> 的 <see cref="IPUI.BuildUI"/>
        /// 直接填充；等价于 <c>PolarisAPI.GameMenu.AddCategory(name, displayName, pui.BuildUI, canEnter)</c>。
        /// </summary>
        /// <param name="name">分类内部标识，规则与 <see cref="GameMenuAPI.AddCategory"/> 一致</param>
        /// <param name="displayName">左侧分类按钮显示文案</param>
        /// <param name="pui">提供内容的 PUI；只用到它的 BuildUI，不会调用 GetUIWindow</param>
        /// <param name="canEnter">是否允许进入该分类；默认始终允许</param>
        /// <param name="insertIndex">添加位置，规则与 <see cref="GameMenuAPI.AddCategory"/> 一致，-1为在最后追加（默认为-1）</param>
        /// <returns>分配到的 CATEG 整数值</returns>
        public int AddCategory(string name, string displayName, IPUI pui, Func<bool> canEnter = null, int insertIndex = -1)
        {
            if (pui == null)
            {
                throw new ArgumentNullException(nameof(pui));
            }

            return PolarisAPI.GameMenu.AddCategory(name, displayName, pui.BuildUI, canEnter, insertIndex);
        }

        /// <summary>按已注册的 PUI 名字解析；解析不到直接抛异常。</summary>
        public int AddCategory(string name, string displayName, string puiName, Func<bool> canEnter = null, int insertIndex = -1)
        {
            if (!PolarisUIAPI.Pui.TryGet(puiName, out PUIRuntime runtime))
            {
                throw new ArgumentException($"\"{puiName}\" is not a registered PUI", nameof(puiName));
            }

            return AddCategory(name, displayName, runtime.Handler, canEnter, insertIndex);
        }
    }
}

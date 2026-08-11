namespace Polaris.Lang
{
    /// <summary>
    /// 由 <c>.plang</c> 生成的注册类实现：把这份文件里的 Key/文案交给
    /// <see cref="PlangRuntime.Register"/>。见 <see cref="PlangAutoRegistrationAttribute"/>。
    /// </summary>
    public interface IPlangRegistrar
    {
        void Register();
    }
}

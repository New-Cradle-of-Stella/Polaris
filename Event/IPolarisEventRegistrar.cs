namespace Polaris.Event
{
    public interface IPolarisEventRegistrar
    {
        void Register(PolarisEventRegistrationContext context);
    }
}

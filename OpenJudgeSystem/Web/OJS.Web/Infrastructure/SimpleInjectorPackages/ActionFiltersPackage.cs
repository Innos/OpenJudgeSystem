namespace OJS.Web.Infrastructure.SimpleInjectorPackages
{
    using OJS.Web.Infrastructure.Filters.Contracts;

    using SimpleInjector;
    using SimpleInjector.Packaging;

    public class ActionFiltersPackage : IPackage
    {
        public void RegisterServices(Container container)
        {
            container.Register(typeof(IActionFilter<>), typeof(IActionFilter<>).Assembly);
        }
    }
}
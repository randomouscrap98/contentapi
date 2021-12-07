using contentapi.Db.History;
using Microsoft.Extensions.DependencyInjection;

namespace contentapi.Db.Setup
{
    public static class DefaultSetup
    {
        /// <summary>
        /// Add all default service implementations to the given service container. Can override later of course.
        /// </summary>
        /// <remarks>
        /// To replace services (such as for unit tests), you can do: services.Replace(ServiceDescriptor.Transient<IFoo, FooB>());i
        /// </remarks>
        /// <param name="services"></param>
        public static void AddDefaultServices(IServiceCollection services)
        {
            services.AddSingleton<IHistoryConverter, HistoryConverter>();
            services.AddAutoMapper(typeof(ContentSnapshotProfile));
        }
    }
}

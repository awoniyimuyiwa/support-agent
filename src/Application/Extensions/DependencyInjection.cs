using Application.BackgroundJobs;
using Application.BackgroundJobs.Handlers;
using Application.Contracts;
using Application.Contracts.BackgroundJobs;
using Application.Services;
using Domain.Shared.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Application.Extensions;

public static class DependencyInjection
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddApplicationServices()
        {
            services
                .AddScoped<ITicketService, TicketService>()
                .AddScoped<IBackgroundJobManager, BackgroundJobManager>()
                .AddBackgroundJobHandlers(); // Calls the inner block method seamlessly

            return services;
        }

        /// <summary>
        /// Scans the assembly for all concrete <see cref="IBackgroundJobHandler"/> implementations.
        /// </summary>
        private IServiceCollection AddBackgroundJobHandlers()
        {
            var mappings = new Dictionary<Type, BackgroundJobType>();

            var handlers = Assembly.GetExecutingAssembly().GetTypes()
                .Where(h => h.IsClass && !h.IsAbstract && typeof(IBackgroundJobHandler).IsAssignableFrom(h))
                .Select(h => new
                {
                    Attr = h.GetCustomAttribute<BackgroundJobHandlerAttribute>(),
                    Implementation = h,
                    DtoType = h.BaseType?.IsGenericType == true && h.BaseType.GetGenericTypeDefinition() == typeof(BackgroundJobHandler<>)
                        ? h.BaseType.GetGenericArguments()[0]
                        : null
                })
                .Where(h => h.Attr != null);

            foreach (var h in handlers)
            {
                services.AddKeyedScoped(typeof(IBackgroundJobHandler), h.Attr!.Type, h.Implementation);

                if (h.DtoType != null)
                {
                    mappings[h.DtoType] = h.Attr.Type;
                }
            }

            services.AddTransient<IBackgroundJobRunner, BackgroundJobRunner>();
            services.AddSingleton<IBackgroundJobTypeRegistry>(new BackgroundJobTypeRegistry(mappings));

            return services;
        }
    }
}

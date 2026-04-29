using DockerCrudDemo.Application;
using DockerCrudDemo.Infrastructure.Messaging;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DockerCrudDemo.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString,
        string rabbitHost,
        string rabbitUser,
        string rabbitPass)
    {
        services.AddDbContext<AppDbContext>(opt =>
            opt.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 4, 0))));
        services.AddScoped<IProductRepository, ProductRepository>();

        services.AddMassTransit(x =>
        {
            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(rabbitHost, "/", h =>
                {
                    h.Username(rabbitUser);
                    h.Password(rabbitPass);
                });
            });
        });
        services.AddScoped<IEventPublisher, MassTransitEventPublisher>();

        services.AddScoped<ProductService>();
        return services;
    }
}

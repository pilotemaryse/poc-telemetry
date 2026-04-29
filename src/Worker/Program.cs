using DockerCrudDemo.Worker.Consumers;
using MassTransit;

var builder = Host.CreateApplicationBuilder(args);

var rabbitHost = builder.Configuration["RabbitMq:Host"] ?? "localhost";
var rabbitUser = builder.Configuration["RabbitMq:User"] ?? "guest";
var rabbitPass = builder.Configuration["RabbitMq:Password"] ?? "guest";

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ProductCreatedConsumer>();
    x.AddConsumer<ProductUpdatedConsumer>();
    x.AddConsumer<ProductDeletedConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(rabbitHost, "/", h =>
        {
            h.Username(rabbitUser);
            h.Password(rabbitPass);
        });
        cfg.ReceiveEndpoint("product-events", e =>
        {
            e.ConfigureConsumer<ProductCreatedConsumer>(ctx);
            e.ConfigureConsumer<ProductUpdatedConsumer>(ctx);
            e.ConfigureConsumer<ProductDeletedConsumer>(ctx);
        });
    });
});

builder.Build().Run();

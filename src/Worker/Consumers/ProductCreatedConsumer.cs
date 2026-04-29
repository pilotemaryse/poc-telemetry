using DockerCrudDemo.Domain.Events;
using MassTransit;

namespace DockerCrudDemo.Worker.Consumers;

public class ProductCreatedConsumer : IConsumer<ProductCreated>
{
    private readonly ILogger<ProductCreatedConsumer> _logger;
    public ProductCreatedConsumer(ILogger<ProductCreatedConsumer> logger) => _logger = logger;

    public Task Consume(ConsumeContext<ProductCreated> context)
    {
        var e = context.Message;
        _logger.LogInformation("[ProductCreated] Id={Id} Name={Name} Price={Price} Stock={Stock} At={At:O}",
            e.Id, e.Name, e.Price, e.Stock, e.OccurredOn);
        return Task.CompletedTask;
    }
}

public class ProductUpdatedConsumer : IConsumer<ProductUpdated>
{
    private readonly ILogger<ProductUpdatedConsumer> _logger;
    public ProductUpdatedConsumer(ILogger<ProductUpdatedConsumer> logger) => _logger = logger;

    public Task Consume(ConsumeContext<ProductUpdated> context)
    {
        var e = context.Message;
        _logger.LogInformation("[ProductUpdated] Id={Id} Name={Name} Price={Price} Stock={Stock} At={At:O}",
            e.Id, e.Name, e.Price, e.Stock, e.OccurredOn);
        return Task.CompletedTask;
    }
}

public class ProductDeletedConsumer : IConsumer<ProductDeleted>
{
    private readonly ILogger<ProductDeletedConsumer> _logger;
    public ProductDeletedConsumer(ILogger<ProductDeletedConsumer> logger) => _logger = logger;

    public Task Consume(ConsumeContext<ProductDeleted> context)
    {
        var e = context.Message;
        _logger.LogInformation("[ProductDeleted] Id={Id} At={At:O}", e.Id, e.OccurredOn);
        return Task.CompletedTask;
    }
}

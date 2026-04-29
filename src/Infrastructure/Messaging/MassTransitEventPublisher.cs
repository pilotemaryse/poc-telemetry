using DockerCrudDemo.Application;
using MassTransit;

namespace DockerCrudDemo.Infrastructure.Messaging;

public class MassTransitEventPublisher : IEventPublisher
{
    private readonly IPublishEndpoint _publish;
    public MassTransitEventPublisher(IPublishEndpoint publish) => _publish = publish;

    public Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : class =>
        _publish.Publish(@event, ct);
}

namespace DockerCrudDemo.Domain.Events;

public record ProductCreated(int Id, string Name, decimal Price, int Stock, DateTime OccurredOn);
public record ProductUpdated(int Id, string Name, decimal Price, int Stock, DateTime OccurredOn);
public record ProductDeleted(int Id, DateTime OccurredOn);

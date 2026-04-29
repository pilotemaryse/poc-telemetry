namespace DockerCrudDemo.Application;

public record ProductDto(int Id, string Name, decimal Price, int Stock);
public record CreateProductDto(string Name, decimal Price, int Stock);
public record UpdateProductDto(string Name, decimal Price, int Stock);

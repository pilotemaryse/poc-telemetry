using DockerCrudDemo.Domain;
using DockerCrudDemo.Domain.Events;
using Microsoft.Extensions.Logging;

namespace DockerCrudDemo.Application;

public class ProductService
{
    private readonly IProductRepository _repo;
    private readonly IEventPublisher _publisher;
    private readonly ILogger<ProductService> _logger;

    public ProductService(IProductRepository repo, IEventPublisher publisher, ILogger<ProductService> logger)
    {
        _repo = repo;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<List<Product>> GetAllAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Fetching all products");
        var items = await _repo.GetAllAsync(ct);
        _logger.LogInformation("Fetched {Count} products", items.Count);
        return items;
    }

    public Task<Product?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        _logger.LogInformation("Fetching product {ProductId}", id);
        return _repo.GetByIdAsync(id, ct);
    }

    public async Task<Product> CreateAsync(CreateProductDto dto, CancellationToken ct = default)
    {
        _logger.LogInformation("Creating product Name={Name} Price={Price} Stock={Stock}", dto.Name, dto.Price, dto.Stock);
        var product = new Product { Name = dto.Name, Price = dto.Price, Stock = dto.Stock };
        var created = await _repo.AddAsync(product, ct);
        _logger.LogInformation("Created product {ProductId}, publishing ProductCreated event", created.Id);
        await _publisher.PublishAsync(
            new ProductCreated(created.Id, created.Name, created.Price, created.Stock, DateTime.UtcNow), ct);
        return created;
    }

    public async Task<bool> UpdateAsync(int id, UpdateProductDto dto, CancellationToken ct = default)
    {
        _logger.LogInformation("Updating product {ProductId}", id);
        var ok = await _repo.UpdateAsync(
            new Product { Id = id, Name = dto.Name, Price = dto.Price, Stock = dto.Stock }, ct);
        if (ok)
        {
            _logger.LogInformation("Updated product {ProductId}, publishing ProductUpdated event", id);
            await _publisher.PublishAsync(
                new ProductUpdated(id, dto.Name, dto.Price, dto.Stock, DateTime.UtcNow), ct);
        }
        else
        {
            _logger.LogWarning("Update failed: product {ProductId} not found", id);
        }
        return ok;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        _logger.LogInformation("Deleting product {ProductId}", id);
        var ok = await _repo.DeleteAsync(id, ct);
        if (ok)
        {
            _logger.LogInformation("Deleted product {ProductId}, publishing ProductDeleted event", id);
            await _publisher.PublishAsync(new ProductDeleted(id, DateTime.UtcNow), ct);
        }
        else
        {
            _logger.LogWarning("Delete failed: product {ProductId} not found", id);
        }
        return ok;
    }
}

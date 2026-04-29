using DockerCrudDemo.Domain;
using DockerCrudDemo.Domain.Events;

namespace DockerCrudDemo.Application;

public class ProductService
{
    private readonly IProductRepository _repo;
    private readonly IEventPublisher _publisher;

    public ProductService(IProductRepository repo, IEventPublisher publisher)
    {
        _repo = repo;
        _publisher = publisher;
    }

    public Task<List<Product>> GetAllAsync(CancellationToken ct = default) => _repo.GetAllAsync(ct);

    public Task<Product?> GetByIdAsync(int id, CancellationToken ct = default) => _repo.GetByIdAsync(id, ct);

    public async Task<Product> CreateAsync(CreateProductDto dto, CancellationToken ct = default)
    {
        var product = new Product { Name = dto.Name, Price = dto.Price, Stock = dto.Stock };
        var created = await _repo.AddAsync(product, ct);
        await _publisher.PublishAsync(
            new ProductCreated(created.Id, created.Name, created.Price, created.Stock, DateTime.UtcNow), ct);
        return created;
    }

    public async Task<bool> UpdateAsync(int id, UpdateProductDto dto, CancellationToken ct = default)
    {
        var ok = await _repo.UpdateAsync(
            new Product { Id = id, Name = dto.Name, Price = dto.Price, Stock = dto.Stock }, ct);
        if (ok)
            await _publisher.PublishAsync(
                new ProductUpdated(id, dto.Name, dto.Price, dto.Stock, DateTime.UtcNow), ct);
        return ok;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var ok = await _repo.DeleteAsync(id, ct);
        if (ok)
            await _publisher.PublishAsync(new ProductDeleted(id, DateTime.UtcNow), ct);
        return ok;
    }
}

using DockerCrudDemo.Application;
using DockerCrudDemo.Domain;
using Microsoft.EntityFrameworkCore;

namespace DockerCrudDemo.Infrastructure;

public class ProductRepository : IProductRepository
{
    private readonly AppDbContext _db;
    public ProductRepository(AppDbContext db) => _db = db;

    public Task<List<Product>> GetAllAsync(CancellationToken ct = default) =>
        _db.Products.AsNoTracking().OrderBy(p => p.Id).ToListAsync(ct);

    public Task<Product?> GetByIdAsync(int id, CancellationToken ct = default) =>
        _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<Product> AddAsync(Product product, CancellationToken ct = default)
    {
        _db.Products.Add(product);
        await _db.SaveChangesAsync(ct);
        return product;
    }

    public async Task<bool> UpdateAsync(Product product, CancellationToken ct = default)
    {
        var existing = await _db.Products.FirstOrDefaultAsync(p => p.Id == product.Id, ct);
        if (existing is null) return false;
        existing.Name = product.Name;
        existing.Price = product.Price;
        existing.Stock = product.Stock;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var existing = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (existing is null) return false;
        _db.Products.Remove(existing);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}

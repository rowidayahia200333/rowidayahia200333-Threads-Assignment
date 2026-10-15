using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class ProductRepository
{
    private readonly JsonFileStore _fileStore;

    // أداة حماية لمنع الـ Race Condition والتضارب أثناء حجز المخزون (Thread Safety)
    private static readonly SemaphoreSlim _stockLock = new(1, 1);

    public ProductRepository(JsonFileStore fileStore)
    {
        _fileStore = fileStore;
    }

    private static async Task SimulateDbWriteDelayAsync(CancellationToken ct)
    {
        await Task.Delay(200, ct); // Open connection
        await Task.Delay(100, ct); // Begin transaction
        await Task.Delay(400, ct); // Update stock
        await Task.Delay(200, ct); // Commit
    }

    private static async Task SimulateDbQueryDelayAsync(CancellationToken ct)
    {
        await Task.Delay(200, ct);
        await Task.Delay(Random.Shared.Next(300, 601), ct);
    }

    public async Task<Product?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await SimulateDbQueryDelayAsync(ct);
        var products = await _fileStore.ReadAsync<Product>(DataFiles.Products, ct);
        return products.FirstOrDefault(p => p.Id == id);
    }

    public async Task<List<Product>> GetAllAsync(CancellationToken ct = default)
    {
        await SimulateDbQueryDelayAsync(ct);
        return await _fileStore.ReadAsync<Product>(DataFiles.Products, ct);
    }

    // الحجز الآمن للمخزون (Task 8.3)
    public async Task<bool> ReserveStockAsync(int productId, int qty, CancellationToken ct = default)
    {
        await _stockLock.WaitAsync(ct);
        try
        {
            await SimulateDbQueryDelayAsync(ct);
            var products = await _fileStore.ReadAsync<Product>(DataFiles.Products, ct);
            var product = products.FirstOrDefault(p => p.Id == productId);

            if (product == null || product.StockQuantity < qty)
            {
                return false;
            }

            await SimulateDbWriteDelayAsync(ct);
            product.StockQuantity -= qty;

            await _fileStore.WriteAsync(DataFiles.Products, products, ct);
            return true;
        }
        finally
        {
            _stockLock.Release();
        }
    }

    // إرجاع المخزون في حال فشل الطلب لاحقاً
    public async Task ReleaseStockAsync(int productId, int qty, CancellationToken ct = default)
    {
        await _stockLock.WaitAsync(ct);
        try
        {
            var products = await _fileStore.ReadAsync<Product>(DataFiles.Products, ct);
            var product = products.FirstOrDefault(p => p.Id == productId);
            if (product != null)
            {
                product.StockQuantity += qty;
                await _fileStore.WriteAsync(DataFiles.Products, products, ct);
            }
        }
        finally
        {
            _stockLock.Release();
        }
    }

    public async Task UpdateAsync(List<Product> products, CancellationToken ct = default)
    {
        await _stockLock.WaitAsync(ct);
        try
        {
            await SimulateDbWriteDelayAsync(ct);
            await _fileStore.WriteAsync(DataFiles.Products, products, ct);
        }
        finally
        {
            _stockLock.Release();
        }
    }
}
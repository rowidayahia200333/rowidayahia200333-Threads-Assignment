using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class CustomerRepository
{
    private readonly JsonFileStore _fileStore;

    public CustomerRepository(JsonFileStore fileStore)
    {
        _fileStore = fileStore;
    }

    // محاكاة تأخيرات قاعدة البيانات حسب القسم 6.5
    private static async Task SimulateDbQueryDelayAsync(CancellationToken ct)
    {
        // Open connection: 200ms
        await Task.Delay(200, ct);
        // Execute query: 300-600ms
        int queryTime = Random.Shared.Next(300, 601);
        await Task.Delay(queryTime, ct);
    }

    public async Task<Customer?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        // طباعة اللوج بالشكل المطلوب في الدويكيومنت
        System.Console.WriteLine($"[{System.DateTime.Now:HH:mm:ss.fff}] [Thread {Environment.CurrentManagedThreadId}] CustomerRepository -> Opening connection...");

        await SimulateDbQueryDelayAsync(ct);

        System.Console.WriteLine($"[{System.DateTime.Now:HH:mm:ss.fff}] [Thread {Environment.CurrentManagedThreadId}] CustomerRepository -> Executing query for Customer #{id}...");

        var customers = await _fileStore.ReadAsync<Customer>(DataFiles.Customers, ct);
        var customer = customers.FirstOrDefault(c => c.Id == id);

        if (customer != null)
        {
            System.Console.WriteLine($"[{System.DateTime.Now:HH:mm:ss.fff}] [Thread {Environment.CurrentManagedThreadId}] CustomerRepository -> Customer #{id} found ({customer.Name})");
        }
        else
        {
            System.Console.WriteLine($"[{System.DateTime.Now:HH:mm:ss.fff}] [Thread {Environment.CurrentManagedThreadId}] CustomerRepository -> Customer #{id} not found.");
        }

        return customer;
    }
}
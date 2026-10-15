namespace rowidayahia200333_Threads_Assignment.InvoiceExport;

using System.Diagnostics;

//orderDatabase

sealed class OrderDatabase
{
    readonly List<Order> _pending =
    [
        new(1001, 1, 240.00m, "Zooba"),
        new(1002, 2, 180.50m, "Koshary El Tahrir"),
        new(1003, 3, 95.00m, "Hawawshi House"),
        new(1004, 1, 310.00m, "Zooba"),
        new(1005, 4, 150.00m, "Fattah Street"),
    ];

    public async Task<List<Order>> GetPendingOrdersAsync()
    {
       await Task.Delay(800);
        return _pending.ToList();
    }

   public async Task<Order?> GetOrderByIdAsync(int id)
    {
       await Task.Delay(400);
        return _pending.FirstOrDefault(o => o.Id == id);
    }
}


// CustomerFileStore

sealed class CustomerFileStore
{
    readonly string _folder;

    public CustomerFileStore(string folder) => _folder = folder;

    public async Task< CustomerProfile> ReadProfileAsync(int customerId)
    {
        string path = Path.Combine(_folder, $"customer-{customerId}.txt");
        await Task.Delay(300);
        string text = await File.ReadAllTextAsync(path);
        string[] parts = text.Split('|');
        return new CustomerProfile(int.Parse(parts[0]), parts[1], parts[2]);
    }
}

//InvoiceFileWriter

sealed class InvoiceFileWriter
{
    readonly string _folder;

    public InvoiceFileWriter(string folder) => _folder = folder;

    public async Task<string> WriteInvoiceAsync(Order order, CustomerProfile customer)
    {
        Directory.CreateDirectory(_folder);
        string path = Path.Combine(_folder, $"invoice-{order.Id}.txt");

        string body =
            $"""
            SwiftBite Invoice
            -----------------
            Order:      {order.Id}
            Restaurant: {order.Restaurant}
            Customer:   {customer.Name} <{customer.Email}>
            Total:      {order.Total:C}
            """;

        await Task.Delay(300);
       await File.WriteAllTextAsync(path, body);
        return path;
    }
}

//InvoiceExportService

sealed class InvoiceExportService
{
    readonly OrderDatabase _db;
    readonly CustomerFileStore _customers;
    readonly InvoiceFileWriter _invoices;

    public InvoiceExportService(OrderDatabase db, CustomerFileStore customers, InvoiceFileWriter invoices)
    {
        _db = db;
        _customers = customers;
        _invoices = invoices;
    }

    public async Task ExportAllPendingAsync()
    {
        var watch = Stopwatch.StartNew();
        List<Order> orders = await _db.GetPendingOrdersAsync();

       
        var exportTasks = orders.Select(async order =>
        {
            Order fresh = await _db.GetOrderByIdAsync(order.Id)
                ?? throw new InvalidOperationException($"Order {order.Id} missing");

            CustomerProfile customer = await _customers.ReadProfileAsync(fresh.CustomerId);
            string path = await _invoices.WriteInvoiceAsync(fresh, customer);

            
            lock (Console.Out)
            {
                Console.WriteLine($"Wrote {path}");
            }
        });

     
        await Task.WhenAll(exportTasks);

        watch.Stop();
        Console.WriteLine($"Export finished in {watch.Elapsed.TotalSeconds:F1} s");
    }
}

sealed record Order(int Id, int CustomerId, decimal Total, string Restaurant);

sealed record CustomerProfile(int Id, string Name, string Email);


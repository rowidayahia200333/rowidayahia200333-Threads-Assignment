using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

public class InvoiceGenerator
{
    private readonly JsonFileStore _fileStore;

    public InvoiceGenerator(JsonFileStore fileStore)
    {
        _fileStore = fileStore;
    }

    public async Task GenerateAsync(Order order, CancellationToken ct = default)
    {
        // استخدام Task.Run لأن العملية محاكاة لمعالجة ثقيلة على المعالج (CPU-bound)
        await Task.Run(() =>
        {
            // محاكاة لوب ثقيلة لتوليد ملف الـ PDF (20 مليون تكرار)
            long dummyHash = 0;
            for (int i = 0; i < 20_000_000; i++)
            {
                dummyHash += i ^ (i >> 1);
            }

            // كتابة الفاتورة في المجلد المخصص
            string dir = Path.Combine("Data", "invoices");
            Directory.CreateDirectory(dir);
            string filePath = Path.Combine(dir, $"invoice_{order.Id}.txt");

            string content = $"=== INVOICE #{order.Id} ===\n" +
                             $"Customer ID: {order.CustomerId}\n" +
                             $"Total Amount: {order.TotalAmount:C}\n" +
                             $"Status: {order.Status}\n" +
                             $"Generated Hash: {dummyHash}\n" +
                             $"Date: {DateTime.UtcNow}\n";

            File.WriteAllText(filePath, content);
        }, ct);
    }
}
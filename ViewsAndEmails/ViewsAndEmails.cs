using System.Collections.Concurrent;
namespace rowidayahia200333_Threads_Assignment.ViewsAndEmails;


sealed class AnalyticsAndEmailJobs
{
    public readonly ConcurrentDictionary<string, int> Views = new();
    public readonly ConcurrentQueue<string> EmailJobs = new();
    public readonly List<string> SentEmails = new();

    readonly object _sentSync = new();
    public volatile bool ProducersDone;

    public void RecordView(string product)
    {
        Views.AddOrUpdate(product,
            addValue:1,
            updateValueFactory:(_,oldValue)=> oldValue+1
            );
    }

    public void PlaceOrder(int orderId)
    {
        EmailJobs.Enqueue($"Email: order {orderId}");
    }

    public void EmailWorker()
    {
        while (true)
        {
            if (EmailJobs.TryDequeue(out string? job))
            {
                lock (_sentSync)
                    SentEmails.Add(job);
                continue;
            }

            if (ProducersDone && EmailJobs.IsEmpty)
            {
                break;
            }

            Thread.Sleep(1);
        }
    

    }
}

using System;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine("\n[System] Cancellation requested...");
        };

        string phase = args.Length > 0 ? args[0].ToLower() : "phase4";

        Console.WriteLine($"=== E-Commerce Order Processing System ({phase.ToUpper()}) ===");

        var fileStore = new JsonFileStore();

        try
        {
            switch (phase)
            {
                case "phase1":
                case "--phase1":
                    await Phase1Runner.RunAsync(fileStore, cts.Token);
                    break;

                case "phase2":
                case "--phase2":
                    await Phase2Runner.RunAsync(fileStore, cts.Token);
                    break;

                case "phase3":
                case --phase3":
                    await Phase3Runner.RunAsync(fileStore, cts.Token);
                    break;

                case "phase4":
                case "--phase4":
                    await Phase4Runner.RunAsync(fileStore, cts.Token);
                    break;

                default:
                    Console.WriteLine($"[Error] Unknown phase '{phase}'. Please use: phase1, phase2, phase3, or phase4.");
                    break;
            }

            Console.WriteLine("\n=== Program Execution Finished Successfully ===");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\n[System] Execution cancelled gracefully.");
        }
    }
}
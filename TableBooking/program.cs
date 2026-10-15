namespace rowidayahia200333_Threads_Assignment.TableBooking;


class Program
{
    static void Main(string[] args)
    {
        var booking = new FridayNightBooking();
        Thread[] customers = new Thread[40];

        for (int i = 0; i < customers.Length; i++)
        {
            string name = $"Customer-{i + 1:00}";
            customers[i] = new Thread(() => booking.Book(name));
            customers[i].Start();
        }

        foreach (Thread t in customers)
            t.Join();

        Console.WriteLine();
        Console.WriteLine($"Booked={booking.Booked}, TablesLeft={booking.TablesLeft}");

    }
}


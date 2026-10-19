namespace rowidayahia200333_Threads_Assignment.TableBooking;


sealed class FridayNightBooking
{
    public int TablesLeft { get; private set; } = 5;
    public int Booked { get; private set; }
    private readonly Lock loc = new();


    public void Book(string customerName)
    {
        lock (loc)
        {
            if (TablesLeft <= 0)
                return;

            Thread.Sleep(20);

            TablesLeft--;
            Booked++;
            Console.WriteLine($"{customerName} booked a table. Left={TablesLeft}");
        }
    }
}

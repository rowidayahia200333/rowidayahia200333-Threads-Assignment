namespace rowidayahia200333_Threads_Assignment.MenuViews;


sealed class MenuViewCounter
{
    public int Views;

    public void RegisterView()
    {
        Interlocked.Increment(ref Views);
    }
}
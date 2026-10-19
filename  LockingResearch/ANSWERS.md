# Assignment 03 — Answers

Student name:  Rowida Yahia
Group / ID: Mon&Thur 

1.What is System.Threading.Monitor used for?
تستخدم لمنع حدوث Race Conditions بسبب التعديل المتزامن وغير الآمن على المتغيرات المشتركة في نفس الوقت وحدوث lost update in information and data 

2.What does the C# lock statement compile to?
بيستخدم عشان مفيش اكتر من thread واحد يدخل في نفس الوقت ويحصل مشاكل زي Race Conditions و lost update بحيث من جواه كمان بيعمل try and catch عشان حتي لو حصل exception يتحرر القفل بردوا 

3.List at least two differences between using lock and calling Monitor APIs directly
Monitor implicit and atomatic lock and less control that is atomatic and more simple and advanced he can calculate the waiting time , while lock explicit and manual lock and more flixible  and i can TryLock first but more explicit control by myself 

4.When might a developer use Monitor.TryEnter, Monitor.Pulse, or Monitor.Wait instead of a plain
lock?
The problem is that it will block indefinitely if the monitor is unavailable. I avoid writing production code that calls APIs which might block forever, because if you hit a deadlock scenario, you'll never recover. And even though you will normally want to design your code in such a way as to avoid deadlock, it's still best to be able to at least detect it if it should occur. (Ideally you want to be robust enough to recover from it too.)
Monitor.Pulse: في سيناريوهات الإنتاج والاستهلاك (Producer-Consumer) أو عندما يحتاج خيط ما إلى تعليق تنفيذه بانتظار حدوث شرط معين يتم تحقيقه بواسطة خيط آخر يقوم بإرسال إشارة (Pulse) لتنبيهه واستئناف العمل.

5.Write the same critical section twice: once with lock, once with Monitor.Enter / Monitor.Exit.

//lock
private readonly Lock obj = new();
public void CriticalSectionWithLock(){
	lock (obj)
	{ }
}

//monitor
private readonly object _lockObj = new();

public void SafeIncrementWithMonitor()
{
    bool lockTaken = false;
    try
    {
        Monitor.Enter(_lockObj, ref lockTaken);
        counter++;
    }
    finally
    {
        if (lockTaken)
        {
            Monitor.Exit(_lockObj);
        }
    }
}


Links:

# What is System.Threading.Monitor used for?
1- Monitor Class (System.Threading) | Microsoft Learn https://share.google/sc8M1rSlxTVjtptI9
2- C# - Advantages of System.Threading.Monitor to control thread - Stack Overflow https://share.google/FwVtGQhm9uSxn88xF

# What does the C# lock statement compile to?
1- The lock statement - synchronize access to shared resources - C# reference | Microsoft Learn https://share.google/EDzfYsoVeqNMXVg5n
2- C# - Lock in Thread - GeeksforGeeks https://share.google/Al29u7Xp5KBLoTtwq

# List at least two differences between using lock and calling Monitor APIs directly
1- Difference Between Lock and Monitor in Java Concurrency - GeeksforGeeks https://share.google/dXkD7IB1Ia2EEL5Lx
2- locking - In Java, what is the difference between a monitor and a lock - Stack Overflow https://share.google/DzaoCOuO1WOIIq0gq

# When might a developer use Monitor.TryEnter, Monitor.Pulse, or Monitor.Wait instead of a plain
lock?
1- Monitor.TryEnter Method (System.Threading) | Microsoft Learn https://share.google/PQXdBPgJHp9WkPErz
2- Monitor.TryEnter, lock, using, and Deadlock https://share.google/sHs1gXeYxW0Hq55Jk


​1. What is the difference between starting a task and awaiting it?
​Starting a task (تشغيل المهمة): بيعني إرسال المهمة للـ Thread Pool أو بدئها في الخلفية (Task.Run أو استدعاء ميثود async)، وده بيخلي التنفيذ يكمل في الكود الحالي من غير ما يستنى النتيجة فوراً (Non-blocking).
​Awaiting it (انتظارها بـ await): معناه تعليق تنفيذ الميثود الحالية مؤقتاً لحد ما المهمة تخلص وترجع النتيجة، مع إرجاع التحكم للـ Caller، وبعد ما تخلص بينزل الكود يكمل تنفيذ الباقي بأمان من غير ما يحصل حظر للـ Thread (Blocking).
​
2. Does every Task create a new thread? Does Task.Delay() use a thread while waiting?
​No: مش كل Task بتبني Thread لوحدها؛ الـ Tasks بتستغل الـ Thread Pool وتدير الـ Threads بكفاءة عالية.
​No (Task.Delay): لا تستهلك أو تحجز Thread أثناء الانتظار؛ دي بتبعت Timer لـ OS، والـ Thread بيرجع يشتغل في حاجات تانية لحد ما الوقت يخلص وبعدين يكمل.

​3. What happens if you call .Result or .Wait() on an unfinished task instead of await? Why is it dangerous? Write a small separate demo method — the only place .Result / .Wait() is allowed.
بيحصل Deadlock (خصوصاً في تطبيقات الـ UI أو ASP.NET التقليدية) لأن الميثود بتحجز الـ Thread الحالي وبتستنى الـ Thread التاني يخلص في نفس السياق (SynchronizationContext)، وده بيقفل الدنيا تماماً.
الاستثناء المسموح الوحيد: في الـ Main method (لأنها synchronous ولا تدعم الـ await في الإصدارات القديمة أو لو بنستدعي كود sync بالكامل)، أو في الـ Unit Tests التزامنية.
public static void SafeMainDemo()
{
    // المكان الوحيد المقبول أحياناً لو مش قادرين نستخدم await Main
    var task = Task.Run(async () => { await Task.Delay(100); return 42; });
    int result = task.Result; // استخدام .Result هنا فقط
}

4. What happens to an exception thrown inside a task that is never awaited?
​الـ Exception بيتحول إلى UnobservedTaskException، وفي الإصدارات الحديثة من .NET ممكن يتسبب في إنهاء الـ Process لو متمش التقاطه أو مراقبته، وعادةً بيفضل مخفي لحد ما يتم عمل .Wait() أو await أو فحص الـ Task.Exception.

​5. When should you use Task.Run(), and when should you avoid it?
​Use it: مع العمليات الثقيلة التي تستهلك المعالج بشكل مكثف (CPU-bound operations)، زي معالجة الصور، الحسابات الرياضية الضخمة، أو محاكاة الـ PDF في مشروعنا (InvoiceGenerator).
​Avoid it: مع عمليات الإدخال والإخراج (I/O-bound operations مثل قراءة الملفات، قواعد البيانات، والـ APIs) لأن لغة C# بتوفر لها أصلًا ميثودز async جاهزة ومش محتاجة Task.Run تستهلك Thread عالفاضي في الـ Thread Pool.
​
6. What is the difference between Task and Task<T>?
​Task: بيمثل عملية أو مهمة بتنفذ شغل في الخلفية من غير ما ترجع قيمة (مثل void في الـ synchronous).
​Task<T>: بيمثل مهمة بتنفذ شغل في الخلفية وترجع قيمة من نوع T عند اكتمالها (مثل Task<decimal> لحساب المجموع أو الأسعار).

​7. Why can a lock block not contain an await?
​لأن الـ lock (اللي بيعتمد على Monitor.Enter و Monitor.Exit) بيقفل الـ Thread الحالي تماماً، بينما الـ await بيعلق المهمة وينقل التحكم ويخلي الـ Thread يروح يشتغل في حاجة تانية. لو الـ Thread اتغير أثناء الـ await، الـ lock هيضيع أو هيحصل استثناء لأن الـ lock مرتبط بالـ Thread اللي عمله (Thread-affine). البديل الحديث للتعامل مع الـ async مع القفل هو استخدام SemaphoreSlim.WaitAsync().

​8. Why did the unprotected stock reservation oversell, and how did you prevent it?
​Why it oversold: لأن أكتر من طلب دخلوا في نفس اللحظة وقروا إن المخزون بواحد (1)، فكل واحد فيهم افتكر إنه يقدر يشتري، وفضلوا يكملوا مع بعض في نفس الوقت (Race Condition) من غير تنسيق.
​How we prevented it: عن طريق استخدام SemaphoreSlim (أو ميثود الحجز الآمن المحمية)، اللي بتسمح لطلب واحد فقط إنه يدخل منطقة الحجز وتعديل المخزون (Critical Section)، والباقيين يستنوا لحد ما الأول يخلص تماماً، وبكده نمنع التضارب نهائياً.


//phase 3

1. Find the problem in this code:
المشكلة: في سطر إرسال البريد الإلكتروني _emailService.SendConfirmationAsync(order, customer); لم يتم استخدام await (وتم تجاهل الـ Task Return). هذا يُسمى Fire-and-forget، مما يترتب عليه الآتي:  
الميثود ستستكمل التنفيذ وتطبع Order processing finished وتنهي عملها قبل أن ينتهي إرسال الإيميل فعلياً في الخلفية.  
لو حدث استثناء (Exception) داخل ميثود الإيميل، فلن يتم التقاطه أبداً عبر الـ try-catch العادية، وسيتحول إلى UnobservedTaskException قد يؤدي لإيقاف التطبيق أو ضياع الخطأ.

2. Show what happens if the email is still being sent when the method returns, or when the console app exits:
لو التطبيق عبارة عن Console Application وانتهت الميثود الرئيسية (Main)، سيتوقف البرنامج عن العمل فوراً ويغلق الـ Process، وبالتالي سيتم إلغاء عملية إرسال الإيميل في الخلفية قبل أن تكتمل.

3. Show what happens if the email operation throws an exception:
سيتم إهمال الخطأ وعدم رؤيته، ويؤدي ذلك إلى عدم معرفة النظام بفشل الإرسال، وقد يتسبب في حدوث UnobservedTaskException في الـ .NET Runtime.

4. Fix the implementation. Show the console output before and after the fix:
التصحيح (Fix): إضافة await قبل استدعاء خدمة الإيميل لضمان الانتظار حتى اكتمال الإرسال بأمان:
public async Task ProcessOrderAsync(Order order, Customer customer)
{
    await _orderRepository.SaveAsync(order);
    await _emailService.SendConfirmationAsync(order, customer); // تم وضع await بنجاح
    Console.WriteLine("Order processing finished");
}

# When is await necessary?
يكون ضرورياً عندما نكون بحاجة لاستخدام النتيجة (Result) أو التأكد من اكتمال تنفيذ المهمة والتعامل مع استثناءاتها قبل الانتقال للخطوة التالية في الكود.
When is fire-and-forget acceptable (if ever)?
مقبول في حالات نادرة جداً وغير حرجة، مثل إرسال إحصائيات تحليلية (Telemetry/Logging) في الخلفية لا يهمنا إن نجحت أو فشلت ولا تؤثر على حالة النظام أو العميل.
Why is async void dangerous, and why is it acceptable for event handlers?
خطورته: لأنه لا يعود بـ Task، وبالتالي لا يمكن عمل await له، ولو حدث داخله استثناء فلن يتم التقاطه بواسطة الـ try-catch الخارجية وسيؤدي لانهيار التطبيق فوراً (Crash).
استثنائه المقبول: مقبول فقط في Event Handlers (مثل أزرار الواجهات UI) لأن توقيع الحدث (Event Signature) يتطلب أن يكون void


//phaase 4

​1. For each of Tasks 10.1–10.4: which tool did you use, and why can't "start, then await one by one" meet the requirement?
​Task 10.1 (Live Progress): استخدام Task.WhenAny. طريقة "الانتظار واحد تلو الآخر" (one-by-one) لن تفي بالغرض لأنها ستجبرنا على انتظار الطلب الأول حتى لو انتهى الطلب العاشر قبله، بينما Task.WhenAny تتيح لنا التقاط أي مهمة تنتهي فوراً أياً كانت ترتيبها.  
​Task 10.2 (Customer Dashboard): استخدام عدة مهام معاً بـ Task.Run ثم معالجة الأخطاء بجمعها. الانتظار التسلسلي (واحد تلو الآخر) سيجعل إجمالي الوقت هو مجموع أوقات كل الاستعلامات بدلاً من وقت أبطأ استعلام منفرد.  
​Task 10.3 (Fastest Shipping Quote): استخدام Task.WhenAny مع السباق بين شركات الشحن. الطريقة التسلسلية لا تسمح بالمنافسة الزمنية ولن تأخذ الاستجابة الأسرع.  
​Task 10.4 (Fraud & Throttling): استخدام Parallel.For و ParallelOptions للتحكم في عدد الخيوط المتاحة. الطرق التسلسلية لا تستغل جميع أنوية المعالج (CPU Cores) في المهام الثقيلة.  
​
2. In 10.2, when several loads fail, how do you get every exception? What does a plain await give you?
​الـ await العادي يرمي أول استثناء فقط ويخفي باقي الأخطاء. للحصول على كافة الأخطاء، نقوم بتغليف الاستثناءات داخل قائمة واستخدام AggregateException لجمعها وإرجاعها معاً.  
​
3. In 10.3, what happens to the losing carriers if you never cancel them?
​ستظل جارية في الخلفية تستهلك موارد الـ Thread Pool وذاكرة النظام ووقت المعالجة بلا داعٍ حتى بعد انتهاء الحاجة لنتائجها، لذا استخدام CancellationTokenSource لإلغائها فور اختيار الأسرع أمر ضروري.  
​
4. Why does 10.4 A get faster with more CPU cores, while 10.4 B does not depend on the number of cores?
​10.4 A (Fraud Scoring): عملية تعتمد كلياً على المعالج (CPU-bound)، وكلما زادت الأنوية (Cores)، كلما أمكن توزيع الحسابات الرياضية وتنفيدها بالتوازي الحقيقي.
​10.4 B (Email Throttling): عملية إدخال وإخراج (I/O-bound)، تعتمد على الانتظار وشبكة الاتصال (Task.Delay) ولا تستهلك قدرات المعالج، بل تعتمد على تنظيم وتحديد التزامن (MaxDegreeOfParallelism).  
​
5. What is the difference between parallel work (10.4 A) and concurrent asynchronous work (10.1)?
​Parallel Work: يوزع مهام الـ CPU على عدة أنوية حقيقية في نفس الوقت لتنفيذ العمليات الحسابية الثقيلة بالتوازي التام.  
​Concurrent Asynchronous Work: يعتمد على معالجة مهام الـ I/O بالتزامن عبر تبديل التحكم بالـ await على خيوط قليلة دون الحاجة لزيادة الأنوية أو حظر الخيوط.  
​
6. Show a cancelled run (--cancel-after) and explained what happened to the orders, the stock, and the payments:
​عند تفعيل الإلغاء (CancellationToken):
​الطلبات (Orders): يتم حفظ حالتها الحالية كـ Failed أو يتم إيقاف المعالجة الحالية لها بأمان.
​المخزون (Stock): يتم تشغيل آليات التعويض (ReleaseStockAsync) لإعادة المخزون المحجوز لحالته الأصلية فوراً.
​المدفوعات (Payments): يتم إيقاف أي عمليات دفع جارية، وإن تمت عملية دفع وتوقف النظام بعدها، تُنفذ دالة الاسترجاع (RefundAsync) لضمان عدم ضياع أموال العميل.




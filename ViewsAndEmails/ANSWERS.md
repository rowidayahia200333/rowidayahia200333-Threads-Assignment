the output Before:
Views expected=50000, actual=43870

# describe the failures you see:
الدالة RecordView بتعدل على Dictionary عادي (Views). والـ Dictionary العادي في C# غير آمن للاستخدام المتزامن مع عدة خيوط (not thread-safe)، وده بيؤدي لحدوث تداخل وضياع تحديثات المشاهدات أو حدوث استثناءات (Exceptions). حتى لو استخدمنا Dictionary آمن، فالعملية نفسها (ContainsKey ثم Add أو التعديل) ليست ذرية (Not atomic).
في EmailWorker، عملية التحقق if (EmailJobs.Count > 0) ثم job = EmailJobs.Dequeue() بيحصل بينهم فاصل زمني بسيط (Race condition)، فممكن خيطين يشوفوا إن الـ Count أكبر من صفر وفي نفس اللحظة أحدهم يحاول يسحب العنصر فيحصل InvalidOperationException أو يتم سحب العنصر مرتين أو بشكل خاطئ.



 the output After:
 Views expected=50000, actual=50000
Email jobs placed=20, unique sent=20, total sends=20

 # why making a collection safer does not automatically make the business rule safe?
 يمنع حدوث تلف في بنية البيانات الداخلية للـ Collection فقط (مثل الـ Dictionary أو Queue)، ولكنه لا يحمي قواعد العمل (Business Rules) التي تتكون من عدة خطوات متتالية ومترابطة (Composite operations) مثل التحقق من وجود المفتاح ثم قراءته ثم التعديل عليه


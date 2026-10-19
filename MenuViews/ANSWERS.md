
the output Before:
Expected: 400000
Actual:   339007

# why the numbers disagree?
بسبب حدوث Race Conditions بسبب التعديل المتزامن وغير الآمن على المتغيرات المشتركة (Views) من عدة Threads في نفس الوقت وهي اصلا كذا عملية قراءة و زياده القيمة بواحد و كتابة القيمة عليها 


the output After:
Expected: 400000
Actual:   400000

#  when you would protect this kind of update differently?
Interlocked.Increment لأننا نتعامل مع عداد مفرد وبسيط (Single counter)، وهي الأسرع والأكثر كفاءة في الأداء (Performance). أما إذا كانت العملية تتضمن تحديث أكثر من متغير معاً أو شروط منطقية معقدة تتطلب كتلة أكواد كاملة (Critical Section)، فحينها نفضل استخدام lock أو أدوات تزامن أخرى

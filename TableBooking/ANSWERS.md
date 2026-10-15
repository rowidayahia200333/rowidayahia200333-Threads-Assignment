the problem:
حدوث Race Conditions بسبب التعديل المتزامن وغير الآمن على المتغيرات المشتركة (TablesLeft و Booked) من عدة Threads في نفس الوقت.


and fix that by:
lock for prevent several threads to do the process and modification in the same time

the output:

Customer-01 booked a table. Left=4
Customer-03 booked a table. Left=3
Customer-02 booked a table. Left=2
Customer-04 booked a table. Left=1
Customer-05 booked a table. Left=0

Booked=5, TablesLeft=0
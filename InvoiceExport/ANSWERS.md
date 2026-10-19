 جواه thread.sleep وده بيعمل block وبيخلي ال thread واقف ومنتظره يخلص الاول عشان يكمل شغل 
 لو عندي كده كتير او customers كتير هيبقي بطئ جدا مع توقيف ال thread لكل واحد كده ومش بنستفاد منه 
 The foreach loop processes orders strictly one by one (sequentially). If there are numerous pending orders, the total execution time scales linearly as the sum of all individual delays and file operations

 ### What Was Changed:
 Refactored all data access and file operations in OrderDatabase, CustomerFileStore, and InvoiceFileWriter to use asynchronous methods (Task.Delay, File.ReadAllTextAsync, File.WriteAllTextAsync).
 Replaced  foreach loop with Task.WhenAll(). This allows all pending orders to be fetched, processed, and written concurrently rather than one after another.

 ### Console Output Comparison:

  Before:

  
[Running] scriptcs "c:\Users\Lenovo\AdvancedCsharp-Assignment-01-InvoiceExport\InvoiceExport\Program.cs"
'scriptcs' is not recognized as an internal or external command,
operable program or batch file.

[Done] exited with code=1 in 0.036 seconds

  After:

  Wrote C:\Users\Lenovo\rowidayahia200333-Threads-Assignment\InvoiceExport\invoices\invoice-1005.txt
Wrote C:\Users\Lenovo\rowidayahia200333-Threads-Assignment\InvoiceExport\invoices\invoice-1004.txt
Wrote C:\Users\Lenovo\rowidayahia200333-Threads-Assignment\InvoiceExport\invoices\invoice-1001.txt
Wrote C:\Users\Lenovo\rowidayahia200333-Threads-Assignment\InvoiceExport\invoices\invoice-1002.txt
Wrote C:\Users\Lenovo\rowidayahia200333-Threads-Assignment\InvoiceExport\invoices\invoice-1003.txt
Export finished in 1.9 s

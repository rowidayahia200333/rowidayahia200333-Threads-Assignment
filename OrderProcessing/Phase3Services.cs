using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

// الاستثناءات المخصصة المطلوبة في Phase 3
public class CustomerNotFoundException : Exception
{
    public CustomerNotFoundException(string message) : base(message) { }
}

public class InvalidCouponException : Exception
{
    public InvalidCouponException(string message) : base(message) { }
}

public class DatabaseWriteException : Exception
{
    public DatabaseWriteException(string message) : base(message) { }
}

public class SmtpUnavailableException : Exception
{
    public SmtpUnavailableException(string message) : base(message) { }
}

// نتيجة عملية الدفع
public class PaymentResult
{
    public bool Success { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

// بوابة الدفع
public class PaymentGateway
{
    private readonly System.Collections.Generic.HashSet<int> _timeoutOrderIds;
    private readonly System.Collections.Generic.HashSet<int> _declineOrderIds;

    public PaymentGateway(IEnumerable<int> timeoutOrderIds, IEnumerable<int> declineOrderIds)
    {
        _timeoutOrderIds = new System.Collections.Generic.HashSet<int>(timeoutOrderIds);
        _declineOrderIds = new System.Collections.Generic.HashSet<int>(declineOrderIds);
    }

    public async Task<PaymentResult> ProcessPaymentAsync(int orderId, decimal amount, CancellationToken ct = default)
    {
        // محاكاة تأخير الدفع (للطلب رقم 5 يحدث تايم أوت 3 ثواني أو 5000ms حسب التكوين)[span_7](start_span)[span_7](end_span)[span_8](start_span)[span_8](end_span)
        int delay = _timeoutOrderIds.Contains(orderId) ? 5000 : 700;

        try
        {
            await Task.Delay(delay, ct);
        }
        catch (OperationCanceledException) when (_timeoutOrderIds.Contains(orderId))
        {
            throw;
        }

        string txId = "TXN-" + Guid.NewGuid().ToString("N")[..12].ToUpper(); [span_9] (start_span)[span_9](end_span)

        // التحقق من رفض البطاقة (للطلب رقم 8)[span_10](start_span)[span_10](end_span)
        if (_declineOrderIds.Contains(orderId))
        {
            var failedResult = new PaymentResult { Success = false, TransactionId = txId, Message = "Card declined by issuer" }; [span_11] (start_span)[span_11](end_span)
            await SavePaymentRecordAsync(orderId, amount, "Declined", txId);
            return failedResult;
        }

        var successResult = new PaymentResult { Success = true, TransactionId = txId, Message = "Captured" };
        await SavePaymentRecordAsync(orderId, amount, "Captured", txId);
        return successResult;
    }

    public async Task RefundAsync(string transactionId, CancellationToken ct = default)
    {
        await Task.Delay(300, ct);
        // تحديث سجل الدفع إلى Refunded في payments.json
        string path = Path.Combine("Data", "payments.json");
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            // تحديث السجل المرتبط بـ transactionId
            // (سيتم تنفيذه بدقة حسب بنية الملفات)
        }
    }

    private async Task SavePaymentRecordAsync(int orderId, decimal amount, string status, string txId)
    {
        string dir = "Data";
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "payments.json");

        var record = new { OrderId = orderId, Amount = amount, Status = status, TransactionId = txId, Timestamp = DateTime.UtcNow };
        var list = new System.Collections.Generic.List<object>();
        if (File.Exists(path))
        {
            string existingJson = File.ReadAllText(path);
            var deserialized = JsonSerializer.Deserialize<System.Collections.Generic.List<object>>(existingJson);
            if (deserialized != null) list = deserialized;
        }
        list.Add(record);
        File.WriteAllText(path, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
    }
}
// خدمة البريد الإلكتروني مع إعادة المحاولة (Task 9.5)
public class EmailService
{
    private readonly System.Collections.Generic.Dictionary<int, int> _failAttempts;

    public EmailService(System.Collections.Generic.Dictionary<int, int> failAttempts)
    {
        _failAttempts = failAttempts;
    }

    public async Task SendConfirmationWithRetryAsync(Order order, Customer customer, CancellationToken ct = default)
    {
        int maxRetries = 3;
        int[] delays = { 500, 1000, 2000 }; [span_12] (start_span)[span_12](end_span)

        for (int attempt = 1; attempt <= 1 + maxRetries; attempt++)
        {
            try
            {
                await Task.Delay(200, ct);

                // فحص ما إذا كان يجب رمي استثناء الفشل بناءً على محاولات الطلب[span_13](start_span)[span_13](end_span)
                if (_failAttempts.ContainsKey(order.Id) && attempt <= _failAttempts[order.Id])
                {
                    throw new SmtpUnavailableException("SMTP server unavailable");
                }

                // نجاح الإرسال -> حفظ في emails.json
                SaveEmailRecord(order.Id, customer.Email, attempt);
                return;
            }
            catch (Exception ex) when (attempt <= maxRetries)
            {
                // انتظار قبل المحاولة التالية[span_14](start_span)[span_14](end_span)
                await Task.Delay(delays[attempt - 1], ct);
            }
            catch (Exception ex)
            {
                // إذا فشلت كل المحاولات (مثل الطلب رقم 17)، يتم حفظه في email_retry.json[span_15](start_span)[span_15](end_span)[span_16](start_span)[span_16](end_span)
                SaveFailedEmailRecord(order.Id, customer.Email, attempt, ex.Message);
                return;
            }
        }
    }

    private void SaveEmailRecord(int orderId, string email, int attempts)
    {
        string dir = "Data";
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "emails.json");

        var record = new { OrderId = orderId, Email = email, Attempts = attempts, Timestamp = DateTime.UtcNow };
        var list = new System.Collections.Generic.List<object>();
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            var des = JsonSerializer.Deserialize<System.Collections.Generic.List<object>>(json);
            if (des != null) list = des;
        }
        list.Add(record);
        File.WriteAllText(path, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void SaveFailedEmailRecord(int orderId, string email, int attempts, string lastError)
    {
        string dir = "Data";
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "email_retry.json");

        var record = new { OrderId = orderId, Email = email, Attempts = attempts, LastError = lastError, Timestamp = DateTime.UtcNow };
        File.WriteAllText(path, JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }));
    }
}
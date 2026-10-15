using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

// الاستثناء المخصص المطلوب في المتطلب R4 لو ملف الـ JSON تالف
public class DataFileCorruptedException : Exception
{
    public string FileName { get; }

    public DataFileCorruptedException(string fileName, string message, Exception innerException)
        : base(message, innerException)
    {
        FileName = fileName;
    }
}

public class JsonFileStore
{
    // بنستخدم SemaphoreSlim عشان نضمن إن مفيش ملفين يتكتبوا في نفس اللحظة ويحصل تضارب (Thread Safety)
    private static readonly SemaphoreSlim _lock = new(1, 1);

    // دالة القراءة العامة لأي ملف JSON
    public async Task<List<T>> ReadAsync<T>(string fileName, CancellationToken cancellationToken = default)
    {
        DataFiles.EnsureDirectoriesExist();
        string fullPath = Path.Combine(DataFiles.DataDirectory, fileName);

        // لو الملف مش موجود، بنرجع لستة فاضية زي ما متطلب R4 بيقول
        if (!File.Exists(fullPath))
        {
            return new List<T>();
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            // قراءة المحتوى بشكل غير متزامن
            string jsonContent = await File.ReadAllTextAsync(fullPath, cancellationToken);

            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                return new List<T>();
            }

            // محاولة تحويل النص لـ الـ List المطلوبة
            return JsonSerializer.Deserialize<List<T>>(jsonContent, JsonConfig.DefaultOptions) ?? new List<T>();
        }
        catch (JsonException ex)
        {
            // لو الملف تالف ومش مفهوم كـ JSON، بنرمي الاستثناء المطلوب R4
            throw new DataFileCorruptedException(fileName, The JSON file '{fileName}' is corrupted., ex);
        }
        finally
        {
            _lock.Release();
        }
    }

    // دالة الكتابة العامة لأي ملف JSON
    public async Task WriteAsync<T>(string fileName, List<T> data, CancellationToken cancellationToken = default)
    {
        DataFiles.EnsureDirectoriesExist();
        string fullPath = Path.Combine(DataFiles.DataDirectory, fileName);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            // تحويل البيانات لنص JSON منظم
            string jsonContent = JsonSerializer.Serialize(data, JsonConfig.DefaultOptions);

            // عشان نضمن متطلب R6 (إن مفيش ملف يبوظ أو يتكتب نصه لو حصل Crash)،
            // بنكتب الأول في ملف مؤقت (Temp file) وبعدين نبدله بالملف الأصلي مرة واحدة
            string tempPath = fullPath + ".tmp";
            await File.WriteAllTextAsync(tempPath, jsonContent, cancellationToken);

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
            File.Move(tempPath, fullPath);
        }
        finally
        {
            _lock.Release();
        }
    }
}
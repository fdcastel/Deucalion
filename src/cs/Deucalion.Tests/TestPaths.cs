namespace Deucalion.Tests;

internal static class TestPaths
{
    /// <summary>
    /// Deletes a directory, retrying briefly. On Windows a SQLite file handle can outlive the
    /// connection's disposal by a few milliseconds, so the first delete may fail with IOException.
    /// Cleanup failures are reported but never fail the test.
    /// </summary>
    public static void DeleteWithRetry(string path, int maxAttempts = 5)
    {
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }

                return;
            }
            catch (IOException ex) when (attempt < maxAttempts)
            {
                Console.WriteLine($"Retrying cleanup of {path} (attempt {attempt}/{maxAttempts}): {ex.Message}");
                Thread.Sleep(50 * attempt);
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Warning: could not delete {path}. {ex.Message}");
                return;
            }
        }
    }
}

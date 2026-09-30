using System.Windows;

namespace KeyFix.Windows;

public sealed class ClipboardTextService(KeyboardInputService keyboardInput)
{
    public async Task<ClipboardReadResult> ReadSelectionAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = ClipboardSnapshot.Capture();
        try
        {
            TryClearClipboard();
            var sequence = NativeMethods.GetClipboardSequenceNumber();
            keyboardInput.SendCopy();

            var changed = await WaitForClipboardChangeAsync(sequence, cancellationToken);
            if (!changed || !Clipboard.ContainsText(TextDataFormat.UnicodeText))
            {
                return new ClipboardReadResult(null, snapshot);
            }

            return new ClipboardReadResult(Clipboard.GetText(TextDataFormat.UnicodeText), snapshot);
        }
        catch
        {
            snapshot.Restore();
            throw;
        }
    }

    private static void TryClearClipboard()
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                Clipboard.Clear();
                return;
            }
            catch when (attempt < 3)
            {
                Thread.Sleep(20);
            }
        }
    }

    private static async Task<bool> WaitForClipboardChangeAsync(uint initialSequence, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            await Task.Delay(25, cancellationToken);
            if (NativeMethods.GetClipboardSequenceNumber() != initialSequence)
            {
                return true;
            }
        }

        return false;
    }
}

public sealed class ClipboardReadResult : IDisposable
{
    private bool _disposed;
    private readonly ClipboardSnapshot _snapshot;

    internal ClipboardReadResult(string? text, ClipboardSnapshot snapshot)
    {
        Text = text;
        _snapshot = snapshot;
    }

    public string? Text { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _snapshot.Restore();
        _disposed = true;
    }
}

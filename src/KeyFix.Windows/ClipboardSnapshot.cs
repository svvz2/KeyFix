using System.Windows;

namespace KeyFix.Windows;

internal sealed class ClipboardSnapshot
{
    private ClipboardSnapshot(DataObject? data) => Data = data;

    private DataObject? Data { get; }

    public static ClipboardSnapshot Capture()
    {
        try
        {
            var source = Clipboard.GetDataObject();
            if (source is null)
            {
                return new ClipboardSnapshot(null);
            }

            var clone = new DataObject();
            foreach (var format in source.GetFormats(autoConvert: false))
            {
                try
                {
                    var value = source.GetData(format, autoConvert: false);
                    if (value is not null)
                    {
                        clone.SetData(format, value);
                    }
                }
                catch
                {
                    // Some applications expose delayed formats that cannot be cloned safely.
                }
            }

            return new ClipboardSnapshot(clone);
        }
        catch
        {
            return new ClipboardSnapshot(null);
        }
    }

    public void Restore()
    {
        try
        {
            if (Data is null)
            {
                Clipboard.Clear();
            }
            else
            {
                Clipboard.SetDataObject(Data, copy: true);
            }
        }
        catch
        {
            // Clipboard ownership can change between capture and restore; correction still succeeds.
        }
    }
}

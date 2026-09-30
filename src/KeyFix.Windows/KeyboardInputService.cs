using System.ComponentModel;
using System.Runtime.InteropServices;

namespace KeyFix.Windows;

public sealed class KeyboardInputService
{
    public async Task WaitForShortcutModifiersReleasedAsync(CancellationToken cancellationToken = default)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(3);
        while (AreShortcutModifiersPressed() && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(20, cancellationToken);
        }

        // Give the active application one message-loop turn after the physical modifiers
        // are released so the synthetic Ctrl+C is treated as a fresh chord.
        await Task.Delay(35, cancellationToken);
    }

    public void SendCopy() => SendChord(NativeMethods.VkControl, NativeMethods.VkC);

    public void SelectPreviousWord() =>
        SendChord([NativeMethods.VkControl, NativeMethods.VkShift], NativeMethods.VkLeft);

    public void CollapseSelectionToEnd() => SendKey(NativeMethods.VkRight);

    public void SelectPreviousCharacters(int count)
    {
        if (count <= 0)
        {
            return;
        }

        var inputs = new List<NativeMethods.INPUT>(count * 2 + 2)
        {
            KeyInput(NativeMethods.VkShift, keyUp: false)
        };

        for (var index = 0; index < count; index++)
        {
            inputs.Add(KeyInput(NativeMethods.VkLeft, keyUp: false));
            inputs.Add(KeyInput(NativeMethods.VkLeft, keyUp: true));
        }

        inputs.Add(KeyInput(NativeMethods.VkShift, keyUp: true));
        Send(inputs);
    }

    public void DeletePreviousCharacters(int count)
    {
        if (count <= 0)
        {
            return;
        }

        var inputs = new List<NativeMethods.INPUT>(count * 2);
        for (var index = 0; index < count; index++)
        {
            inputs.Add(KeyInput(NativeMethods.VkBack, keyUp: false));
            inputs.Add(KeyInput(NativeMethods.VkBack, keyUp: true));
        }

        Send(inputs);
    }

    public void SendUnicodeText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
        {
            return;
        }

        var inputs = new List<NativeMethods.INPUT>(text.Length * 2);
        foreach (var character in text)
        {
            inputs.Add(UnicodeInput(character, keyUp: false));
            inputs.Add(UnicodeInput(character, keyUp: true));
        }

        Send(inputs);
    }

    private void SendKey(ushort virtualKey) => Send([KeyInput(virtualKey, false), KeyInput(virtualKey, true)]);

    private void SendChord(ushort modifier, ushort key) => SendChord([modifier], key);

    private void SendChord(IReadOnlyList<ushort> modifiers, ushort key)
    {
        var inputs = new List<NativeMethods.INPUT>(modifiers.Count * 2 + 2);
        foreach (var modifier in modifiers)
        {
            inputs.Add(KeyInput(modifier, false));
        }

        inputs.Add(KeyInput(key, false));
        inputs.Add(KeyInput(key, true));

        for (var index = modifiers.Count - 1; index >= 0; index--)
        {
            inputs.Add(KeyInput(modifiers[index], true));
        }

        Send(inputs);
    }

    private static bool AreShortcutModifiersPressed() =>
        IsPressed(NativeMethods.VkControl) ||
        IsPressed(NativeMethods.VkAlt) ||
        IsPressed(NativeMethods.VkShift) ||
        IsPressed(NativeMethods.VkLeftWindows) ||
        IsPressed(NativeMethods.VkRightWindows);

    private static bool IsPressed(ushort virtualKey) =>
        (NativeMethods.GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    private static NativeMethods.INPUT KeyInput(ushort virtualKey, bool keyUp) => new()
    {
        Type = NativeMethods.InputKeyboard,
        Data = new NativeMethods.InputUnion
        {
            Keyboard = new NativeMethods.KEYBDINPUT
            {
                VirtualKey = virtualKey,
                Flags = keyUp ? NativeMethods.KeyEventKeyUp : 0
            }
        }
    };

    private static NativeMethods.INPUT UnicodeInput(char character, bool keyUp) => new()
    {
        Type = NativeMethods.InputKeyboard,
        Data = new NativeMethods.InputUnion
        {
            Keyboard = new NativeMethods.KEYBDINPUT
            {
                ScanCode = character,
                Flags = NativeMethods.KeyEventUnicode | (keyUp ? NativeMethods.KeyEventKeyUp : 0)
            }
        }
    };

    private static void Send(IReadOnlyCollection<NativeMethods.INPUT> inputCollection)
    {
        var inputs = inputCollection.ToArray();
        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
        if (sent != inputs.Length)
        {
            throw new Win32Exception("تعذر إرسال النص إلى التطبيق الحالي.");
        }
    }
}

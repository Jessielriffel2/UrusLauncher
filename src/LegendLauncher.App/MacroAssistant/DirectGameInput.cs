using LegendLauncher.App.GameHosting;
using LegendLauncher.App.ViewModels;

namespace LegendLauncher.App.MacroAssistant;

internal readonly record struct DirectInputResult(bool Succeeded, string Message)
{
    public static DirectInputResult Success(string message = "Ação enviada à sessão.") =>
        new(true, message);

    public static DirectInputResult Failure(string message) => new(false, message);
}

internal sealed class DirectGameInput
{
    private const uint WmMouseMove = 0x0200;
    private const uint WmLeftButtonDown = 0x0201;
    private const uint WmLeftButtonUp = 0x0202;
    private const uint WmKeyDown = 0x0100;
    private const uint WmKeyUp = 0x0101;
    private const uint WmChar = 0x0102;
    private const int VirtualKeyReturn = 0x0D;

    public DirectInputResult ExecuteMove(
        GameSessionViewModel session,
        MacroMove move,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(move);
        if (!double.IsFinite(move.Start.X) ||
            !double.IsFinite(move.Start.Y) ||
            !double.IsFinite(move.End.X) ||
            !double.IsFinite(move.End.Y))
        {
            return DirectInputResult.Failure("A troca macro recebeu coordenadas inválidas.");
        }

        DirectInputResult first = Click(session, move.Start, cancellationToken);
        if (!first.Succeeded)
        {
            return first;
        }

        cancellationToken.ThrowIfCancellationRequested();
        Thread.Sleep(60);
        return Click(session, move.End, cancellationToken);
    }

    public DirectInputResult ExecuteAction(
        GameSessionViewModel session,
        MacroAction action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(action);

        if (action.PressEnter)
        {
            DirectInputResult enter = SendKey(
                session,
                action.AfterClick ?? action.Click ?? new MacroPoint(0, 0),
                VirtualKeyReturn,
                cancellationToken);
            if (!enter.Succeeded)
            {
                return enter;
            }

            Thread.Sleep(120);
            if (action.AfterClick is { } afterClick)
            {
                return Click(session, afterClick, cancellationToken);
            }

            return action.Click is { } confirmClick
                ? Click(session, confirmClick, cancellationToken)
                : DirectInputResult.Success();
        }

        if (action.Text is { Length: > 0 } text)
        {
            if (action.InputClick is { } inputClick)
            {
                DirectInputResult input = Click(session, inputClick, cancellationToken);
                if (!input.Succeeded)
                {
                    return input;
                }

                Thread.Sleep(80);
            }

            DirectInputResult typed = SendText(
                session,
                action.InputClick ?? action.Click ?? new MacroPoint(0, 0),
                text,
                cancellationToken);
            if (!typed.Succeeded)
            {
                return typed;
            }

            Thread.Sleep(80);
        }

        return action.Click is { } click
            ? Click(session, click, cancellationToken)
            : DirectInputResult.Failure("A ação do Cosmo não possui ponto de clique local.");
    }

    internal DirectInputResult Click(
        GameSessionViewModel session,
        MacroPoint point,
        CancellationToken cancellationToken)
    {
        if (!TryResolveTarget(session, point, out nint target, out int x, out int y, out string? error))
        {
            return DirectInputResult.Failure(error ?? "A sessão do jogo não está disponível.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        nint position = MakePosition(x, y);
        if (!NativeWindowMethods.PostMessage(target, WmMouseMove, nint.Zero, position) ||
            !NativeWindowMethods.PostMessage(target, WmLeftButtonDown, MkLeftButtonDown, position))
        {
            return DirectInputResult.Failure("O Windows recusou o clique direto na sessão.");
        }

        Thread.Sleep(25);
        cancellationToken.ThrowIfCancellationRequested();
        if (!NativeWindowMethods.PostMessage(target, WmLeftButtonUp, nint.Zero, position))
        {
            return DirectInputResult.Failure("O Windows não conseguiu encerrar o clique direto.");
        }

        return DirectInputResult.Success();
    }

    private DirectInputResult SendText(
        GameSessionViewModel session,
        MacroPoint anchor,
        string text,
        CancellationToken cancellationToken)
    {
        foreach (char character in text)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryResolveTarget(session, anchor, out nint target, out _, out _, out string? error))
            {
                return DirectInputResult.Failure(error ?? "A sessão do jogo não está disponível.");
            }

            if (!NativeWindowMethods.PostMessage(
                    target,
                    WmChar,
                    new nint(character),
                    new nint(1)))
            {
                return DirectInputResult.Failure("O Windows recusou a entrada de texto direta.");
            }
        }

        return DirectInputResult.Success();
    }

    private DirectInputResult SendKey(
        GameSessionViewModel session,
        MacroPoint anchor,
        int virtualKey,
        CancellationToken cancellationToken)
    {
        if (!TryResolveTarget(session, anchor, out nint target, out _, out _, out string? error))
        {
            return DirectInputResult.Failure(error ?? "A sessão do jogo não está disponível.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!NativeWindowMethods.PostMessage(target, WmKeyDown, new nint(virtualKey), nint.Zero) ||
            !NativeWindowMethods.PostMessage(target, WmKeyUp, new nint(virtualKey), nint.Zero))
        {
            return DirectInputResult.Failure("O Windows recusou a tecla direta.");
        }

        return DirectInputResult.Success();
    }

    private static bool TryResolveTarget(
        GameSessionViewModel session,
        MacroPoint point,
        out nint target,
        out int x,
        out int y,
        out string? error)
    {
        target = nint.Zero;
        x = 0;
        y = 0;
        error = null;
        GameWindowAttachment? attachment = session.Attachment;
        if (attachment is null || !NativeWindowMethods.IsWindowHandle(attachment.GameWindow))
        {
            error = "A sessão do jogo não possui uma janela válida.";
            return false;
        }

        try
        {
            if (NativeWindowMethods.GetWindowProcessId(attachment.GameWindow) != session.ProcessId)
            {
                error = "A janela observada não pertence ao processo esperado da sessão.";
                return false;
            }

            NativeClientSize size = NativeWindowMethods.GetClientSize(attachment.GameWindow);
            if (!size.HasArea)
            {
                error = "A sessão do jogo está sem área visível.";
                return false;
            }

            x = Math.Clamp((int)Math.Round(point.X), 0, size.Width - 1);
            y = Math.Clamp((int)Math.Round(point.Y), 0, size.Height - 1);
            nint root = attachment.GameWindow;
            nint deepest = NativeWindowMethods.FindDeepestChildAtPoint(root, x, y);
            target = deepest == nint.Zero ? root : deepest;
            if (target != root)
            {
                try
                {
                    if (NativeWindowMethods.GetWindowProcessId(target) != session.ProcessId)
                    {
                        target = root;
                    }
                }
                catch (InvalidOperationException)
                {
                    target = root;
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    target = root;
                }
            }

            if (target != root &&
                NativeWindowMethods.TryClientToScreen(root, x, y, out int screenX, out int screenY) &&
                NativeWindowMethods.TryScreenToClient(target, screenX, screenY, out x, out y))
            {
                x = Math.Clamp(x, 0, Math.Max(0, size.Width - 1));
                y = Math.Clamp(y, 0, Math.Max(0, size.Height - 1));
            }

            return target != nint.Zero;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception)
        {
            error = exception.Message;
            return false;
        }
    }

    internal static nint MakePosition(int x, int y) =>
        new((y & 0xFFFF) << 16 | (x & 0xFFFF));

    private static nint MkLeftButtonDown => new(1);
}

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using LegendLauncher.Core.Contracts;
using LegendLauncher.Core.Models;

namespace LegendLauncher.Infrastructure.Security;

/// <summary>
/// Stores launcher credentials in Windows Credential Manager as generic credentials.
/// This class never enumerates credentials and accepts only new-launcher target keys.
/// </summary>
public sealed class WindowsCredentialVault : ICredentialVault
{
    private const int MaximumUserNameLength = 512;

    public Task<CredentialSecret?> GetAsync(
        string credentialKey,
        CancellationToken cancellationToken = default)
    {
        CredentialKey.Validate(credentialKey);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureWindows();

        if (!WindowsCredentialNative.CredRead(
                credentialKey,
                WindowsCredentialNative.TypeGeneric,
                flags: 0,
                out var credentialPointer))
        {
            var error = Marshal.GetLastPInvokeError();
            if (error == WindowsCredentialNative.ErrorNotFound)
            {
                return Task.FromResult<CredentialSecret?>(null);
            }

            throw CreateNativeException(error, "read");
        }

        try
        {
            var native = Marshal.PtrToStructure<WindowsCredentialNative.NativeCredential>(
                credentialPointer);
            var userName = native.UserName == IntPtr.Zero
                ? string.Empty
                : Marshal.PtrToStringUni(native.UserName) ?? string.Empty;
            var password = ReadPassword(native);
            return Task.FromResult<CredentialSecret?>(new CredentialSecret(userName, password));
        }
        finally
        {
            WindowsCredentialNative.CredFree(credentialPointer);
        }
    }

    public Task SetAsync(
        string credentialKey,
        CredentialSecret credential,
        CancellationToken cancellationToken = default)
    {
        CredentialKey.Validate(credentialKey);
        ArgumentNullException.ThrowIfNull(credential);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureWindows();

        ArgumentNullException.ThrowIfNull(credential.UserName);
        ArgumentNullException.ThrowIfNull(credential.Password);
        if (credential.UserName.Length > MaximumUserNameLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(credential),
                "The credential user name is too long for Windows Credential Manager.");
        }

        var passwordBytes = Encoding.Unicode.GetBytes(credential.Password);
        if (passwordBytes.Length > WindowsCredentialNative.MaximumCredentialBlobSize)
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
            throw new ArgumentOutOfRangeException(
                nameof(credential),
                "The credential password is too long for Windows Credential Manager.");
        }

        var targetPointer = IntPtr.Zero;
        var userNamePointer = IntPtr.Zero;
        var passwordHandle = default(GCHandle);

        try
        {
            targetPointer = Marshal.StringToCoTaskMemUni(credentialKey);
            userNamePointer = Marshal.StringToCoTaskMemUni(credential.UserName);
            if (passwordBytes.Length > 0)
            {
                passwordHandle = GCHandle.Alloc(passwordBytes, GCHandleType.Pinned);
            }

            var native = new WindowsCredentialNative.NativeCredential
            {
                Type = WindowsCredentialNative.TypeGeneric,
                TargetName = targetPointer,
                CredentialBlobSize = checked((uint)passwordBytes.Length),
                CredentialBlob = passwordHandle.IsAllocated
                    ? passwordHandle.AddrOfPinnedObject()
                    : IntPtr.Zero,
                Persist = WindowsCredentialNative.PersistLocalMachine,
                UserName = userNamePointer
            };

            if (!WindowsCredentialNative.CredWrite(ref native, flags: 0))
            {
                throw CreateNativeException(Marshal.GetLastPInvokeError(), "save");
            }

            return Task.CompletedTask;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
            if (passwordHandle.IsAllocated)
            {
                passwordHandle.Free();
            }

            if (userNamePointer != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(userNamePointer);
            }

            if (targetPointer != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(targetPointer);
            }
        }
    }

    public Task DeleteAsync(
        string credentialKey,
        CancellationToken cancellationToken = default)
    {
        CredentialKey.Validate(credentialKey);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureWindows();

        if (WindowsCredentialNative.CredDelete(
                credentialKey,
                WindowsCredentialNative.TypeGeneric,
                flags: 0))
        {
            return Task.CompletedTask;
        }

        var error = Marshal.GetLastPInvokeError();
        if (error == WindowsCredentialNative.ErrorNotFound)
        {
            return Task.CompletedTask;
        }

        throw CreateNativeException(error, "delete");
    }

    private static string ReadPassword(WindowsCredentialNative.NativeCredential native)
    {
        if (native.CredentialBlobSize == 0)
        {
            return string.Empty;
        }

        if (native.CredentialBlob == IntPtr.Zero ||
            native.CredentialBlobSize > WindowsCredentialNative.MaximumCredentialBlobSize ||
            (native.CredentialBlobSize & 1) != 0)
        {
            throw new InvalidDataException("Windows Credential Manager returned an invalid credential blob.");
        }

        var passwordBytes = new byte[checked((int)native.CredentialBlobSize)];
        try
        {
            Marshal.Copy(native.CredentialBlob, passwordBytes, startIndex: 0, passwordBytes.Length);
            return Encoding.Unicode.GetString(passwordBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    private static Win32Exception CreateNativeException(int error, string operation) =>
        new(error, $"Windows Credential Manager could not {operation} the launcher credential.");

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Windows Credential Manager is available only on Windows.");
        }
    }
}

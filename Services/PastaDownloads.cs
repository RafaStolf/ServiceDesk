using System.Runtime.InteropServices;

namespace ServiceDesk.Services;

internal static class PastaDownloads
{
    // Known Folder: respeita redirecionamentos e o nome localizado de Downloads.
    public static string Obter()
    {
        var id = new Guid("374DE290-123F-4565-9164-39C4925E467B");
        Marshal.ThrowExceptionForHR(SHGetKnownFolderPath(in id, 0, IntPtr.Zero, out var caminho));
        try { return Marshal.PtrToStringUni(caminho) ?? throw new IOException("Pasta Downloads indisponível."); }
        finally { Marshal.FreeCoTaskMem(caminho); }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetKnownFolderPath(in Guid id, uint flags, IntPtr token, out IntPtr caminho);
}

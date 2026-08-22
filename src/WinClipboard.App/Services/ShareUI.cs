using System.Runtime.InteropServices;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using WinRT;

namespace WinClipboard.App.Services;

/// <summary>
/// Shows the Windows Share flyout from this unpackaged WPF app, following Microsoft's documented
/// pattern for desktop apps ("Share content from a desktop app" / IDataTransferManagerInterop).
///
/// CAVEAT: this project was assembled without access to a Windows machine, so this file has
/// never actually been built or run — there is no WPF/WinRT toolchain in the Linux sandbox it
/// was written in. The shape of the interop call is correct to the best of available knowledge,
/// but treat it as a first draft: build on Windows, exercise the "Share" quick action, and fix
/// up whatever the compiler/debugger surface before shipping it.
/// </summary>
internal static class ShareUI
{
    [ComImport]
    [Guid("3A3DCD6C-3EAB-43DC-BCDE-45671CE800C8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDataTransferManagerInterop
    {
        IntPtr GetForWindow([In] IntPtr appWindow, [In] ref Guid riid);
        void ShowShareUIForWindow(IntPtr appWindow);
    }

    public static async Task ShowAsync(IntPtr hwnd, IReadOnlyList<string> filePaths)
    {
        var interop = DataTransferManager.As<IDataTransferManagerInterop>();
        var riid = typeof(DataTransferManager).GUID;
        var pointer = interop.GetForWindow(hwnd, ref riid);
        var manager = MarshalInterface<DataTransferManager>.FromAbi(pointer);

        var files = new List<StorageFile>(filePaths.Count);
        foreach (var path in filePaths)
        {
            files.Add(await StorageFile.GetFileFromPathAsync(path));
        }

        void OnDataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            args.Request.Data.Properties.Title = "WinClipboard";
            args.Request.Data.SetStorageItems(files);
        }

        manager.DataRequested += OnDataRequested;
        try
        {
            interop.ShowShareUIForWindow(hwnd);
        }
        finally
        {
            manager.DataRequested -= OnDataRequested;
        }
    }
}

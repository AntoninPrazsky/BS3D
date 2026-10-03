using System;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace BS3D.Online
{
    /// <summary>
    /// Whether this machine is connected to the internet (#763), the one fact the first-launch nickname question
    /// needs about the network. <b>It sends nothing:</b> it asks Windows what Windows has already worked out
    /// (<c>INetworkListManager</c>, the answer behind the network icon in the taskbar), so the game contacts neither
    /// the score server nor anyone else before the player has agreed to anything.
    /// <para>
    /// Where that call is unavailable the answer falls back to <see cref="NetworkInterface.GetIsNetworkAvailable"/>,
    /// which says only that an adapter is up, so a network without an internet connection passes it. Being wrong is
    /// cheap either way (#763): a false yes shows the question, and a first clear then waits in the outbox, which
    /// survives the server being away; a false no skips this launch and asks again at the next.
    /// </para>
    /// </summary>
    internal static class InternetCheck
    {
        /// <summary>
        /// Windows' own verdict, or the weaker adapter test when Windows would not give one. <paramref name="how"/>
        /// names which answered, for the run's log.
        /// </summary>
        internal static bool IsConnected(out string how)
        {
            try
            {
                INetworkListManager manager = (INetworkListManager)new NetworkListManagerClass();

                try
                {
                    int result = manager.get_IsConnectedToInternet(out bool connected);
                    if (result >= 0)
                    {
                        how = "Windows network list";
                        return connected;
                    }
                }
                finally
                {
                    Marshal.FinalReleaseComObject(manager);
                }
            }
            catch (Exception e) when (e is COMException or InvalidCastException or PlatformNotSupportedException
                or TypeLoadException or UnauthorizedAccessException)
            {
                //No network list service on this machine, or it would not answer: the adapter test below
            }

            how = "an adapter is up";
            return NetworkInterface.GetIsNetworkAvailable();
        }

        //The network list manager's coclass and interface. Only the vtable's order matters to the call, so every slot
        //before the one wanted is declared with the shape it has and is never called: GetNetworks, GetNetwork,
        //GetNetworkConnections, GetNetworkConnection, then the two properties.
        [ComImport, Guid("DCB00C01-570F-4A9B-8D69-199FDBA5723B")]
        private class NetworkListManagerClass { }

        [ComImport, Guid("DCB00000-570F-4A9B-8D69-199FDBA5723B"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
        private interface INetworkListManager
        {
            [PreserveSig] int GetNetworks(int flags, out IntPtr networks);
            [PreserveSig] int GetNetwork(Guid id, out IntPtr network);
            [PreserveSig] int GetNetworkConnections(out IntPtr connections);
            [PreserveSig] int GetNetworkConnection(Guid id, out IntPtr connection);
            [PreserveSig] int get_IsConnectedToInternet([MarshalAs(UnmanagedType.VariantBool)] out bool connected);
            [PreserveSig] int get_IsConnected([MarshalAs(UnmanagedType.VariantBool)] out bool connected);
        }
    }
}

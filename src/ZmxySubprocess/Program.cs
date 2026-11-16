using System;
using System.IO;
using System.Runtime.InteropServices;
using CefSharp.BrowserSubprocess;
using CefSharp.RenderProcess;

internal static class Program
{
    [DllImport("zmxy-speed.dll", CallingConvention=CallingConvention.Cdecl)] static extern int ZmxyInitialize();
    [DllImport("zmxy-speed.dll", CallingConvention=CallingConvention.Cdecl)] static extern void ZmxyShutdown();
    static int Main(string[] args)
    {
        bool plugin=Array.Exists(args,a=>a=="--type=ppapi");
        bool initialized=false;
        try {
            SubProcess.EnableHighDPISupport();
            if(plugin) {
                int result=ZmxyInitialize(); initialized=result==1;
                var log=Environment.GetEnvironmentVariable("ZMXY_LOG_DIR");
                if(!String.IsNullOrEmpty(log)) File.AppendAllText(Path.Combine(log,"plugin.log"), DateTime.Now+" init="+result+Environment.NewLine);
            }
            return new WcfBrowserSubprocessExecutable().Main(args,null);
        } catch(Exception ex) {
            var log=Environment.GetEnvironmentVariable("ZMXY_LOG_DIR");
            if(!String.IsNullOrEmpty(log)) File.AppendAllText(Path.Combine(log,"subprocess-error.log"),ex+Environment.NewLine);
            return 1;
        } finally { if(initialized) ZmxyShutdown(); }
    }
}

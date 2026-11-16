using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ZmxyDemo {
 // Only audio sessions owned by our live worker process trees are changed.
 // Core Audio runs off the UI thread; no endpoint/system master volume is touched.
 internal sealed class GameAudio:IDisposable {
  internal sealed class Target {public int ProcessId;public string Name;public float Volume;}
  internal sealed class Sample {public string Page;public int ProcessId;public float Volume;public bool Muted;}
  readonly object gate=new object();readonly AutoResetEvent changed=new AutoResetEvent(false);readonly Task worker;
  Target[] targets=new Target[0];Sample[] samples=new Sample[0];volatile bool stopping;string error;
  public GameAudio(){worker=Task.Factory.StartNew(Run,CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default);}
  public void SetTargets(IEnumerable<Target> next){lock(gate){if(stopping)return;targets=next.ToArray();changed.Set();}}
  public Sample[] Snapshot(){lock(gate)return samples.ToArray();}
  public string Error {get{lock(gate)return error;}}
  void Run(){while(!stopping){Target[] current;lock(gate)current=targets;try{var result=Apply(current);lock(gate){samples=result;error=null;}}catch(Exception ex){lock(gate)error=ex.Message;}changed.WaitOne(400);}}
  static Sample[] Apply(Target[] targets){
   var result=new List<Sample>();if(targets.Length==0)return result.ToArray();
   var roots=targets.Where(t=>t.ProcessId>0).ToDictionary(t=>t.ProcessId);var parents=ProcessParents();
   object enumerator=null;IMMDeviceCollection devices=null;
   try{
    enumerator=Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")));
    Check(((IMMDeviceEnumerator)enumerator).EnumAudioEndpoints(0,1,out devices));Check(devices.GetCount(out var count));
    for(uint i=0;i<count;i++){
     IMMDevice device=null;object manager=null;IAudioSessionEnumerator sessions=null;
     try{
      Check(devices.Item(i,out device));var iid=new Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");Check(device.Activate(ref iid,23,IntPtr.Zero,out manager));
      Check(((IAudioSessionManager2)manager).GetSessionEnumerator(out sessions));Check(sessions.GetCount(out var length));
      for(int j=0;j<length;j++){
       object session=null;
       try{
        Check(sessions.GetSession(j,out session));var control=(IAudioSessionControl2)session;Check(control.GetProcessId(out var pid));
        int ancestor=(int)pid;Target target=null;var visited=new HashSet<int>();
        while(ancestor>0&&visited.Add(ancestor)){if(roots.TryGetValue(ancestor,out target))break;if(!parents.TryGetValue(ancestor,out ancestor))break;}
        if(target==null)continue;
        var volume=(ISimpleAudioVolume)session;Check(volume.SetMasterVolume(target.Volume,IntPtr.Zero));Check(volume.SetMute(target.Volume==0,IntPtr.Zero));
        Check(volume.GetMasterVolume(out var actual));Check(volume.GetMute(out var muted));result.Add(new Sample{Page=target.Name,ProcessId=(int)pid,Volume=actual,Muted=muted});
       }catch(COMException){/* An audio stream can disappear during enumeration. */}finally{Release(session);}
      }
     }catch(COMException){/* Hot-plugged devices are retried on the next pass. */}finally{Release(sessions);Release(manager);Release(device);}
    }
   }finally{Release(devices);Release(enumerator);}return result.ToArray();
  }
  static void Check(int hr){if(hr<0)Marshal.ThrowExceptionForHR(hr);}
  static void Release(object value){if(value!=null&&Marshal.IsComObject(value))Marshal.ReleaseComObject(value);}
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct ProcessEntry {public uint Size,Usage,Id;public UIntPtr Heap;public uint Module,Threads,Parent;public int Priority;public uint Flags;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string Exe;}
  [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr CreateToolhelp32Snapshot(uint flags,uint process);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern bool Process32First(IntPtr snapshot,ref ProcessEntry entry);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern bool Process32Next(IntPtr snapshot,ref ProcessEntry entry);
  [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
  static Dictionary<int,int> ProcessParents(){var map=new Dictionary<int,int>();var snapshot=CreateToolhelp32Snapshot(2,0);if(snapshot==new IntPtr(-1))return map;try{var entry=new ProcessEntry{Size=(uint)Marshal.SizeOf(typeof(ProcessEntry))};if(Process32First(snapshot,ref entry))do{map[(int)entry.Id]=(int)entry.Parent;}while(Process32Next(snapshot,ref entry));}finally{CloseHandle(snapshot);}return map;}
  public void Dispose(){lock(gate){if(stopping)return;stopping=true;changed.Set();}worker.GetAwaiter().GetResult();changed.Dispose();}

  [ComImport,Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IMMDeviceEnumerator {
   [PreserveSig]int EnumAudioEndpoints(int flow,uint state,out IMMDeviceCollection devices);
  }
  [ComImport,Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IMMDeviceCollection {
   [PreserveSig]int GetCount(out uint count);[PreserveSig]int Item(uint index,out IMMDevice device);
  }
  [ComImport,Guid("D666063F-1587-4E43-81F1-B948E807363F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IMMDevice {
   [PreserveSig]int Activate(ref Guid iid,uint context,IntPtr parameters,[MarshalAs(UnmanagedType.IUnknown)]out object value);
  }
  [ComImport,Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IAudioSessionManager2 {
   [PreserveSig]int GetAudioSessionControl(IntPtr guid,uint flags,out IntPtr control);
   [PreserveSig]int GetSimpleAudioVolume(IntPtr guid,uint flags,out IntPtr volume);
   [PreserveSig]int GetSessionEnumerator(out IAudioSessionEnumerator sessions);
  }
  [ComImport,Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IAudioSessionEnumerator {
   [PreserveSig]int GetCount(out int count);[PreserveSig]int GetSession(int index,[MarshalAs(UnmanagedType.IUnknown)]out object control);
  }
  [ComImport,Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IAudioSessionControl2 {
   [PreserveSig]int GetState(out int state);
   [PreserveSig]int GetDisplayName(out IntPtr name);[PreserveSig]int SetDisplayName(IntPtr name,IntPtr context);
   [PreserveSig]int GetIconPath(out IntPtr path);[PreserveSig]int SetIconPath(IntPtr path,IntPtr context);
   [PreserveSig]int GetGroupingParam(out Guid grouping);[PreserveSig]int SetGroupingParam(ref Guid grouping,IntPtr context);
   [PreserveSig]int RegisterAudioSessionNotification(IntPtr events);[PreserveSig]int UnregisterAudioSessionNotification(IntPtr events);
   [PreserveSig]int GetSessionIdentifier(out IntPtr id);[PreserveSig]int GetSessionInstanceIdentifier(out IntPtr id);
   [PreserveSig]int GetProcessId(out uint pid);
  }
  [ComImport,Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface ISimpleAudioVolume {
   [PreserveSig]int SetMasterVolume(float level,IntPtr context);[PreserveSig]int GetMasterVolume(out float level);
   [PreserveSig]int SetMute([MarshalAs(UnmanagedType.Bool)]bool mute,IntPtr context);[PreserveSig]int GetMute([MarshalAs(UnmanagedType.Bool)]out bool mute);
  }
 }
}

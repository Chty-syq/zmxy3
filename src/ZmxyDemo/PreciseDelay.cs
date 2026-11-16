using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace ZmxyDemo {
 // One cancellable native timer per keyboard loop; no spinning or thread-pool timer queue.
 internal sealed class PreciseDelay:IDisposable {
  sealed class NativeTimer:WaitHandle { public NativeTimer(IntPtr handle){SafeWaitHandle=new SafeWaitHandle(handle,true);} }
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr CreateWaitableTimerEx(IntPtr attributes,string name,uint flags,uint access);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool SetWaitableTimer(SafeWaitHandle timer,ref long due,int period,IntPtr callback,IntPtr state,bool resume);
  [DllImport("winmm.dll")]static extern uint timeBeginPeriod(uint period);
  [DllImport("winmm.dll")]static extern uint timeEndPeriod(uint period);
  readonly NativeTimer timer;readonly WaitHandle[] waits;readonly CancellationToken token;readonly bool fallbackResolution;
  public bool HighResolution {get;}
  public PreciseDelay(CancellationToken cancellation){
   token=cancellation;var handle=CreateWaitableTimerEx(IntPtr.Zero,null,2,0x00100002);HighResolution=handle!=IntPtr.Zero;
   if(handle==IntPtr.Zero)handle=CreateWaitableTimerEx(IntPtr.Zero,null,0,0x00100002);
   if(handle==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
   timer=new NativeTimer(handle);waits=new WaitHandle[]{token.WaitHandle,timer};
   if(!HighResolution)fallbackResolution=timeBeginPeriod(1)==0;
  }
  public void Wait(int milliseconds){
   token.ThrowIfCancellationRequested();long due=-milliseconds*10000L;
   if(!SetWaitableTimer(timer.SafeWaitHandle,ref due,0,IntPtr.Zero,IntPtr.Zero,false))throw new Win32Exception(Marshal.GetLastWin32Error());
   if(WaitHandle.WaitAny(waits)==0)token.ThrowIfCancellationRequested();
  }
  public void Dispose(){timer.Dispose();if(fallbackResolution)timeEndPeriod(1);}
 }
}

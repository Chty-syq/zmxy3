using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using CefSharp;
using CefSharp.WinForms;

namespace ZmxyDemo
{
    public sealed class InputRepeater
    {
        readonly ChromiumWebBrowser browser;
        CancellationTokenSource cancellation;
        Task running=Task.CompletedTask;
        readonly SemaphoreSlim lifecycle=new SemaphoreSlim(1,1);
        sealed class Stamp {public long Time;public bool Up;public int Key;}
        readonly ConcurrentQueue<Stamp> timing=new ConcurrentQueue<Stamp>();string timerMode="idle";
        const uint MapVirtualKeyToExtendedScanCode = 4; // MAPVK_VK_TO_VSC_EX
        [DllImport("user32.dll", EntryPoint = "MapVirtualKeyW")]
        static extern uint MapVirtualKey(uint code, uint mapType);
        public bool IsRunning => cancellation!=null;
        public InputRepeater(ChromiumWebBrowser browser) { this.browser=browser; }
        public async Task StartAsync(IEnumerable<KeyboardStep> configuredSteps) {
            var steps=KeyboardStep.Prepare(configuredSteps);
            await lifecycle.WaitAsync();
            try {
            await StopCoreAsync();
            if(!browser.IsBrowserInitialized) throw new InvalidOperationException("请先等待游戏页面加载。");
            while(timing.TryDequeue(out _)){}
            var source=new CancellationTokenSource(); cancellation=source;
            running=Task.Factory.StartNew(()=>{
                var token=source.Token;
                try {
                  using(var delay=new PreciseDelay(token)) {
                    timerMode=delay.HighResolution?"high-resolution waitable timer":"waitable timer with 1 ms resolution request";
                    while(!token.IsCancellationRequested) {
                        foreach(var step in steps) {
                            token.ThrowIfCancellationRequested();
                            try { Send(step.Key,false); delay.Wait(Math.Min(30,step.IntervalMs)); }
                            finally { Send(step.Key,true); }
                            delay.Wait(step.IntervalMs);
                        }
                    }
                  }
                } catch(OperationCanceledException) {}
            },CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default);
            } finally { lifecycle.Release(); }
        }
        void Send(int key,bool up) {
            if(browser.IsDisposed||!browser.IsBrowserInitialized) return;
            try {
                var host=browser.GetBrowser().GetHost();
                if(Program.IsolatedTest){timing.Enqueue(new Stamp{Time=Stopwatch.GetTimestamp(),Up=up,Key=key});if(timing.Count>2048)timing.TryDequeue(out _);}
                host.SendKeyEvent(new KeyEvent {
                    WindowsKeyCode = key,
                    // CEF 84 looks up the physical key using the scan code, including its E0/E1 prefix.
                    NativeKeyCode = (int)MapVirtualKey((uint)key, MapVirtualKeyToExtendedScanCode),
                    Modifiers = KeyboardStep.IsNumpadDigit(key)
                        ? CefEventFlags.IsKeyPad | CefEventFlags.NumLockOn
                        : CefEventFlags.None,
                    Type = up ? KeyEventType.KeyUp : KeyEventType.RawKeyDown,
                    FocusOnEditableField = false
                });
            } catch(ObjectDisposedException) {} catch(InvalidOperationException) {}
        }
        internal object TimingSnapshot(){
            var stamps=timing.ToArray();var downs=stamps.Where(s=>!s.Up).Select(s=>s.Time).ToArray();
            var cycles=downs.Skip(1).Select((t,i)=>(t-downs[i])*1000.0/Stopwatch.Frequency).Skip(3).OrderBy(v=>v).ToArray();
            var gaps=new List<double>();for(int i=0;i<stamps.Length-1;i++)if(stamps[i].Up&&!stamps[i+1].Up)gaps.Add((stamps[i+1].Time-stamps[i].Time)*1000.0/Stopwatch.Frequency);
            var releaseGaps=stamps.Take(Math.Max(0,stamps.Length-1)).Select((s,i)=>new{stamp=s,next=stamps[i+1]}).Where(p=>p.stamp.Up&&!p.next.Up).Select(p=>new{key=p.stamp.Key,milliseconds=(p.next.Time-p.stamp.Time)*1000.0/Stopwatch.Frequency}).ToArray();
            return new{mode=timerMode,presses=downs.Length,meanCycleMs=cycles.Length==0?0:cycles.Average(),p50CycleMs=cycles.Length==0?0:cycles[cycles.Length/2],p95CycleMs=cycles.Length==0?0:cycles[(int)((cycles.Length-1)*.95)],meanReleaseGapMs=gaps.Count==0?0:gaps.Average(),releaseGaps};
        }
        public async Task StopAsync() {
            await lifecycle.WaitAsync();
            try { await StopCoreAsync(); } finally { lifecycle.Release(); }
        }
        async Task StopCoreAsync() {
            var source=cancellation; cancellation=null;
            if(source==null) return;
            source.Cancel();
            try { await running; } finally { source.Dispose(); }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Newtonsoft.Json;
using CefSharp;

namespace ZmxyDemo
{
    internal static class SelfTests
    {
        [DllImport("zmxy-speed.dll",CallingConvention=CallingConvention.Cdecl)] static extern int ZmxyInitialize();
        [DllImport("zmxy-speed.dll",CallingConvention=CallingConvention.Cdecl)] static extern void ZmxyShutdown();
        [DllImport("winmm.dll")] static extern uint timeGetTime();
        [DllImport("zmxy-speed.dll",CallingConvention=CallingConvention.Cdecl)] static extern ulong ZmxyRealTime();
        static void Check(bool ok,string name,List<object> result) { result.Add(new{name,ok}); if(!ok)throw new Exception(name); }
        static double Measure(double factor,SpeedController speed) {
            speed.Set(factor); timeGetTime(); Thread.Sleep(100);
            var real=Stopwatch.StartNew(); uint start=timeGetTime(); Thread.Sleep(350); uint end=timeGetTime(); real.Stop();
            return unchecked(end-start)/real.Elapsed.TotalMilliseconds;
        }
        public static int Run() {
            var result=new List<object>(); int exit=0;
            try {
                Check(BrowserPolicy.Allowed(Program.GameUrl),"official page accepted",result);
                Check(!BrowserPolicy.Allowed("https://4399.com.example.org/"),"lookalike domain rejected",result);
                Check(!BrowserPolicy.Allowed("file:///C:/Windows/win.ini"),"arbitrary local file rejected",result);
                Check(BrowserPolicy.OpenExternally(Program.GameUrl,true,false,TransitionType.LinkClicked,"GET"),"clicked 4399 links open externally",result);
                Check(BrowserPolicy.OpenExternally("https://example.org/",true,false,TransitionType.LinkClicked,"GET"),"clicked external website opens externally",result);
                Check(!BrowserPolicy.OpenExternally(Program.GameUrl,false,false,TransitionType.Explicit,"GET"),"programmatic game loading remains embedded",result);
                Check(!BrowserPolicy.OpenExternally(Program.GameUrl,true,true,TransitionType.LinkClicked,"GET"),"automatic redirects do not launch browser",result);
                Check(!BrowserPolicy.OpenExternally(Program.GameUrl,true,false,TransitionType.FormSubmit,"GET")&&!BrowserPolicy.OpenExternally(Program.GameUrl,true,false,TransitionType.LinkClicked,"POST"),"login form submissions remain embedded",result);
                Check(!BrowserPolicy.WebLink("file:///C:/Windows/win.ini")&&!BrowserPolicy.WebLink("javascript:alert(1)")&&!BrowserPolicy.WebLink("cmd:calc")&&!BrowserPolicy.WebLink("about:blank"),"non-web protocols are not shell-launched",result);
                using(var speed=new SpeedController(ZmxyRealTime)) {
                    bool rejected=true;foreach(double invalid in new[]{Double.NaN,Double.PositiveInfinity,-1,0,0.5,1.5,4,99,101}){try{speed.Set(invalid);rejected=false;}catch(ArgumentOutOfRangeException){}}
                    Check(rejected,"unsupported speeds rejected",result);
                    Check(ZmxyInitialize()==1,"native hook initializes outside DllMain",result);
                    Check(!speed.HooksActive,"normal startup does not intercept clocks",result);
                    try {
                        foreach(double factor in new[]{1.0,2.0,3.0,100.0,1.0}) {
                            double ratio=Measure(factor,speed); result.Add(new{factor,ratio});
                            Check(Math.Abs(ratio-factor)<Math.Max(0.25,factor*0.02),"clock ratio "+factor,result);
                        }
                        uint previous=timeGetTime();bool monotonic=true;
                        for(int i=0;i<20;i++){var elapsed=Stopwatch.StartNew();speed.Set(i%2==0?100:1);Thread.Sleep(25);uint now=timeGetTime();elapsed.Stop();if(unchecked(now-previous)>elapsed.Elapsed.TotalMilliseconds*100+200)monotonic=false;previous=now;}
                        Check(monotonic,"speed changes preserve clock continuity",result);
                        Check(speed.Attached==1,"native instance reports readiness",result);
                    } finally { ZmxyShutdown(); }
                }
            } catch(Exception ex) {result.Add(new{error=ex.ToString()});exit=1;}
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"self-test-results.json"),JsonConvert.SerializeObject(result,Formatting.Indented));
            return exit;
        }
    }
}

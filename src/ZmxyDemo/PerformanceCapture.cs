using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CefSharp;
using CefSharp.Callback;
using CefSharp.WinForms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ZmxyDemo {
 // On-demand only: records scheduling/render timing, never cookies, DOM text or screenshots.
 internal sealed class PerformanceCapture:IDevToolsMessageObserver {
  readonly ConcurrentDictionary<int,TaskCompletionSource<JObject>> requests=new ConcurrentDictionary<int,TaskCompletionSource<JObject>>();
  readonly List<JObject> events=new List<JObject>();
  readonly TaskCompletionSource<bool> finished=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
  IBrowserHost host;int nextId;int dropped;
  [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")]static extern IntPtr GetAncestor(IntPtr window,uint flags);
  async Task<JObject> Call(string method,object parameters=null){int id=Interlocked.Increment(ref nextId);var completion=new TaskCompletionSource<JObject>(TaskCreationOptions.RunContinuationsAsynchronously);requests[id]=completion;
   try{await Cef.UIThreadTaskFactory.StartNew(()=>{if(!host.SendDevToolsMessage(JsonConvert.SerializeObject(new{id,method,@params=parameters??new{}})))throw new IOException("Tracing command rejected");});
    if(await Task.WhenAny(completion.Task,Task.Delay(8000))!=completion.Task)throw new TimeoutException(method);return await completion.Task;
   }finally{requests.TryRemove(id,out _);}
  }
  public async Task<string> RunAsync(Control owner,ChromiumWebBrowser browser,string mode="default"){
   host=browser.GetBrowser().GetHost();var registration=await Cef.UIThreadTaskFactory.StartNew(()=>host.AddDevToolsMessageObserver(this));bool started=false;
   try{
    await Call("Tracing.start",new{categories="pepper,ppapi,toplevel,accessibility",options="record-until-full",transferMode="ReportEvents"});started=true;
    var cpu=Process.GetCurrentProcess().TotalProcessorTime.TotalMilliseconds;var clock=Stopwatch.StartNew();var window=owner.Handle;
    var ui=new List<double>();var cef=new List<double>();int foreground=0,timeouts=0;
    await Task.Run(async()=>{for(int i=0;i<150&&clock.ElapsedMilliseconds<15000;i++){
     if(owner.IsDisposed||owner.Disposing)break;
     var response=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);var watch=Stopwatch.StartNew();
     try{owner.BeginInvoke(new Action(()=>response.TrySetResult(true)));}catch(InvalidOperationException){break;}
     if(await Task.WhenAny(response.Task,Task.Delay(2000))==response.Task)ui.Add(watch.Elapsed.TotalMilliseconds);else timeouts++;
     watch.Restart();var cefTask=Cef.UIThreadTaskFactory.StartNew(()=>true);if(await Task.WhenAny(cefTask,Task.Delay(2000))==cefTask)cef.Add(watch.Elapsed.TotalMilliseconds);else timeouts++;
     if(GetAncestor(GetForegroundWindow(),2)==GetAncestor(window,2))foreground++;
     await Task.Delay(100);
    }});
    double duration=clock.Elapsed.TotalMilliseconds,cpuMs=Process.GetCurrentProcess().TotalProcessorTime.TotalMilliseconds-cpu;
    await Call("Tracing.end");started=false;if(await Task.WhenAny(finished.Task,Task.Delay(10000))!=finished.Task)throw new TimeoutException("Tracing completion");
    string folder=Path.Combine(Program.ProfileDir,"performance");Directory.CreateDirectory(folder);string file=Path.Combine(folder,DateTime.Now.ToString("yyyyMMdd-HHmmss")+".json");
    var data=new{page=Program.Session,mode,softwareRendering=Program.SoftwareRendering,durationMs=duration,workerCpuCorePercent=cpuMs/duration*100,foregroundSamples=foreground,samples=ui.Count,timeouts,uiQueue=Stats(ui),cefQueue=Stats(cef),droppedEvents=dropped,traceEvents=events};
    await Task.Run(()=>File.WriteAllText(file,JsonConvert.SerializeObject(data,Formatting.None)));return file;
   }finally{if(started)try{await Call("Tracing.end");}catch(Exception){}await Cef.UIThreadTaskFactory.StartNew(()=>registration.Dispose());}
  }
  static object Stats(List<double> values){var sorted=values.OrderBy(v=>v).ToArray();return new{count=sorted.Length,p50Ms=sorted.Length==0?0:sorted[sorted.Length/2],p95Ms=sorted.Length==0?0:sorted[(int)((sorted.Length-1)*.95)],maxMs=sorted.Length==0?0:sorted.Last()};}
  public bool OnDevToolsMessage(IBrowser browser,Stream message){using(var reader=new StreamReader(message)){var value=JObject.Parse(reader.ReadToEnd());int? id=(int?)value["id"];
   if(id!=null&&requests.TryGetValue(id.Value,out var completion)){if(value["error"]!=null)completion.TrySetException(new IOException(value["error"].ToString()));else completion.TrySetResult((JObject)value["result"]);}
   else if((string)value["method"]=="Tracing.tracingComplete")finished.TrySetResult(true);
   else if((string)value["method"]=="Tracing.dataCollected")foreach(JObject entry in value["params"]["value"]){if(events.Count>=200000){dropped++;continue;}var clean=new JObject();foreach(string key in new[]{"name","cat","ph","ts","dur","pid","tid","id"})if(entry[key]!=null)clean[key]=entry[key];if((string)entry["ph"]=="M"&&new[]{"thread_name","process_name","thread_sort_index","process_sort_index"}.Contains((string)entry["name"]))clean["args"]=entry["args"];else if(entry["args"] is JObject args){var safe=new JObject();foreach(string key in new[]{"src_file","src_func","task_queue_name"})if(args[key]!=null)safe[key]=args[key];if(safe.Count>0)clean["args"]=safe;}events.Add(clean);}
  }return true;}
  public void OnDevToolsMethodResult(IBrowser browser,int messageId,bool success,Stream result){}
  public void OnDevToolsEvent(IBrowser browser,string method,Stream parameters){}
  public void OnDevToolsAgentAttached(IBrowser browser){}
  public void OnDevToolsAgentDetached(IBrowser browser){foreach(var request in requests.Values)request.TrySetException(new IOException("Tracing detached"));}
  public void Dispose(){foreach(var request in requests.Values)request.TrySetException(new ObjectDisposedException(nameof(PerformanceCapture)));}
 }
}

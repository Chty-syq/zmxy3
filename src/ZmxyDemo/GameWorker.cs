using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using CefSharp;
using CefSharp.WinForms;
using Newtonsoft.Json.Linq;

namespace ZmxyDemo {
 internal sealed class GameWorker:Form {
  readonly IntPtr parent;readonly string pipeName;
  readonly ChromiumWebBrowser browser;readonly InputRepeater repeater;
  readonly ConcurrentQueue<string> externalLinks=new ConcurrentQueue<string>();
  SessionChannel channel;bool canClose,closing,capturing;Task readTask;
  readonly Timer parentWatch=new Timer{Interval=1000};
  [DllImport("user32.dll")]static extern bool IsWindow(IntPtr hwnd);
  [DllImport("user32.dll")]static extern IntPtr SetParent(IntPtr child,IntPtr newParent);
  protected override CreateParams CreateParams {get{var p=base.CreateParams;p.Parent=parent;p.Style=(p.Style&~unchecked((int)0x80CF0000))|0x40000000|0x02000000;p.ExStyle&=~0x00040000;return p;}}
  public GameWorker(IntPtr parentHandle,string name){
   parent=parentHandle;pipeName=name;TopLevel=false;ShowInTaskbar=false;FormBorderStyle=FormBorderStyle.None;AutoScaleMode=AutoScaleMode.None;
   Text="造梦会话 "+Program.Session;BackColor=Theme.Surface;ClientSize=new Size(1100,720);
   var navigation=new BrowserPolicy();navigation.ExternalRequested+=OpenExternal;
   browser=new ChromiumWebBrowser("about:blank"){Dock=DockStyle.Fill,RequestHandler=navigation};
   var popup=new PopupPolicy();popup.Requested+=OpenExternal;browser.LifeSpanHandler=popup;
   repeater=new InputRepeater(browser);Controls.Add(browser);
   browser.IsBrowserInitializedChanged+=(s,e)=>{if(browser.IsBrowserInitialized)Ui(async()=>{
    await Cef.UIThreadTaskFactory.StartNew(()=>{var host=browser.GetBrowser().GetHost();host.SetAudioMuted(Program.Session!="A");
     // Avoid expensive automatic Flash accessibility queries on the browser UI thread.
     // Screen reader users can explicitly opt in; WinForms controls remain accessible.
     host.SetAccessibilityState(Program.WebAccessibility?CefState.Enabled:CefState.Disabled);
     host.RequestContext.SetPreference("profile.default_content_setting_values.plugins",1,out var error);});
    await SendAsync("ready",new{hwnd=Handle.ToInt64()});
   });};
   browser.AddressChanged+=(s,e)=>Ui(async()=>{await repeater.StopAsync();await SendAsync("address",new{url=e.Address});});
   browser.LoadingStateChanged+=(s,e)=>{_ =SendAsync("loading",new{loading=e.IsLoading});};
   browser.LoadError+=(s,e)=>{if(e.ErrorCode!=CefErrorCode.Aborted&&e.Frame.IsMain)_=SendAsync("error",new{text="加载失败："+e.ErrorCode});};
   parentWatch.Tick+=(s,e)=>{if(!IsWindow(parent))Close();};
   Shown+=async(s,e)=>{
    try{
     var client=new NamedPipeClientStream(".",pipeName,PipeDirection.InOut,PipeOptions.Asynchronous);
     await client.ConnectAsync(15000);channel=new SessionChannel(client);
     channel.Received+=message=>Ui(async()=>await HandleCommand(message));channel.Disconnected+=()=>Ui(()=>Close());
     readTask=channel.ReadAsync();parentWatch.Start();
     if(Program.AutoPerformanceCapture){_ =AutoCaptureAsync();}
     if(browser.IsBrowserInitialized)await SendAsync("ready",new{hwnd=Handle.ToInt64()});
    }catch(Exception ex){File.AppendAllText(Path.Combine(Program.LogDir,"worker-error.log"),ex+Environment.NewLine);Close();}
   };
   FormClosing+=async(s,e)=>{if(canClose)return;e.Cancel=true;if(closing)return;closing=true;parentWatch.Stop();await Task.Yield();
    await repeater.StopAsync();Hide();SetParent(Handle,IntPtr.Zero);browser.Dispose();channel?.Dispose();canClose=true;Close();};
  }
  void Ui(Action action){if(IsDisposed||Disposing)return;try{BeginInvoke(new Action(()=>{if(!closing)action();}));}catch(InvalidOperationException){}}
  void OpenExternal(string url){Ui(async()=>{
   if(Program.IntegrationTest){externalLinks.Enqueue(url);return;}
   try{BrowserPolicy.LaunchExternal(url);}catch(Exception){await SendAsync("error",new{text="无法打开默认浏览器，请检查 Windows 默认浏览器设置。"});}
  });}
  async Task AutoCaptureAsync(){await Task.Delay(20000);if(!closing&&!IsDisposed)await CaptureAsync();}
  async Task CaptureAsync(){if(capturing||closing)return;capturing=true;try{
   await SendAsync("status",new{text=Program.PerformanceCompare?"正在对照采样约 35 秒，请保持游戏在前台":"正在采样约 15 秒，请保持游戏在前台"});
   if(Program.PerformanceCompare)await Cef.UIThreadTaskFactory.StartNew(()=>browser.GetBrowser().GetHost().SetAccessibilityState(CefState.Enabled));
   string file=await new PerformanceCapture().RunAsync(this,browser,Program.WebAccessibility||Program.PerformanceCompare?"accessibility-enabled":"accessibility-disabled");
   if(Program.PerformanceCompare&&!closing){await Cef.UIThreadTaskFactory.StartNew(()=>browser.GetBrowser().GetHost().SetAccessibilityState(CefState.Disabled));await Task.Delay(2000);file=await new PerformanceCapture().RunAsync(this,browser,"accessibility-disabled");}
   await SendAsync("status",new{text="性能采样已保存："+Path.GetFileName(file)});
  }catch(Exception ex){if(!closing){File.AppendAllText(Path.Combine(Program.LogDir,"performance-error.log"),ex+Environment.NewLine);await SendAsync("error",new{text="采样失败："+ex.Message});}}
   finally{capturing=false;if(Program.PerformanceCompare&&!closing)await Cef.UIThreadTaskFactory.StartNew(()=>browser.GetBrowser().GetHost().SetAccessibilityState(Program.WebAccessibility?CefState.Enabled:CefState.Disabled));}}
  async Task SendAsync(string type,object data=null){try{if(channel!=null)await channel.SendAsync(Message.Create(type,data));}catch(IOException){}catch(ObjectDisposedException){}}
  public static bool CookieAllowed(string d)=>d!=null&&(d.TrimStart('.').Equals("4399.com",StringComparison.OrdinalIgnoreCase)||d.EndsWith(".4399.com",StringComparison.OrdinalIgnoreCase)||d.TrimStart('.').Equals("4399.cn",StringComparison.OrdinalIgnoreCase)||d.EndsWith(".4399.cn",StringComparison.OrdinalIgnoreCase));
  async Task HandleCommand(Message m){
   object result=new{ok=true};bool close=false;
   try{switch(m.Type){
    case "performance":BrowserFocus.Ensure(browser,parent);_ =CaptureAsync();break;
    case "resize": ClientSize=new Size(Math.Max(1,(int)m.Data["width"]),Math.Max(1,(int)m.Data["height"]));break;
    case "navigate":
     string url=(string)m.Data["url"];if(!BrowserPolicy.Allowed(url))throw new InvalidOperationException("仅支持4399和本地验证页");
     await repeater.StopAsync();browser.Load(url);break;
    case "refresh":await repeater.StopAsync();browser.Reload();break;
    case "focus":BrowserFocus.Ensure(browser,parent);break;
    case "visibility":
     bool visible=(bool)m.Data["visible"];browser.Visible=visible;if(!visible)await repeater.StopAsync();
     if(visible)BrowserFocus.Ensure(browser,parent);break;
    case "stop":await repeater.StopAsync();break;
    case "start":
     var steps=m.Data["steps"]?.ToObject<List<KeyboardStep>>();KeyboardStep.Prepare(steps);
     BrowserFocus.Ensure(browser,parent);
     await repeater.StartAsync(steps);break;
    case "exportCookies":
     using(var cm=browser.GetCookieManager())result=new{cookies=(await cm.VisitAllCookiesAsync()).Where(c=>CookieAllowed(c.Domain)).ToArray()};break;
    case "importCookies":
     var cookies=m.Data["cookies"].ToObject<List<Cookie>>();
     using(var cm=browser.GetCookieManager()){await cm.DeleteCookiesAsync("","");foreach(var c in cookies.Where(c=>CookieAllowed(c.Domain)))await cm.SetCookieAsync((c.Secure?"https://":"http://")+c.Domain.TrimStart('.')+"/",c);await cm.FlushStoreAsync();}break;
    case "probe":
     if(browser.Address!=Program.TestUrl)throw new InvalidOperationException("测试探针仅可读取本地验证页");
     var value=await browser.EvaluateScriptAsync("JSON.stringify(window.testState ? window.testState() : null)");
     var muted=await Cef.UIThreadTaskFactory.StartNew(()=>browser.GetBrowser().GetHost().IsAudioMuted);
     result=new{state=value.Result as string,dispatch=repeater.TimingSnapshot(),muted,externalLinks=externalLinks.ToArray()};break;
    case "testLink":
     if(!Program.IntegrationTest||browser.Address!=Program.TestUrl)throw new InvalidOperationException("仅限链接集成验证");
     string link=(string)m.Data["link"];if(!new[]{"link-self","link-blank","link-script","link-other"}.Contains(link))throw new ArgumentException("未知验证链接");
     var bounds=await browser.EvaluateScriptAsync("(function(){var a=document.getElementById('"+link+"');a.scrollIntoView({block:'center'});var r=a.getBoundingClientRect();return JSON.stringify({x:r.x+r.width/2,y:r.y+r.height/2});})()");
     if(!bounds.Success)throw new InvalidOperationException(bounds.Message);var point=JObject.Parse((string)bounds.Result);
     bool middle=(bool?)m.Data["middle"]??false,control=(bool?)m.Data["control"]??false;
     var mouse=new MouseEvent((int)(double)point["x"],(int)(double)point["y"],control?CefEventFlags.ControlDown:CefEventFlags.None);
     var host=browser.GetBrowser().GetHost();host.SendMouseClickEvent(mouse,middle?MouseButtonType.Middle:MouseButtonType.Left,false,1);host.SendMouseClickEvent(mouse,middle?MouseButtonType.Middle:MouseButtonType.Left,true,1);break;
    case "testAutoPopup":
     if(!Program.IntegrationTest||browser.Address!=Program.TestUrl)throw new InvalidOperationException("仅限链接集成验证");
     await browser.EvaluateScriptAsync("window.open('https://example.org/automatic-popup')");break;
    case "testAudio":
     if(!Program.IsolatedTest||browser.Address!=Program.TestUrl)throw new InvalidOperationException("仅限本地验证页");
     var toneResult=await browser.EvaluateScriptAsync("window.audioCheck = new AudioContext(); var tone = audioCheck.createOscillator(); var gain = audioCheck.createGain(); gain.gain.value = 0.0001; tone.connect(gain); gain.connect(audioCheck.destination); tone.start(); audioCheck.resume(); audioCheck.state;");
     if(!toneResult.Success)throw new InvalidOperationException(toneResult.Message);result=new{state=toneResult.Result};break;
    case "close":close=true;await repeater.StopAsync();using(var cm=browser.GetCookieManager())await cm.FlushStoreAsync();break;
    default:throw new InvalidOperationException("未知会话指令");
   }}catch(Exception ex){result=new{error=ex.Message};}
   if(m.Id!=null&&channel!=null){try{await channel.SendAsync(Message.Create("reply",result,m.Id));}catch(IOException){}}
   if(close)Close();
  }
 }
}

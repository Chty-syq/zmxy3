using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace ZmxyDemo {
 internal sealed class GameSession:IDisposable {
  public readonly string Name;public readonly Panel Surface;public readonly SpeedController Speed;
  public bool Running;
  public int ProcessId=>process==null||disposed?0:process.Id;
  public string Url="about:blank",Status="正在启动…";public bool Loading;
  public event Action<GameSession> Changed;
  readonly NamedPipeServerStream server;readonly string pipeName="Zmxy-"+Guid.NewGuid().ToString("N");
  readonly TaskCompletionSource<bool> ready=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
  SessionChannel channel;Process process;bool disposed;Task closeTask;
  public Task Ready=>ready.Task;
  public GameSession(string name,Panel surface){Name=name;Surface=surface;Speed=new SpeedController(publishEnvironment:false);server=SessionChannel.Server(pipeName);}
  public async Task StartAsync(){
   var waiting=server.WaitForConnectionAsync();
   var args="--worker --session "+Name+" --parent "+Surface.Handle.ToInt64()+" --pipe "+pipeName+(Program.IntegrationTest?" --integration-test":"")+(Program.TestPage?" --test-page":"")+(Program.SoftwareRendering?" --software-rendering":"");
   var start=new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ZmxyDemo.exe"),args){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory};
   if(Program.AutoPerformanceCapture)start.Arguments+=" --performance-capture";
   if(Program.PerformanceCompare)start.Arguments+=" --performance-compare";
   if(Program.WebAccessibility)start.Arguments+=" --enable-web-accessibility";
   start.EnvironmentVariables["ZMXY_SPEED_CHANNEL"]=Speed.Channel;
   process=Process.Start(start);process.EnableRaisingEvents=true;process.Exited+=(s,e)=>{if(!disposed){Status="会话已退出";ready.TrySetException(new IOException("会话启动失败，请查看 data/"+Name+"/logs"));Changed?.Invoke(this);}};
   if(await Task.WhenAny(waiting,Task.Delay(20000))!=waiting)throw new TimeoutException("游戏引擎连接超时");
   await waiting;channel=new SessionChannel(server);channel.Received+=Receive;_ =channel.ReadAsync();
   if(await Task.WhenAny(Ready,Task.Delay(20000))!=Ready)throw new TimeoutException("游戏引擎初始化超时");await Ready;
  }
  void Receive(Message m){switch(m.Type){case "ready":ready.TrySetResult(true);Status="就绪";break;case "address":Url=(string)m.Data["url"];Running=false;break;case "loading":Loading=(bool)m.Data["loading"];Status=Loading?"页面加载中…":"就绪";break;case "status":case "error":Status=(string)m.Data["text"];break;}Changed?.Invoke(this);}
  public Task<Message> RequestAsync(string type,object data=null){if(channel==null)throw new InvalidOperationException("页面仍在启动");return channel.RequestAsync(type,data);}
  public async Task StopAsync(){Running=false;if(channel!=null)await RequestAsync("stop");Changed?.Invoke(this);}
  public async Task ResizeAsync(){if(channel!=null&&!disposed)try{await RequestAsync("resize",new{width=Surface.ClientSize.Width,height=Surface.ClientSize.Height});}catch(IOException){}catch(TimeoutException){}}
  public Task CloseAsync()=>closeTask??(closeTask=CloseCoreAsync());
  async Task CloseCoreAsync(){if(disposed)return;try{if(channel!=null)await RequestAsync("close");}catch(Exception){}
   if(process!=null&&!process.HasExited){await Task.Run(()=>process.WaitForExit(5000));if(!process.HasExited)process.Kill();}Dispose();}
  public void Dispose(){if(disposed)return;disposed=true;channel?.Dispose();server.Dispose();Speed.Dispose();process?.Dispose();Surface.Dispose();}
 }
}

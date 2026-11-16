using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ZmxyDemo {
 internal sealed class Message {
  public string Type,Id;public JObject Data;
  public static Message Create(string type,object data=null,string id=null)=>new Message{Type=type,Id=id,Data=data==null?new JObject():JObject.FromObject(data)};
 }
 internal sealed class SessionChannel:IDisposable {
  readonly PipeStream pipe;readonly StreamReader reader;readonly StreamWriter writer;
  readonly SemaphoreSlim writes=new SemaphoreSlim(1,1);
  readonly ConcurrentDictionary<string,TaskCompletionSource<Message>> pending=new ConcurrentDictionary<string,TaskCompletionSource<Message>>();
  public event Action<Message> Received;public event Action Disconnected;
  bool disposed;
  public SessionChannel(PipeStream stream){pipe=stream;reader=new StreamReader(pipe,Encoding.UTF8,false,4096,true);writer=new StreamWriter(pipe,new UTF8Encoding(false),4096,true){AutoFlush=true};}
  public static NamedPipeServerStream Server(string name){
   var security=new PipeSecurity();security.SetAccessRuleProtection(true,false);
   security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User,PipeAccessRights.FullControl,AccessControlType.Allow));
   return new NamedPipeServerStream(name,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous,65536,65536,security);
  }
  public async Task ReadAsync(){
   try{while(!disposed){string line=await reader.ReadLineAsync().ConfigureAwait(false);if(line==null)break;if(line.Length>1048576)throw new IOException("Session message too large");
    var message=JsonConvert.DeserializeObject<Message>(line);
    if(message==null)continue;
    if(message.Type=="reply"&&message.Id!=null&&pending.TryRemove(message.Id,out var completion))completion.TrySetResult(message);
    else Received?.Invoke(message);
   }}catch(IOException){}catch(ObjectDisposedException){}finally{foreach(var p in pending)p.Value.TrySetException(new IOException("游戏会话已断开"));pending.Clear();Disconnected?.Invoke();}
  }
  public async Task SendAsync(Message message){
   await writes.WaitAsync().ConfigureAwait(false);
   try{if(disposed)throw new ObjectDisposedException(nameof(SessionChannel));await writer.WriteLineAsync(JsonConvert.SerializeObject(message)).ConfigureAwait(false);}
   finally{writes.Release();}
  }
  public async Task<Message> RequestAsync(string type,object data=null,int timeout=15000){
   string id=Guid.NewGuid().ToString("N");var completion=new TaskCompletionSource<Message>(TaskCreationOptions.RunContinuationsAsynchronously);pending[id]=completion;
   try{await SendAsync(Message.Create(type,data,id)).ConfigureAwait(false);
    if(await Task.WhenAny(completion.Task,Task.Delay(timeout)).ConfigureAwait(false)!=completion.Task)throw new TimeoutException("游戏页面响应超时");
    var reply=await completion.Task.ConfigureAwait(false);if(reply.Data["error"]!=null)throw new InvalidOperationException((string)reply.Data["error"]);return reply;
   }finally{pending.TryRemove(id,out _);}
  }
  public void Dispose(){if(disposed)return;disposed=true;pipe.Dispose();}
 }
}

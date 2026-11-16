using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;

namespace ZmxyDemo
{
    public sealed class SpeedController : IDisposable
    {
        public static readonly double[] SupportedSpeeds = { 1, 2, 3, 100 };
        [DllImport("kernel32.dll")] public static extern ulong GetTickCount64();
        readonly MemoryMappedFile mapping;
        readonly MemoryMappedViewAccessor view;
        readonly Timer timer;
        public string Channel { get; } = "Local\\ZmxyDemo-"+Guid.NewGuid().ToString("N");
        public double Speed { get; private set; }=1;
        public int Attached => view.ReadInt32(16);
        public bool HooksActive => view.ReadInt32(24)!=0;
        public SpeedController(Func<ulong> realClock=null,bool publishEnvironment=true) {
            var clock=realClock??GetTickCount64;
            mapping=MemoryMappedFile.CreateNew(Channel,64);
            view=mapping.CreateViewAccessor();
            view.Write(0,0x5A4D5833); view.Write(4,1000);
            view.Write(8,(long)clock());
            timer=new Timer(_=>view.Write(8,(long)clock()),null,0,500);
            if(publishEnvironment)Environment.SetEnvironmentVariable("ZMXY_SPEED_CHANNEL",Channel);
        }
        public void Set(double speed) {
            if(Array.IndexOf(SupportedSpeeds,speed)<0) throw new ArgumentOutOfRangeException(nameof(speed));
            Speed=speed; view.Write(4,(int)Math.Round(speed*1000));
        }
        public void Dispose() {
            Set(1);
            using(var done=new ManualResetEvent(false)) { timer.Dispose(done); done.WaitOne(); }
            view.Dispose(); mapping.Dispose();
        }
    }
}

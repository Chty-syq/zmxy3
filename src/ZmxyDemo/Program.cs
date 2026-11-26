using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using CefSharp;

namespace ZmxyDemo {
 public static class Program {
  public static string Session="A",DataRoot,ProfileDir,LogDir,FlashPath;
  public static bool TestPage,SoftwareRendering,IntegrationTest,AutoPerformanceCapture,PerformanceCompare,WebAccessibility;
  public static bool IsolatedTest=>TestPage||IntegrationTest;
  public static readonly string GameUrl="https://www.4399.com/flash/zmhj.htm?g=3";
  public static string TestUrl=>new Uri(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-page.html")).AbsoluteUri;
  public static string Option(string[] args,string key,string fallback=null){int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
  internal static string FindFlash() {
   var baseDir=AppDomain.CurrentDomain.BaseDirectory;
   if(!File.Exists(Path.Combine(baseDir,"installed.flag"))) {
    var portable=new[]{Path.Combine(baseDir,"runtime","pepflashplayer.dll"),Path.GetFullPath(Path.Combine(baseDir,"..","..","runtime","pepflashplayer.dll"))}.FirstOrDefault(File.Exists);
    if(portable!=null)return portable;
   }
   // Use the system installation for the packaged client. Side-by-side versions
   // are named pepflashplayer64_34_0_0_384.dll, etc.
   var directory=Path.Combine(Environment.SystemDirectory,"Macromed","Flash");
   const string prefix="pepflashplayer64_";
   try {
    if(!Directory.Exists(directory))return null;
    return Directory.EnumerateFiles(directory,prefix+"*.dll")
     .OrderByDescending(file=>Version.TryParse(Path.GetFileNameWithoutExtension(file).Substring(prefix.Length).Replace('_','.'),out var version)?version:new Version())
     .FirstOrDefault();
   }catch(IOException){return null;}catch(UnauthorizedAccessException){return null;}
  }
  [STAThread] public static int Main(string[] args){
   if(args.Contains("--self-test"))return SelfTests.Run();
   TestPage=args.Contains("--test-page");SoftwareRendering=args.Contains("--software-rendering");IntegrationTest=args.Contains("--integration-test");
   AutoPerformanceCapture=args.Contains("--performance-capture");
   PerformanceCompare=args.Contains("--performance-compare");
   WebAccessibility=args.Contains("--enable-web-accessibility");
   var baseDir=AppDomain.CurrentDomain.BaseDirectory;
   var profileRoot=File.Exists(Path.Combine(baseDir,"installed.flag"))
    ?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ZmxyStudio"):baseDir;
   DataRoot=Path.Combine(profileRoot,IsolatedTest?"test-data":"data");Directory.CreateDirectory(DataRoot);
   FlashPath=FindFlash();
   Cef.EnableHighDPISupport();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   if(args.Contains("--worker"))return RunWorker(args);
   bool created;using(var mutex=new Mutex(true,IsolatedTest?"Local\\ZmxyDemo-TestShell":"Local\\ZmxyDemo-Shell",out created)){
    if(!created){MessageBox.Show("造梦工坊已打开，请在左侧添加或切换页面。","造梦工坊");return 0;}
    try{Application.Run(new MainForm());return Environment.ExitCode;}
    catch(Exception ex){File.AppendAllText(Path.Combine(DataRoot,"shell-error.log"),ex+Environment.NewLine);MessageBox.Show(ex.Message,"启动失败");return 1;}
    finally{mutex.ReleaseMutex();}
   }
  }
  static int RunWorker(string[] args){
   Session=Option(args,"--session","A");if(Session.Length!=1||Session[0]<'A'||Session[0]>'D')return 2;
   ProfileDir=Path.Combine(DataRoot,Session);LogDir=Path.Combine(ProfileDir,"logs");Directory.CreateDirectory(LogDir);
   Environment.SetEnvironmentVariable("ZMXY_LOG_DIR",LogDir);
   bool created;using(var mutex=new Mutex(true,"Local\\ZmxyDemo-"+(IsolatedTest?"Test-":"")+Session,out created)){
    if(!created)return 3;bool cef=false;
    try{
     var settings=new CefSharp.WinForms.CefSettings{
      CachePath=Path.Combine(ProfileDir,"cache"),RootCachePath=ProfileDir,PersistSessionCookies=true,
      BrowserSubprocessPath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ZmxySubprocess.exe"),
      LogFile=Path.Combine(LogDir,"cef.log"),LogSeverity=LogSeverity.Warning,AcceptLanguageList="zh-CN,zh,en-US,en"
     };
     // All page workers use direct connections, independent of Windows proxy/PAC.
     settings.CefCommandLineArgs["no-proxy-server"]="1";
     // Prevent native accessibility clients from automatically re-enabling the
     // renderer tree after navigation. SetAccessibilityState alone is transient.
     if(!WebAccessibility&&!PerformanceCompare)settings.CefCommandLineArgs["disable-renderer-accessibility"]="1";
     settings.CefCommandLineArgs["disable-renderer-backgrounding"]="1";
     settings.CefCommandLineArgs["disable-background-timer-throttling"]="1";
     settings.CefCommandLineArgs["disable-backgrounding-occluded-windows"]="1";
     if(Session!="A")settings.CefCommandLineArgs["mute-audio"]="1";
     if(IsolatedTest)settings.CefCommandLineArgs["autoplay-policy"]="no-user-gesture-required";
     if(SoftwareRendering){settings.CefCommandLineArgs["disable-gpu"]="1";settings.CefCommandLineArgs["disable-gpu-compositing"]="1";}
     if(FlashPath!=null){
      settings.CefCommandLineArgs["ppapi-flash-path"]=FlashPath;
      settings.CefCommandLineArgs["ppapi-flash-version"]=System.Diagnostics.FileVersionInfo.GetVersionInfo(FlashPath).FileVersion.Replace(',','.');
      settings.CefCommandLineArgs["allow-outdated-plugins"]="1";settings.CefCommandLineArgs["plugin-policy"]="allow";
      settings.CefCommandLineArgs["disable-features"]="BlockOutdatedPlugin,FlashDeprecationWarning,PreferHtmlOverPlugins,CalculateNativeWinOcclusion";
     }
     cef=Cef.Initialize(settings);if(!cef)throw new InvalidOperationException("游戏引擎启动失败。");
     Application.Run(new GameWorker(new IntPtr(Int64.Parse(Option(args,"--parent"))),Option(args,"--pipe")));return 0;
    }catch(Exception ex){File.AppendAllText(Path.Combine(LogDir,"worker-error.log"),ex+Environment.NewLine);return 1;}
    finally{if(cef)Cef.Shutdown();mutex.ReleaseMutex();}
   }
  }
 }
}

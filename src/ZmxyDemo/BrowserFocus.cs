using System;
using System.Runtime.InteropServices;
using CefSharp;
using CefSharp.WinForms;

namespace ZmxyDemo {
 internal static class BrowserFocus {
  [StructLayout(LayoutKind.Sequential)]struct GuiThreadInfo {public uint Size,Flags;public IntPtr Active,Focus,Capture,MenuOwner,MoveSize,Caret;public int Left,Top,Right,Bottom;}
  [DllImport("user32.dll")]static extern bool GetGUIThreadInfo(uint thread,ref GuiThreadInfo info);
  [DllImport("user32.dll")]static extern bool IsChild(IntPtr parent,IntPtr child);
  [DllImport("user32.dll")]static extern IntPtr GetAncestor(IntPtr hwnd,uint flags);
  [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
  public static bool ContainsNativeFocus(ChromiumWebBrowser browser){var info=new GuiThreadInfo{Size=(uint)Marshal.SizeOf(typeof(GuiThreadInfo))};return GetGUIThreadInfo(0,ref info)&&(info.Focus==browser.Handle||IsChild(browser.Handle,info.Focus));}
  public static void Ensure(ChromiumWebBrowser browser,IntPtr parent){
   if(!browser.IsBrowserInitialized||ContainsNativeFocus(browser))return;
   // F7 must not move focus away from an already-focused Flash/plugin child.
   // Explicitly allowed background input must not activate another application.
   if(GetAncestor(GetForegroundWindow(),2)!=GetAncestor(parent,2))return;
   browser.GetBrowser().GetHost().SetFocus(true);
  }
 }
}

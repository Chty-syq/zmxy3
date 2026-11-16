using System;
using System.IO;
using System.Diagnostics;
using CefSharp;
using CefSharp.Handler;

namespace ZmxyDemo
{
    public sealed class BrowserPolicy : RequestHandler
    {
        public event Action<string> ExternalRequested;
        internal static bool WebLink(string url)=>Uri.TryCreate(url,UriKind.Absolute,out var uri)&&(uri.Scheme==Uri.UriSchemeHttp||uri.Scheme==Uri.UriSchemeHttps);
        internal static bool OpenExternally(string url,bool userGesture,bool isRedirect,TransitionType transition,string method){
            var source=transition&TransitionType.SourceMask;
            return WebLink(url)&&userGesture&&!isRedirect&&String.Equals(method,"GET",StringComparison.OrdinalIgnoreCase)&&source!=TransitionType.FormSubmit&&source!=TransitionType.Reload;
        }
        internal static void LaunchExternal(string url){
            if(!WebLink(url))throw new ArgumentException("仅支持网页链接");
            Process.Start(new ProcessStartInfo(new Uri(url).AbsoluteUri){UseShellExecute=true});
        }
        public static bool Allowed(string url) {
            if(url=="about:blank") return true;
            Uri uri;
            if(!Uri.TryCreate(url,UriKind.Absolute,out uri)) return false;
            if(uri.IsFile) return String.Equals(Path.GetFullPath(uri.LocalPath),Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-page.html"),StringComparison.OrdinalIgnoreCase);
            if(uri.Scheme!="http"&&uri.Scheme!="https") return false;
            return uri.Host=="4399.com"||uri.Host.EndsWith(".4399.com",StringComparison.OrdinalIgnoreCase)||uri.Host=="4399.cn"||uri.Host.EndsWith(".4399.cn",StringComparison.OrdinalIgnoreCase);
        }
        protected override bool OnBeforeBrowse(IWebBrowser web,IBrowser browser,IFrame frame,IRequest request,bool userGesture,bool isRedirect) {
            if(OpenExternally(request.Url,userGesture,isRedirect,request.TransitionType,request.Method)){
                ExternalRequested?.Invoke(request.Url);return true;
            }
            // Programmatic game loads, form submissions and login subframes stay embedded.
            return frame.IsMain&&!Allowed(request.Url);
        }
        protected override bool OnOpenUrlFromTab(IWebBrowser web,IBrowser browser,IFrame frame,string targetUrl,WindowOpenDisposition targetDisposition,bool userGesture){
            if(userGesture&&WebLink(targetUrl)){ExternalRequested?.Invoke(targetUrl);return true;}
            return !Allowed(targetUrl);
        }
    }
    public sealed class PopupPolicy : ILifeSpanHandler
    {
        public event Action<string> Requested;
        public bool OnBeforePopup(IWebBrowser web,IBrowser browser,IFrame frame,string targetUrl,string targetFrameName,WindowOpenDisposition targetDisposition,bool userGesture,IPopupFeatures popupFeatures,IWindowInfo windowInfo,IBrowserSettings browserSettings,ref bool noJavascriptAccess,out IWebBrowser newBrowser) {
            newBrowser=null;
            if(userGesture&&BrowserPolicy.WebLink(targetUrl)) Requested?.Invoke(targetUrl);
            return true;
        }
        public void OnAfterCreated(IWebBrowser web,IBrowser browser) {}
        // The worker owns this child window. CEF's default close notification targets
        // the top-level shell, which would close all pages when one tab is disposed.
        public bool DoClose(IWebBrowser web,IBrowser browser) => true;
        public void OnBeforeClose(IWebBrowser web,IBrowser browser) {}
    }
}

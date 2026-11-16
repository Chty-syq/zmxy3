using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ZmxyDemo {
 internal sealed class MainForm:Form {
  sealed class PageTab {public Panel Panel;public PageButton Select;public StudioButton Close;}
  readonly List<GameSession> sessions=new List<GameSession>();readonly Dictionary<GameSession,PageTab> tabButtons=new Dictionary<GameSession,PageTab>();readonly HashSet<GameSession> removing=new HashSet<GameSession>();
  readonly Panel stage=new Panel{Dock=DockStyle.Fill,BackColor=Theme.Surface};
  readonly FlowLayoutPanel tabs=new FlowLayoutPanel{Width=268,Height=60,FlowDirection=FlowDirection.TopDown,WrapContents=false,Margin=new Padding(0,8,0,0)};
  readonly Label status=new Label(),address=new Label(),inputState=new Label(),volumeLabel=new Label(),pageCount=new Label();
  readonly Dictionary<double,StudioButton> speedButtons=new Dictionary<double,StudioButton>();readonly KeyboardSequenceEditor keyboard=new KeyboardSequenceEditor();
  readonly CheckBox background=new ToggleSwitch{Text="切到其他应用时继续",Width=268,Height=29};
  readonly StudioButton start=new StudioButton(),add=new StudioButton();readonly Timer timer=new Timer{Interval=1000};
  readonly ToolTip tips=new ToolTip{AutoPopDelay=8000,InitialDelay=500,ReshowDelay=200};
  readonly VolumeSlider volume=new VolumeSlider{BackColor=Theme.Surface};
  readonly GameAudio audio=new GameAudio();double sharedSpeed=1;
  GameSession active;bool creating,closing,canClose,hotkey,inputHotkey,inputBusy;int inputGeneration;
  [DllImport("user32.dll")]static extern bool RegisterHotKey(IntPtr h,int id,uint modifiers,uint key);
  [DllImport("user32.dll")]static extern bool UnregisterHotKey(IntPtr h,int id);
  [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")]static extern IntPtr GetAncestor(IntPtr hwnd,uint flags);
  internal IReadOnlyList<GameSession> Sessions=>sessions;internal GameSession Active=>active;
  internal List<KeyboardStep> SharedKeyboardSteps=>keyboard.ReadSteps();internal double SharedSpeed=>sharedSpeed;
  internal GameAudio.Sample[] AudioSnapshot=>audio.Snapshot();
  internal string AudioError=>audio.Error;
  public MainForm(){
   Text="造梦工坊"+(Program.IntegrationTest?" · 功能验证":"");Width=1500;Height=980;MinimumSize=new Size(1180,820);
   StartPosition=FormStartPosition.CenterScreen;AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
   BackColor=Theme.Background;ForeColor=Theme.Text;Font=new Font("Microsoft YaHei UI",9F);DoubleBuffered=true;
   var iconPath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"app.ico");if(File.Exists(iconPath))Icon=new Icon(iconPath);
   var layout=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3,ColumnCount=1,Padding=new Padding(22,0,22,0),BackColor=Theme.Background};
   layout.RowStyles.Add(new RowStyle(SizeType.Absolute,86));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,34));Controls.Add(layout);
   var header=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty};
   header.Controls.Add(new BrandMark{Location=new Point(0,21),Size=new Size(44,44)});
   header.Controls.Add(new Label{Text="造梦工坊",Font=new Font(Font.FontFamily,19F,FontStyle.Bold),AutoSize=true,Location=new Point(57,14)});
   header.Controls.Add(new Label{Text="大闹天庭，再续西游",ForeColor=Theme.Muted,Font=new Font(Font.FontFamily,8.5f),AutoSize=true,Location=new Point(60,48)});
   var headerActions=new FlowLayoutPanel{Dock=DockStyle.Right,Width=250,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(0,24,0,0),WrapContents=false};
   var stop=Button("全部停止",EmergencyStopAsync);stop.Shortcut="F8";stop.Width=138;stop.ForeColor=Theme.Danger;stop.Margin=new Padding(10,0,0,0);
   tips.SetToolTip(stop,"停止所有页面的连点，并恢复 1× 速度");headerActions.Controls.Add(stop);
   var help=Button("使用说明",()=>{ShowHelp();return Task.CompletedTask;});help.Width=96;help.Quiet=true;help.Margin=Padding.Empty;headerActions.Controls.Add(help);header.Controls.Add(headerActions);layout.Controls.Add(header,0,0);
   var body=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};
   body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,328));body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));layout.Controls.Add(body,0,1);
   var sidebar=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,BackColor=Theme.Background,Margin=new Padding(0,0,12,0)};body.Controls.Add(sidebar,0,0);
   var pages=new StudioCard();pages.Controls.Add(Heading("游戏页面",null,pageCount));pages.Controls.Add(tabs);
   add.Text="＋  新建页面";StyleButton(add);add.ForeColor=Theme.Blue;add.Click+=async(s,e)=>await AddPageUiAsync();pages.Controls.Add(add);
   tips.SetToolTip(add,"继承当前账号的登录状态，最多打开四个页面");sidebar.Controls.Add(pages);
   var speedCard=new StudioCard();speedCard.Controls.Add(Heading("变速齿轮","全页面共享"));
   var speeds=new FlowLayoutPanel{Width=268,Height=36,WrapContents=false,Margin=new Padding(0,10,0,0)};
   foreach(double factor in SpeedController.SupportedSpeeds){var button=new StudioButton{Text=factor+"×",Width=62,Height=34,Margin=new Padding(0,0,6,0),AccessibleName="游戏速度 "+factor+" 倍"};button.Click+=(s,e)=>SetGameSpeed(factor);speedButtons[factor]=button;speeds.Controls.Add(button);}speedCard.Controls.Add(speeds);sidebar.Controls.Add(speedCard);
   var keys=new StudioCard();keys.Controls.Add(Heading("键盘连点","当前页面执行"));keys.Controls.Add(keyboard);
   background.ForeColor=Theme.Muted;background.Font=new Font(Font.FontFamily,8F);background.Margin=new Padding(0,4,0,6);keys.Controls.Add(background);
   StyleButton(start,true);start.Text="开始连点";start.Shortcut="F7";start.Height=40;start.TextAlign=ContentAlignment.MiddleLeft;start.Click+=async(s,e)=>await ToggleInputAsync();keys.Controls.Add(start);
   StyleNote(inputState,23);keys.Controls.Add(inputState);tips.SetToolTip(keyboard,"从上到下循环执行启用行。间隔为松开本行按键后的等待时间，最低 1 ms。");sidebar.Controls.Add(keys);
   var sound=new StudioCard();sound.Controls.Add(Heading("游戏音量",null,volumeLabel));volume.Margin=new Padding(0,5,0,0);sound.Controls.Add(volume);
   sound.Controls.Add(Note("仅页面 A 播放声音",20));volume.ValueChanged+=(s,e)=>UpdateAudio();sidebar.Controls.Add(sound);
   var game=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,ColumnCount=1,Margin=Padding.Empty,BackColor=Theme.Surface,Padding=new Padding(1,0,1,1)};
   game.RowStyles.Add(new RowStyle(SizeType.Absolute,62));game.RowStyles.Add(new RowStyle(SizeType.Percent,100));body.Controls.Add(game,1,0);
   var nav=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Padding=new Padding(18,0,14,0),Margin=Padding.Empty};nav.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,120));nav.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));nav.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,110));
   var gameTitle=new Label{Text=Program.IsolatedTest?"输入验证":"造梦西游 3",Font=new Font(Font.FontFamily,11F,FontStyle.Bold),Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft};nav.Controls.Add(gameTitle,0,0);
   address.Dock=DockStyle.Fill;address.ForeColor=Theme.Muted;address.TextAlign=ContentAlignment.MiddleLeft;address.AutoEllipsis=true;nav.Controls.Add(address,1,0);
   var refresh=Button("↻  刷新页面",async()=>{if(active!=null){inputGeneration++;var target=active;await target.StopAsync();await target.RequestAsync("refresh");}});refresh.Width=104;refresh.Height=34;refresh.Anchor=AnchorStyles.Right;refresh.Margin=Padding.Empty;nav.Controls.Add(refresh,2,0);
   tips.SetToolTip(refresh,"重新载入当前页面；请先保存游戏进度");game.Controls.Add(nav,0,0);stage.Margin=Padding.Empty;game.Controls.Add(stage,0,1);
   var footer=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty};
   var shortcuts=new Label{Text="F7  开始 / 停止连点     ·     F8  停止并恢复 1×",Dock=DockStyle.Right,Width=380,TextAlign=ContentAlignment.MiddleRight,ForeColor=Theme.Muted,Font=new Font(Font.FontFamily,8F)};footer.Controls.Add(shortcuts);
   status.Dock=DockStyle.Fill;status.ForeColor=Theme.Muted;status.TextAlign=ContentAlignment.MiddleLeft;status.AutoEllipsis=true;status.Font=new Font(Font.FontFamily,8F);footer.Controls.Add(status);status.BringToFront();layout.Controls.Add(footer,0,2);
   stage.Resize+=async(s,e)=>{if(active!=null)await active.ResizeAsync();};
   Shown+=async(s,e)=>{RegisterControls();timer.Start();await AddPageUiAsync();if(Program.IntegrationTest)await IntegrationTests.RunAsync(this);};
   timer.Tick+=(s,e)=>{RegisterControls();UpdateStatus();};
   Deactivate+=async(s,e)=>{if(!closing&&!background.Checked&&GetAncestor(GetForegroundWindow(),2)!=Handle)await StopAllInputsAsync();};
   FormClosing+=async(s,e)=>{if(canClose)return;e.Cancel=true;if(closing)return;closing=true;timer.Stop();Enabled=false;await Task.Yield();
    await EmergencyStopAsync();if(hotkey)UnregisterHotKey(Handle,0x5133);if(inputHotkey)UnregisterHotKey(Handle,0x5132);audio.Dispose();await Task.WhenAll(sessions.Select(x=>x.CloseAsync()));canClose=true;Close();};
   FormClosed+=(s,e)=>{tips.Dispose();timer.Dispose();};UpdateAudio();
  }
  async Task AddPageUiAsync(){try{await AddSessionAsync();}catch(Exception ex){status.Text="新建失败："+ex.Message;File.AppendAllText(Path.Combine(Program.DataRoot,"shell-error.log"),ex+Environment.NewLine);}}
  internal async Task<GameSession> AddSessionAsync(){
   if(creating||closing||sessions.Count>=4)return null;creating=true;add.Enabled=false;GameSession session=null;var source=active;
   try{
    Newtonsoft.Json.Linq.JToken cookies=null;
    if(source!=null&&source.Ready.Status==TaskStatus.RanToCompletion)cookies=(await source.RequestAsync("exportCookies")).Data["cookies"];
    var surface=new Panel{Dock=DockStyle.Fill,BackColor=Theme.Surface};stage.Controls.Add(surface);var handle=surface.Handle;
    string name=new[]{"A","B","C","D"}.First(n=>sessions.All(s=>s.Name!=n));session=new GameSession(name,surface);session.Speed.Set(sharedSpeed);sessions.Add(session);
    var tab=new PageTab{Panel=new Panel{Width=268,Height=56,Margin=new Padding(0,0,0,4)},Select=new PageButton{PageName=name,Text="页面 "+name,AccessibleName="页面 "+name+(name=="A"?"，主页面":"，静音"),TextAlign=ContentAlignment.MiddleLeft,Dock=DockStyle.Fill}};tab.Select.Click+=async(s,e)=>await SelectSessionAsync(session);tab.Panel.Controls.Add(tab.Select);
    if(name!="A"){tab.Close=new StudioButton{Text="×",Width=30,Dock=DockStyle.Right,Quiet=true,AccessibleName="关闭页面 "+name};tips.SetToolTip(tab.Close,"关闭页面 "+name+"，请先保存进度");tab.Close.Click+=async(s,e)=>{try{await CloseSessionAsync(session);}catch(Exception ex){status.Text=ex.Message;}};tab.Panel.Controls.Add(tab.Close);}
    if(DeviceDpi!=96)tab.Panel.Scale(new SizeF(DeviceDpi/96f,DeviceDpi/96f));tabButtons[session]=tab;tabs.Controls.Add(tab.Panel);tabs.Height=(int)(sessions.Count*60*DeviceDpi/96f);
    session.Changed+=changed=>{if(!IsDisposed&&!closing)try{BeginInvoke(new Action(UpdateStatus));}catch(InvalidOperationException){}};
    await SelectSessionAsync(session);await session.StartAsync();UpdateAudio();await session.ResizeAsync();
    if(cookies!=null)await session.RequestAsync("importCookies",new{cookies});
    await session.RequestAsync("navigate",new{url=Program.TestPage||Program.IntegrationTest||Program.FlashPath==null?Program.TestUrl:Program.GameUrl});return session;
   }catch{if(session!=null){if(active==session)active=null;RemoveSessionUi(session);await session.CloseAsync();if(active==null&&source!=null&&sessions.Contains(source))await SelectSessionAsync(source);}throw;}
   finally{creating=false;add.Enabled=sessions.Count<4;}
  }
  void RemoveSessionUi(GameSession session){sessions.Remove(session);if(tabButtons.TryGetValue(session,out var tab)){tabs.Controls.Remove(tab.Panel);tabButtons.Remove(session);tab.Panel.Dispose();}tabs.Height=(int)(sessions.Count*60*DeviceDpi/96f);UpdateAudio();}
  internal async Task<bool> CloseSessionAsync(GameSession session){
   if(session==null||session.Name=="A"||creating||closing||!sessions.Contains(session)||!removing.Add(session))return false;
   try{inputGeneration++;if(tabButtons.TryGetValue(session,out var tab))tab.Panel.Enabled=false;
    await session.StopAsync();if(active==session)await SelectSessionAsync(sessions.First(s=>s.Name=="A"));
    // Keep the name reserved until the worker and its cache handles are gone.
    await session.CloseAsync();RemoveSessionUi(session);add.Enabled=sessions.Count<4;UpdateStatus();return true;
   }finally{removing.Remove(session);}
  }
  internal async Task SelectSessionAsync(GameSession session){
   if(active==session||!sessions.Contains(session)||removing.Contains(session))return;inputGeneration++;var previous=active;
   if(previous!=null){previous.Surface.Visible=false;_ =SuspendPageAsync(previous);}
   active=session;session.Surface.Visible=true;session.Surface.BringToFront();UpdateStatus();
   if(session.Ready.Status==TaskStatus.RanToCompletion){await session.ResizeAsync();if(active==session)await session.RequestAsync("visibility",new{visible=true});}
  }
  async Task SuspendPageAsync(GameSession session){session.Running=false;try{if(session.Ready.Status==TaskStatus.RanToCompletion)await session.RequestAsync("visibility",new{visible=false});}catch(Exception ex){if(!closing)session.Status=ex.Message;}}
  async Task StopQuietly(GameSession session){try{await session.StopAsync();}catch(Exception ex){if(!closing)status.Text=ex.Message;}}
  internal void SetGameSpeed(double value){if(Array.IndexOf(SpeedController.SupportedSpeeds,value)<0)throw new ArgumentOutOfRangeException(nameof(value));sharedSpeed=value;foreach(var session in sessions.Where(s=>!removing.Contains(s)))session.Speed.Set(value);UpdateStatus();}
  internal void SetVolume(int value){volume.Value=Math.Max(0,Math.Min(100,value));UpdateAudio();}
  void UpdateAudio(){audio.SetTargets(sessions.Where(s=>s.ProcessId>0&&!removing.Contains(s)).Select(s=>new GameAudio.Target{ProcessId=s.ProcessId,Name=s.Name,Volume=s.Name=="A"?volume.Value/100f:0}));volumeLabel.Text=volume.Value==0?"已静音":volume.Value+"%";volumeLabel.ForeColor=Theme.Blue;}
  void RegisterControls(){if(!hotkey)hotkey=RegisterHotKey(Handle,0x5133,0x4000,0x77);if(!inputHotkey)inputHotkey=RegisterHotKey(Handle,0x5132,0x4000,0x76);}
  void UpdateStatus(){if(active==null||closing)return;
   foreach(var pair in tabButtons){bool current=pair.Key==active;pair.Value.Select.Selected=current;if(pair.Value.Close!=null)pair.Value.Close.Selected=current;}
   foreach(var pair in speedButtons)pair.Value.Selected=pair.Key==sharedSpeed;
   SetText(pageCount,sessions.Count+" / 4");SetText(address,"4399 在线版    /    页面 "+active.Name);
   SetText(status,"●  "+(active.Status=="就绪"?"游戏已就绪":active.Status)+(!inputHotkey?"   ·   F7 已被占用":"")+(!hotkey?"   ·   F8 已被占用":"")+(audio.Error!=null?"   ·   音频设备暂不可用":""));
   SetText(start,active.Running?"停止连点":"开始连点");int enabled=keyboard.ReadSteps().Count(x=>x.Enabled);
   SetText(inputState,active.Running?"正在页面 "+active.Name+" 执行 · 切页自动停止":enabled==0?"启用按键后，按 F7 开始":"已启用 "+enabled+" 个按键 · 按顺序循环");
  }
  async Task NavigateAsync(string url){if(active==null)return;inputGeneration++;var target=active;await target.StopAsync();await target.RequestAsync("navigate",new{url});}
  internal async Task ToggleInputAsync(){if(active==null||inputBusy||closing||removing.Contains(active))return;inputBusy=true;var target=active;int generation=inputGeneration;
   try{if(target.Running){await target.StopAsync();return;}var steps=keyboard.ReadSteps();KeyboardStep.Prepare(steps);await target.RequestAsync("start",new{steps});if(generation==inputGeneration&&target==active&&!closing){target.Running=true;target.Status="就绪";}else await target.StopAsync();
   }catch(Exception ex){target.Status=ex.Message;}finally{inputBusy=false;UpdateStatus();}
  }
  internal void SetKeyboardSteps(IEnumerable<KeyboardStep> steps){keyboard.SetSteps(steps);}
  internal async Task StopAllInputsAsync(){inputGeneration++;await Task.WhenAll(sessions.Select(StopQuietly));UpdateStatus();}
  internal async Task EmergencyStopAsync(){SetGameSpeed(1);await StopAllInputsAsync();}
  protected override void WndProc(ref System.Windows.Forms.Message m){if(m.Msg==0x312){if(m.WParam.ToInt32()==0x5133){_ =EmergencyStopAsync();return;}if(m.WParam.ToInt32()==0x5132){if(keyboard.CapturingKey){SetText(inputState,"F7 / F8 是控制热键，请选择其他按键");return;}if(active!=null&&(active.Running||background.Checked||GetAncestor(GetForegroundWindow(),2)==Handle))_ =ToggleInputAsync();return;}}base.WndProc(ref m);}
  static void SetText(Control c,string text){if(c.Text!=text)c.Text=text;}
  static Control Heading(string text,string detail=null,Label accessory=null){var panel=new Panel{Width=268,Height=24,Margin=Padding.Empty};panel.Controls.Add(new Label{Text=text,Font=new Font("Microsoft YaHei UI",9.5F,FontStyle.Bold),Dock=DockStyle.Left,Width=110,TextAlign=ContentAlignment.MiddleLeft});if(detail!=null||accessory!=null){var label=accessory??new Label{Text=detail};label.ForeColor=Theme.Muted;label.Dock=DockStyle.Right;label.Width=145;label.TextAlign=ContentAlignment.MiddleRight;label.Font=new Font("Microsoft YaHei UI",8F);panel.Controls.Add(label);}return panel;}
  static Label Note(string text,int height){var label=new Label{Text=text};StyleNote(label,height);return label;}
  static void StyleNote(Label c,int height){c.Width=268;c.Height=height;c.ForeColor=Theme.Muted;c.Margin=new Padding(0,3,0,2);c.Font=new Font("Microsoft YaHei UI",8F);}
  static void StyleButton(StudioButton b,bool primary=false){b.Width=268;b.Height=36;b.Margin=new Padding(0,4,0,2);b.Primary=primary;}
  StudioButton Button(string text,Func<Task> action){var b=new StudioButton{Text=text};StyleButton(b);b.Click+=async(s,e)=>{try{await action();}catch(Exception ex){if(active!=null)active.Status=ex.Message;status.Text=ex.Message;}};return b;}
  void ShowHelp(){
   using(var help=new Form{Text="使用说明 · 造梦工坊",ClientSize=new Size(450,442),FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,ShowInTaskbar=false,StartPosition=FormStartPosition.CenterParent,BackColor=Theme.Background,ForeColor=Theme.Text,Font=Font,AutoScaleMode=AutoScaleMode.Dpi,AutoScaleDimensions=new SizeF(96,96)}){
    help.Controls.Add(new BrandMark{Location=new Point(26,24),Size=new Size(40,40)});
    help.Controls.Add(new Label{Text="造梦工坊",Font=new Font(Font.FontFamily,16F,FontStyle.Bold),AutoSize=true,Location=new Point(79,23)});
    help.Controls.Add(new Label{Text="0.6.0 · 造梦西游 3 在线版",ForeColor=Theme.Muted,AutoSize=true,Location=new Point(82,54)});
    help.Controls.Add(new Label{Text="多个页面，一个账号\n左侧新建页面会继承登录状态。A 保留声音，其余页面静音。关闭或刷新前，请先在游戏内保存进度。\n\n键盘连点\n点击按键框后直接按键，启用所需行，再按 F7 开始或停止。从上到下循环；间隔为松开按键后的等待时间。运行中修改，下次启动生效。\n\n快捷控制\n变速和连点设置由所有页面共享。切换页面会停止连点；F8 停止全部连点并恢复 1×。",Location=new Point(28,95),Size=new Size(394,272),Font=new Font(Font.FontFamily,9F)});
    var done=new StudioButton{Text="知道了",Primary=true,Bounds=new Rectangle(300,382,120,36),DialogResult=DialogResult.OK};help.Controls.Add(done);help.AcceptButton=done;help.CancelButton=done;help.ShowDialog(this);
   }
  }
 }
}

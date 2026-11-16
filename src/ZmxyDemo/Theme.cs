using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ZmxyDemo {
 internal static class Theme {
  public static readonly Color Background=Color.FromArgb(244,246,242),Surface=Color.White,
   Text=Color.FromArgb(36,57,49),Muted=Color.FromArgb(117,132,123),Blue=Color.FromArgb(38,113,88),
   Tint=Color.FromArgb(231,242,235),Line=Color.FromArgb(224,232,225),Soft=Color.FromArgb(247,249,246),Danger=Color.FromArgb(165,79,64);
  public static GraphicsPath Round(RectangleF r,float radius){var p=new GraphicsPath();float d=Math.Min(radius*2,Math.Min(r.Width,r.Height));if(d<=0)return p;
   p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
  public static void Fill(Graphics g,RectangleF r,Color color,float radius){using(var p=Round(r,radius))using(var b=new SolidBrush(color))g.FillPath(b,p);}
  public static void Outline(Graphics g,RectangleF r,Color color,float radius){using(var p=Round(r,radius))using(var pen=new Pen(color))g.DrawPath(pen,p);}
  public static void Button(Button b,bool primary=false){b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderSize=0;b.BackColor=primary?Blue:Soft;b.ForeColor=primary?Color.White:Text;b.Cursor=Cursors.Hand;b.TabStop=true;b.UseVisualStyleBackColor=false;}
  public static void Mark(Graphics g,RectangleF r){Theme.Fill(g,r,Blue,r.Width*.28f);var state=g.Save();g.TranslateTransform(r.X,r.Y);g.ScaleTransform(r.Width/40f,r.Height/40f);
   using(var p=new Pen(Color.FromArgb(241,249,237),2.2f){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round}){g.DrawLines(p,new[]{new PointF(9,27),new PointF(18,14),new PointF(23,21),new PointF(27,17),new PointF(32,27)});g.DrawLine(p,13,29,28,29);g.DrawArc(p,26,8,6,6,30,270);}g.Restore(state);}
 }
 // No animation timers: repaint only when input, focus or state changes.
 internal class StudioButton:Button {
  bool hover,down,selected;string shortcut="";
  public bool Primary {get;set;}public bool Quiet {get;set;}
  public bool Selected {get=>selected;set{if(selected==value)return;selected=value;Invalidate();}}
  public string Shortcut {get=>shortcut;set{if(shortcut==value)return;shortcut=value;Invalidate();}}
  public StudioButton(){Theme.Button(this);SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
  protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
  protected override void OnMouseLeave(EventArgs e){hover=false;down=false;Invalidate();base.OnMouseLeave(e);}
  protected override void OnMouseDown(MouseEventArgs e){down=true;Invalidate();base.OnMouseDown(e);}
  protected override void OnMouseUp(MouseEventArgs e){down=false;Invalidate();base.OnMouseUp(e);}
  protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}
  protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}
  protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Parent?.BackColor??Theme.Surface);float s=g.DpiX/96f;var r=new RectangleF(.5f,.5f,Width-1,Height-1);
   var color=Primary?Theme.Blue:Selected?Theme.Tint:Quiet?Theme.Surface:Theme.Soft;if(hover||down)color=Primary?Color.FromArgb(29,94,73):Color.FromArgb(222,236,226);if(!Enabled)color=Theme.Soft;
   Theme.Fill(g,r,color,9*s);if(!Primary&&!Quiet&&!Selected)Theme.Outline(g,r,Theme.Line,9*s);if(Focused&&ShowFocusCues)Theme.Outline(g,new RectangleF(2,2,Width-5,Height-5),Primary?Color.White:Theme.Blue,8*s);
   DrawContent(g,Rectangle.Inflate(ClientRectangle,-(int)((Width<45*s?3:12)*s),0),!Enabled?Theme.Muted:Primary?Color.White:Selected?Theme.Blue:ForeColor,s);}
  protected virtual void DrawContent(Graphics g,Rectangle r,Color color,float s){var flags=TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix;
   if(!String.IsNullOrEmpty(Shortcut)){int w=(int)(33*s);var badge=new Rectangle(r.Right-w,r.Top+(r.Height-(int)(22*s))/2,w,(int)(22*s));Theme.Fill(g,badge,Primary?Color.FromArgb(64,133,108):Theme.Tint,5*s);TextRenderer.DrawText(g,Shortcut,Font,badge,color,flags|TextFormatFlags.HorizontalCenter);r.Width-=w+(int)(7*s);}
   TextRenderer.DrawText(g,Text,Font,r,color,flags|(TextAlign==ContentAlignment.MiddleLeft?TextFormatFlags.Left:TextFormatFlags.HorizontalCenter));}
 }
 internal sealed class PageButton:StudioButton {
  public string PageName;
  protected override void DrawContent(Graphics g,Rectangle r,Color color,float s){int size=(int)(32*s);var badge=new Rectangle(r.Left,r.Top+(r.Height-size)/2,size,size);Theme.Fill(g,badge,Selected?Theme.Blue:Theme.Tint,8*s);
   using(var f=new Font("Segoe UI",10,FontStyle.Bold))TextRenderer.DrawText(g,PageName,f,badge,Selected?Color.White:Theme.Blue,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
   int x=badge.Right+(int)(11*s),y=r.Top+(int)(8*s);TextRenderer.DrawText(g,"页面 "+PageName,Font,new Rectangle(x,y,r.Right-x,(int)(22*s)),color,TextFormatFlags.Left|TextFormatFlags.VerticalCenter);
   using(var f=new Font("Microsoft YaHei UI",7.5f))TextRenderer.DrawText(g,PageName=="A"?"主页面 · 声音开启":"独立页面 · 已静音",f,new Rectangle(x,y+(int)(21*s),r.Right-x,(int)(17*s)),Theme.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter);}
 }
 internal sealed class StudioCard:FlowLayoutPanel {
  public StudioCard(){Width=296;FlowDirection=FlowDirection.TopDown;WrapContents=false;AutoSize=true;AutoSizeMode=AutoSizeMode.GrowAndShrink;Padding=new Padding(14,12,14,12);Margin=new Padding(0,0,0,12);BackColor=Theme.Surface;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
  protected override void OnPaintBackground(PaintEventArgs e){var g=e.Graphics;g.Clear(Parent?.BackColor??Theme.Background);g.SmoothingMode=SmoothingMode.AntiAlias;Theme.Fill(g,new RectangleF(.5f,.5f,Width-1,Height-1),Theme.Surface,13*g.DpiX/96f);Theme.Outline(g,new RectangleF(.5f,.5f,Width-1,Height-1),Theme.Line,13*g.DpiX/96f);}
 }
 internal sealed class BrandMark:Control {
  public BrandMark(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
  protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;Theme.Mark(e.Graphics,new RectangleF(1,1,Width-2,Height-2));}
 }
 internal sealed class ToggleSwitch:CheckBox {
  public ToggleSwitch(){AutoSize=false;Cursor=Cursors.Hand;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
  protected override void OnCheckedChanged(EventArgs e){Invalidate();base.OnCheckedChanged(e);}
  protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(BackColor);float s=g.DpiX/96f;var r=new RectangleF(2,(Height-18*s)/2,32*s,18*s);Theme.Fill(g,r,Checked?Theme.Blue:Color.FromArgb(212,221,214),9*s);
   using(var b=new SolidBrush(Color.White))g.FillEllipse(b,r.Left+(Checked?16:3)*s,r.Top+3*s,12*s,12*s);if(Focused&&ShowFocusCues)Theme.Outline(g,new RectangleF(0,r.Top-2,r.Width+4,r.Height+4),Theme.Blue,11*s);
   if(!String.IsNullOrEmpty(Text))TextRenderer.DrawText(g,Text,Font,new Rectangle((int)(43*s),0,Width-(int)(43*s),Height),ForeColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);}
 }
 internal sealed class VolumeSlider:Control {
  int value=100;public int Value {get=>value;set{int next=Math.Max(0,Math.Min(100,value));if(this.value==next)return;this.value=next;Invalidate();ValueChanged?.Invoke(this,EventArgs.Empty);}}public event EventHandler ValueChanged;
  public VolumeSlider(){Height=28;Width=268;Cursor=Cursors.Hand;TabStop=true;AccessibleRole=AccessibleRole.Slider;AccessibleName="页面 A 音量";SetStyle(ControlStyles.Selectable|ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
  protected override bool IsInputKey(Keys k)=>k==Keys.Left||k==Keys.Right||k==Keys.Up||k==Keys.Down||base.IsInputKey(k);
  protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Left||e.KeyCode==Keys.Down)Value--;else if(e.KeyCode==Keys.Right||e.KeyCode==Keys.Up)Value++;else if(e.KeyCode==Keys.Home)Value=0;else if(e.KeyCode==Keys.End)Value=100;else{base.OnKeyDown(e);return;}e.Handled=true;}
  void SetFromX(int x){float edge=10*DeviceDpi/96f;Value=(int)Math.Round((x-edge)*100/Math.Max(1,Width-edge*2));}
  protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left){Focus();Capture=true;SetFromX(e.X);}}
  protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(Capture&&e.Button==MouseButtons.Left)SetFromX(e.X);}
  protected override void OnMouseUp(MouseEventArgs e){Capture=false;base.OnMouseUp(e);}
  protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}
  protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}
  protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(BackColor);float s=g.DpiX/96f,edge=10*s,x=edge+(Width-edge*2)*Value/100f,y=Height/2f;
   Theme.Fill(g,new RectangleF(edge,y-2*s,Width-edge*2,4*s),Theme.Line,2*s);if(Value>0)Theme.Fill(g,new RectangleF(edge,y-2*s,x-edge,4*s),Theme.Blue,2*s);using(var b=new SolidBrush(Theme.Blue))g.FillEllipse(b,x-7*s,y-7*s,14*s,14*s);using(var b=new SolidBrush(Color.White))g.FillEllipse(b,x-3*s,y-3*s,6*s,6*s);if(Focused&&ShowFocusCues)using(var p=new Pen(Theme.Blue))g.DrawEllipse(p,x-9*s,y-9*s,18*s,18*s);}
  protected override AccessibleObject CreateAccessibilityInstance()=>new SliderAccessibility(this);
  sealed class SliderAccessibility:ControlAccessibleObject {readonly VolumeSlider owner;public SliderAccessibility(VolumeSlider owner):base(owner){this.owner=owner;}public override string Value{get=>owner.Value+"%";set{if(Int32.TryParse(value.TrimEnd('%'),out int v))owner.Value=v;}}}
 }
 internal sealed class FieldFrame:Panel {
  public FieldFrame(){BackColor=Theme.Soft;Padding=new Padding(8,6,8,5);SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
  protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent?.BackColor??Theme.Surface);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;Theme.Fill(e.Graphics,new RectangleF(.5f,.5f,Width-1,Height-1),Theme.Soft,6*e.Graphics.DpiX/96f);}
  protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;Theme.Outline(e.Graphics,new RectangleF(.5f,.5f,Width-1,Height-1),ContainsFocus?Theme.Blue:Theme.Line,6*e.Graphics.DpiX/96f);}
  protected override void OnEnter(EventArgs e){Invalidate();base.OnEnter(e);}
  protected override void OnLeave(EventArgs e){Invalidate();base.OnLeave(e);}
 }
}

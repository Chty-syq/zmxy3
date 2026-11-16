using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ZmxyDemo {
 internal sealed class KeyCaptureBox:TextBox {
  public int KeyCode {get;private set;}
  public event Action KeyChanged;
  public KeyCaptureBox(){ReadOnly=true;ShortcutsEnabled=false;TextAlign=HorizontalAlignment.Center;BackColor=Theme.Soft;ForeColor=Theme.Text;BorderStyle=BorderStyle.None;Cursor=Cursors.IBeam;}
  public void SetKey(int key){KeyCode=key;Text=Label(key);}
  protected override bool IsInputKey(Keys keyData)=>true;
  protected override bool ProcessCmdKey(ref System.Windows.Forms.Message msg,Keys keyData){Capture(keyData);return true;}
  protected override void OnKeyDown(KeyEventArgs e){Capture(e.KeyData);e.Handled=true;e.SuppressKeyPress=true;}
  protected override void OnKeyPress(KeyPressEventArgs e){e.Handled=true;}
  void Capture(Keys key){if((key&Keys.Modifiers)!=0||!KeyboardStep.KeyAllowed((int)(key&Keys.KeyCode)))return;SetKey((int)(key&Keys.KeyCode));KeyChanged?.Invoke();}
  static string Label(int key){switch(key){case 32:return "空格";case 37:return "←";case 38:return "↑";case 39:return "→";case 40:return "↓";case 13:return "Enter";case 9:return "Tab";case 27:return "Esc";}return key>=112&&key<=123?"F"+(key-111):((char)key).ToString();}
 }
 internal sealed class KeyboardSequenceEditor:UserControl {
  sealed class Row {
   public Panel Panel;public KeyCaptureBox Key;public NumericUpDown Interval;public CheckBox Enabled;public Button Remove;
   public KeyboardStep Read()=>new KeyboardStep{Key=Key.KeyCode,IntervalMs=(int)Interval.Value,Enabled=Enabled.Checked};
  }
  readonly List<Row> rows=new List<Row>();
  readonly FlowLayoutPanel list=new FlowLayoutPanel{Location=new Point(0,26),Width=268,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Margin=Padding.Empty};
  readonly StudioButton add=new StudioButton{Quiet=true};readonly ToolTip tips=new ToolTip();bool loading;
  public event Action Changed;
  public bool CapturingKey=>rows.Any(row=>row.Key.Focused);
  public KeyboardSequenceEditor(){
   Width=268;AutoScaleMode=AutoScaleMode.None;Margin=new Padding(0,8,0,3);BackColor=Theme.Surface;
   Header("按键",0,74);Header("间隔 · ms",84,90);Header("启用",178,40);Controls.Add(list);
   add.Text="＋  添加按键";add.Width=248;add.Height=30;add.ForeColor=Theme.Blue;add.Click+=(s,e)=>{if(rows.Count>=32)return;var row=AddRow(new KeyboardStep{Key=83,IntervalMs=1,Enabled=false});LayoutRows();list.ScrollControlIntoView(row.Panel);row.Key.Focus();OnChanged();};Controls.Add(add);
   SetSteps(KeyboardStep.Defaults());
  }
  void Header(string text,int x,int width){Controls.Add(new Label{Text=text,ForeColor=Theme.Muted,Location=new Point(x,0),Size=new Size(width,23)});}
  Row AddRow(KeyboardStep step){
   var row=new Row{Panel=new Panel{Width=248,Height=36,Margin=new Padding(0,0,0,6)},Key=new KeyCaptureBox(),Interval=new NumericUpDown{Minimum=1,Maximum=60000,Increment=1,BackColor=Theme.Soft,ForeColor=Theme.Text,BorderStyle=BorderStyle.None},Enabled=new ToggleSwitch(),Remove=new StudioButton{Text="×",Quiet=true,AccessibleName="删除此行",Padding=Padding.Empty}};
   var keyFrame=new FieldFrame{Bounds=new Rectangle(0,0,74,34)};var intervalFrame=new FieldFrame{Bounds=new Rectangle(84,0,84,34)};
   row.Key.Dock=DockStyle.Fill;row.Interval.Dock=DockStyle.Fill;keyFrame.Controls.Add(row.Key);intervalFrame.Controls.Add(row.Interval);
   row.Enabled.SetBounds(178,0,37,34);row.Remove.SetBounds(220,2,28,30);row.Remove.ForeColor=Theme.Muted;
   row.Key.SetKey(KeyboardStep.KeyAllowed(step.Key)?step.Key:83);row.Interval.Value=Math.Max(1,Math.Min(60000,step.IntervalMs));row.Enabled.Checked=step.Enabled;
   tips.SetToolTip(row.Key,"点击后直接按下一个键；F7 / F8 为控制热键");tips.SetToolTip(row.Remove,"删除此行");
   row.Panel.Controls.AddRange(new Control[]{keyFrame,intervalFrame,row.Enabled,row.Remove});if(IsHandleCreated&&DeviceDpi!=96)row.Panel.Scale(new SizeF(DeviceDpi/96f,DeviceDpi/96f));list.Controls.Add(row.Panel);rows.Add(row);
   row.Key.KeyChanged+=OnChanged;row.Interval.ValueChanged+=(s,e)=>OnChanged();row.Enabled.CheckedChanged+=(s,e)=>OnChanged();
   row.Remove.Click+=(s,e)=>{rows.Remove(row);list.Controls.Remove(row.Panel);row.Panel.Dispose();LayoutRows();OnChanged();};return row;
  }
  void LayoutRows(){float s=IsHandleCreated?DeviceDpi/96f:1;list.Height=(int)(Math.Max(42,Math.Min(5,rows.Count)*42)*s);add.Top=list.Bottom+(int)(3*s);Height=add.Bottom+(int)(2*s);add.Enabled=rows.Count<32;for(int i=0;i<rows.Count;i++){rows[i].Key.AccessibleName="第 "+(i+1)+" 行按键";rows[i].Interval.AccessibleName="第 "+(i+1)+" 行间隔";rows[i].Enabled.AccessibleName="启用第 "+(i+1)+" 行";rows[i].Remove.AccessibleName="删除第 "+(i+1)+" 行";}}
  void OnChanged(){if(!loading)Changed?.Invoke();}
  public List<KeyboardStep> ReadSteps()=>rows.Select(row=>row.Read()).ToList();
  public void SetSteps(IEnumerable<KeyboardStep> steps){loading=true;try{foreach(var row in rows)row.Panel.Dispose();rows.Clear();list.Controls.Clear();foreach(var step in steps)AddRow(step);LayoutRows();}finally{loading=false;}}
  protected override void Dispose(bool disposing){if(disposing)tips.Dispose();base.Dispose(disposing);}
 }
}

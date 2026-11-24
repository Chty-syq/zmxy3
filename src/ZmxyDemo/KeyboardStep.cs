using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace ZmxyDemo {
 public sealed class KeyboardStep {
  public int Key { get; set; } = 74;
  public int IntervalMs { get; set; } = 100;
  public bool Enabled { get; set; } = true;
  public KeyboardStep Copy()=>new KeyboardStep{Key=Key,IntervalMs=IntervalMs,Enabled=Enabled};
  public static List<KeyboardStep> Defaults()=>new[]{83,83,74}.Select(key=>new KeyboardStep{Key=key,IntervalMs=1,Enabled=false}).ToList();
  internal static bool IsNumpadDigit(int key) => key >= (int)Keys.NumPad0 && key <= (int)Keys.NumPad9;

  public static bool KeyAllowed(int key) =>
      (key >= (int)Keys.A && key <= (int)Keys.Z) ||
      (key >= (int)Keys.D0 && key <= (int)Keys.D9) ||
      IsNumpadDigit(key) ||
      (key >= (int)Keys.Left && key <= (int)Keys.Down) ||
      key == (int)Keys.Space || key == (int)Keys.Enter || key == (int)Keys.Tab || key == (int)Keys.Escape ||
      (key >= (int)Keys.F1 && key <= (int)Keys.F12 && key != (int)Keys.F7 && key != (int)Keys.F8);
  public static KeyboardStep[] Prepare(IEnumerable<KeyboardStep> steps){
   if(steps==null)throw new ArgumentException("请先设置键盘列表");
   var rows=steps.ToArray();if(rows.Length>32)throw new ArgumentException("最多设置 32 行按键");
   var enabled=new List<KeyboardStep>();
   foreach(var row in rows){
    if(row==null)throw new ArgumentException("按键设置无效");
    if(!row.Enabled)continue;
    if(!KeyAllowed(row.Key))throw new ArgumentException("不支持此按键；F7/F8 为控制热键");
    if(row.IntervalMs<1||row.IntervalMs>60000)throw new ArgumentException("间隔需为 1～60000 毫秒");
    enabled.Add(row.Copy());
   }
   if(enabled.Count==0)throw new ArgumentException("请至少启用一行按键");
   return enabled.ToArray();
  }
 }
}

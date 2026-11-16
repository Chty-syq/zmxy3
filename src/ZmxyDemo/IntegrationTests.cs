using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ZmxyDemo {
 internal static class IntegrationTests {
  static async Task<JObject> Probe(GameSession session){for(int i=0;i<80;i++){try{var r=await session.RequestAsync("probe");var value=(string)r.Data["state"];if(!String.IsNullOrEmpty(value)&&value!="null"){var state=JObject.Parse(value);state["dispatch"]=r.Data["dispatch"];state["muted"]=r.Data["muted"];state["externalLinks"]=r.Data["externalLinks"];return state;}}catch(Exception){}await Task.Delay(150);}throw new Exception("Local test page failed to load");}
  static async Task<JObject> Until(GameSession session,Func<JObject,bool> condition){for(int i=0;i<120;i++){var state=await Probe(session);if(condition(state))return state;await Task.Delay(20);}throw new Exception("Timed out waiting for keyboard input");}
  public static async Task RunAsync(MainForm form){var results=new List<object>();
   Action<bool,string> check=(ok,name)=>{results.Add(new{name,ok});if(!ok)throw new Exception(name);};
   try{
    var a=form.Active;await Probe(a);check(!a.Speed.HooksActive,"normal speed has no clock hooks");
    check(form.SharedKeyboardSteps.Select(s=>s.Key).SequenceEqual(new[]{83,83,74})&&form.SharedKeyboardSteps.All(s=>s.IntervalMs==1&&!s.Enabled),"default rows are S S J at 1 ms and disabled");
    form.Activate();JObject focusBefore=null;for(int i=0;i<100;i++){await a.RequestAsync("focus");focusBefore=await Probe(a);if((bool)focusBefore["hasFocus"])break;await Task.Delay(150);}check((bool)focusBefore["hasFocus"],"test page obtains foreground focus");
    form.SetKeyboardSteps(new[]{new KeyboardStep{Key=74,IntervalMs=70},new KeyboardStep{Key=76,IntervalMs=20,Enabled=false},new KeyboardStep{Key=75,IntervalMs=140},new KeyboardStep{Key=85,IntervalMs=40}});
    await form.ToggleInputAsync();await Until(a,s=>(int)s["keys"]>=7);await form.ToggleInputAsync();
    var aState=await Probe(a);var events=(JArray)aState["events"];var downs=events.Where(e=>(string)e["type"]=="down").ToArray();int[] expected={74,75,85};
    check(downs.Length>=7&&downs.Select((e,i)=>(int)e["key"]==expected[i%3]).All(ok=>ok),"enabled rows repeat in configured order and disabled rows are skipped");
    check((int)aState["held"]==0&&(int)aState["maxHeld"]==1&&!a.Running,"F7 toggle stops sequence and releases keys without overlapping presses");
    check((bool)aState["hasFocus"]&&(int)aState["blurCount"]==(int)focusBefore["blurCount"],"starting and stopping preserves native page focus without transient blur");
    results.Add(new{sequenceEvents=events});var releaseGaps=(JArray)aState["dispatch"]["releaseGaps"];bool timing=releaseGaps.Count>=6;foreach(var gap in releaseGaps){int key=(int)gap["key"];double expectedMs=key==74?70:key==75?140:40;if((double)gap["milliseconds"]<expectedMs-.5)timing=false;}
    check(timing,"each row waits its own interval after key release including loop boundary");int oldCount=(int)aState["keys"];
    form.SetGameSpeed(2);var b=await form.AddSessionAsync();var bState=await Probe(b);check(form.Sessions.Count==2&&(int)bState["keys"]==0,"second independent page without another shell");
    check(a.Speed.Speed==2&&b.Speed.Speed==2,"new page inherits shared speed");form.SetGameSpeed(1);
    check((bool)bState["muted"]&&!(bool)(await Probe(a))["muted"],"only page A permits browser audio");
    await form.SelectSessionAsync(a);aState=await Probe(a);check((int)aState["keys"]==oldCount,"switching preserves page state without reload");
    check(form.SharedKeyboardSteps.Count==4&&form.SharedKeyboardSteps[2].Key==75&&form.SharedKeyboardSteps[2].IntervalMs==140&&!form.SharedKeyboardSteps[1].Enabled,"page switching preserves shared keyboard settings");
    await form.ToggleInputAsync();await Until(a,s=>(int)s["held"]==1);
    await form.SelectSessionAsync(b);await Task.Delay(150);aState=await Probe(a);int hiddenCount=(int)aState["keys"];await Task.Delay(150);aState=await Probe(a);
    check((int)aState["keys"]==hiddenCount&&(int)aState["held"]==0&&!a.Running,"switching stops input and releases held keys on hidden page");
    for(int i=0;i<5;i++){var first=form.SelectSessionAsync(a);var second=form.SelectSessionAsync(b);await Task.WhenAll(first,second);}
    bState=await Probe(b);check((string)bState["visibility"]=="visible","rapid switching leaves current page visible");
    await form.SelectSessionAsync(b);form.SetGameSpeed(100);check(a.Speed.Speed==100&&b.Speed.Speed==100,"shared speed updates every page at 100x");
    form.SetKeyboardSteps(new[]{new KeyboardStep{Key=75,IntervalMs=60000}});await form.ToggleInputAsync();await Until(b,s=>(int)s["keys"]>=1&&(int)s["held"]==0);var stopTime=Stopwatch.StartNew();
    await form.EmergencyStopAsync();bState=await Probe(b);check((int)bState["keys"]>0&&(int)bState["held"]==0&&!b.Running&&a.Speed.Speed==1&&b.Speed.Speed==1,"stop all pages and restore speed");
    stopTime.Stop();check(stopTime.ElapsedMilliseconds<1000,"long interval is cancelled promptly");
    int stopped=(int)bState["keys"];await Task.Delay(200);bState=await Probe(b);check((int)bState["keys"]==stopped,"no queued input after stop");
    form.SetKeyboardSteps(new[]{new KeyboardStep{Key=74,Enabled=false}});await form.ToggleInputAsync();bState=await Probe(b);check(!b.Running&&(int)bState["keys"]==stopped&&b.Status.Contains("至少启用"),"all-disabled list does not start");
    bool invalidRejected=false;try{await b.RequestAsync("start",new{steps=new[]{new KeyboardStep{Key=118}}});}catch(InvalidOperationException){invalidRejected=true;}check(invalidRejected,"reserved F7 cannot be used as an automated key");
    foreach(int gap in new[]{1,2,5,10}){
     var before=await Probe(b);double since=((JArray)before["events"]).Select(e=>(double)e["time"]).DefaultIfEmpty(0).Max();
     form.SetKeyboardSteps(new[]{new KeyboardStep{Key=74,IntervalMs=gap}});check(form.SharedKeyboardSteps[0].IntervalMs==gap,"editor preserves "+gap+" ms setting");
     await form.ToggleInputAsync();await Task.Delay(350);await form.ToggleInputAsync();var sample=await Probe(b);
     var input=((JArray)sample["events"]).Where(e=>(double)e["time"]>since&&(string)e["type"]=="down").Select(e=>(double)e["time"]).Skip(4).ToArray();
     var dispatch=sample["dispatch"];double median=(double)dispatch["p50CycleMs"];
     results.Add(new{configuredGapMs=gap,receivedPresses=input.Length,meanReceivedCycleMs=input.Length<2?0:(input.Last()-input.First())/(input.Length-1),dispatch});
     check(input.Length>=8&&median>=gap*1.6&&median<gap*2+8&&(int)sample["held"]==0,"small interval "+gap+" ms runs without fixed 30 ms hold and stops cleanly");
    }
    results.Add(new{timing=bState["timing"]});
    await form.SelectSessionAsync(a);var audioStart=await a.RequestAsync("testAudio");results.Add(new{audioStart=audioStart.Data});form.SetVolume(25);
    for(int i=0;i<30&&!form.AudioSnapshot.Any(s=>s.Page=="A"&&Math.Abs(s.Volume-.25)<.01&&!s.Muted);i++)await Task.Delay(100);
    results.Add(new{audioSamples=form.AudioSnapshot,audioError=form.AudioError});check(form.AudioSnapshot.Any(s=>s.Page=="A"&&Math.Abs(s.Volume-.25)<.01&&!s.Muted),"volume slider controls page A native audio session");
    form.SetVolume(0);for(int i=0;i<30&&!form.AudioSnapshot.Any(s=>s.Page=="A"&&s.Muted&&s.Volume==0);i++)await Task.Delay(100);
    check(form.AudioSnapshot.Any(s=>s.Page=="A"&&s.Muted&&s.Volume==0),"zero volume mutes page A native audio session");form.SetVolume(100);
    check(!await form.CloseSessionAsync(a)&&form.Sessions.Contains(a),"page A cannot be closed");
    await form.SelectSessionAsync(b);check(await form.CloseSessionAsync(b)&&form.Active==a&&form.Sessions.Count==1,"closing active page returns to page A");
    var reopened=await form.AddSessionAsync();await Probe(reopened);check(reopened.Name=="B"&&(bool)(await Probe(reopened))["muted"],"closed page slot reopens cleanly and remains muted");
    var c=await form.AddSessionAsync();await Probe(c);check(c.Name=="C","new page names remain unique");
    check(await form.CloseSessionAsync(reopened)&&form.Active==c,"closing background page preserves current page");
    var reused=await form.AddSessionAsync();await Probe(reused);check(reused.Name=="B"&&form.Sessions.Select(s=>s.Name).Distinct().Count()==form.Sessions.Count,"free page slot is reused without duplicate names");
    await form.CloseSessionAsync(c);await form.CloseSessionAsync(reused);
    int opened=0;
    foreach(var click in new[]{new{link="link-self",middle=false,control=false},new{link="link-blank",middle=false,control=false},new{link="link-script",middle=false,control=false},new{link="link-other",middle=false,control=false},new{link="link-self",middle=true,control=false},new{link="link-self",middle=false,control=true}}){
     await a.RequestAsync("testLink",click);opened++;var observed=await Until(a,s=>((JArray)s["externalLinks"]).Count>=opened);await Task.Delay(80);
     check(((JArray)(await Probe(a))["externalLinks"]).Count==opened&&a.Url==Program.TestUrl,"link opens externally once without replacing game: "+click.link+(click.middle?" middle":"")+(click.control?" ctrl":""));
    }
    await a.RequestAsync("testAutoPopup");await Task.Delay(150);check(((JArray)(await Probe(a))["externalLinks"]).Count==opened,"automatic popups do not launch external browser");
   }catch(Exception ex){results.Add(new{error=ex.ToString()});Environment.ExitCode=1;}
   File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"integration-test-results.json"),JsonConvert.SerializeObject(results,Formatting.Indented));form.Close();
  }
 }
}

using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
class Browser : Form {
 readonly TabControl tabs = new TabControl { Dock=DockStyle.Fill };
 readonly ToolStrip bar = new ToolStrip();
 readonly ToolStripTextBox address = new ToolStripTextBox { AutoSize=false, Width=460 };
 readonly ToolStripStatusLabel status = new ToolStripStatusLabel();
 CoreWebView2Environment environment;
 readonly bool test;
 WebView2 Current { get { return tabs.SelectedTab == null ? null : tabs.SelectedTab.Controls[0] as WebView2; } }
 [STAThread] static void Main(string[] args) { Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new Browser(Array.IndexOf(args,"--self-test")>=0)); }
 public Browser(bool testing) {
  test=testing; Text="Clarenza | Browser"; Width=1200; Height=820; MinimumSize=new Size(850,500);
  Add("Back",delegate { if(Current!=null && Current.CanGoBack) Current.GoBack(); });
  Add("Forward",delegate { if(Current!=null && Current.CanGoForward) Current.GoForward(); });
  Add("Reload",delegate { if(Current!=null) Current.Reload(); });
  Add("Stop",delegate { if(Current!=null && Current.CoreWebView2!=null) Current.CoreWebView2.Stop(); });
  bar.Items.Add(address); address.KeyDown += delegate(object s, KeyEventArgs e) { if(e.KeyCode==Keys.Enter){ Go(); e.SuppressKeyPress=true; } };
  Add("Go",Go); Add("YouTube",delegate { Navigate("https://www.youtube.com/"); });
  Add("+ Tab",async delegate { try { if(environment!=null) await NewTab("https://www.bing.com/"); } catch(Exception e){status.Text=e.Message;} });
  Add("Close tab",delegate { if(tabs.TabPages.Count<=1){Close();return;} var page=tabs.SelectedTab; tabs.TabPages.Remove(page); page.Dispose(); });
  Add("Return to class",delegate { Close(); });
  var bottom=new StatusStrip(); bottom.Items.Add(status); Controls.Add(tabs); Controls.Add(bar); Controls.Add(bottom);
  tabs.SelectedIndexChanged+=delegate { Sync(); };
  Shown+=async delegate { try { environment=await CoreWebView2Environment.CreateAsync(null,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Clarenza",test?"BrowserTest":"Browser")); await NewTab(test?null:"https://www.youtube.com/"); if(test) await SelfTest(); } catch(Exception e){ status.Text=e.Message; if(test){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"browser-test.txt"),"FAILED: "+e);Close();} } };
 }
 void Add(string label, Action action){ var b=new ToolStripButton(label);b.Click+=delegate {try{action();}catch(Exception e){status.Text=e.Message;}};bar.Items.Add(b); }
 public static string Resolve(string value) { value=value.Trim(); Uri uri; if(value.IndexOf("://",StringComparison.Ordinal)>=0){ if(!Uri.TryCreate(value,UriKind.Absolute,out uri)||(uri.Scheme!="https"&&uri.Scheme!="http"))throw new ArgumentException("Use an HTTP or HTTPS address."); return uri.AbsoluteUri; } if(!value.Contains(" ") && value.Contains(".") && Uri.TryCreate("https://"+value,UriKind.Absolute,out uri))return uri.AbsoluteUri; return "https://www.bing.com/search?q="+Uri.EscapeDataString(value); }
 void Go(){Navigate(Resolve(address.Text));}
 void Navigate(string url){if(Current!=null && Current.CoreWebView2!=null)Current.CoreWebView2.Navigate(url);}
 void Sync(){if(Current!=null && Current.Source!=null)address.Text=Current.Source.AbsoluteUri;}
 async Task<WebView2> NewTab(string url){
  var page=new TabPage("New tab");var view=new WebView2 {Dock=DockStyle.Fill};page.Controls.Add(view);tabs.TabPages.Add(page);tabs.SelectedTab=page;
  try { await view.EnsureCoreWebView2Async(environment);
   view.CoreWebView2.Settings.AreDevToolsEnabled=false;
   view.CoreWebView2.DocumentTitleChanged+=delegate {page.Text=view.CoreWebView2.DocumentTitle.Length>28?view.CoreWebView2.DocumentTitle.Substring(0,28):view.CoreWebView2.DocumentTitle;};
   view.SourceChanged+=delegate {if(Current==view)Sync();};
   view.NavigationCompleted+=delegate(object s,CoreWebView2NavigationCompletedEventArgs e){if(Current==view)status.Text=e.IsSuccess?"Ready":("Navigation failed: "+e.WebErrorStatus);};
   view.CoreWebView2.NewWindowRequested+=async delegate(object s,CoreWebView2NewWindowRequestedEventArgs e){var deferral=e.GetDeferral();e.Handled=true;try{var child=await NewTab(null);e.NewWindow=child.CoreWebView2;}catch(Exception ex){status.Text=ex.Message;}finally{deferral.Complete();}};
   if(url!=null)view.CoreWebView2.Navigate(url);return view;
  }catch{tabs.TabPages.Remove(page);page.Dispose();throw;}
 }
 async Task SelfTest(){
  if(Resolve("youtube.com")!="https://youtube.com/" || !Resolve("physics lesson").Contains("search?q=physics%20lesson"))throw new Exception("Address normalization failed");
  bool rejected=false;try{Resolve("file:///C:/Windows");}catch(ArgumentException){rejected=true;}if(!rejected)throw new Exception("Scheme check failed");
  var first=Current;var loaded=new TaskCompletionSource<bool>(); first.NavigationCompleted+=delegate(object s,CoreWebView2NavigationCompletedEventArgs e){loaded.TrySetResult(e.IsSuccess);};
  first.NavigateToString("<html><head><title>Clarenza browser test</title></head><body><h1>Clarenza browser is ready</h1><input value='Keyboard input'><video controls></video></body></html>");
  if(await Task.WhenAny(loaded.Task,Task.Delay(20000))!=loaded.Task || !await loaded.Task)throw new Exception("Page rendering failed");
  await NewTab(null);if(tabs.TabPages.Count!=2)throw new Exception("Tab creation failed");var second=tabs.SelectedTab;tabs.TabPages.Remove(second);second.Dispose();tabs.SelectedIndex=0;
  using(var file=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"browser-test.png")))await first.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,file);
  File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"browser-test.txt"),"PASS: WebView2 initialization, local HTML rendering, URL/search normalization, unsupported scheme rejection, tab creation and disposal. External YouTube playback and sign-in not tested.");Close();
 }
}

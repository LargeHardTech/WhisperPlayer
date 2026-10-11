using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.Drivers;
using Attribute = Terminal.Gui.Drawing.Attribute;

// 离屏渲染测试：Button 在固定 Width/Height=1 下是否可见
var win = new Window { Title = "T", Width = 40, Height = 10 };
win.BorderStyle = LineStyle.Double;
var btn = new Button { Text = "_Play", X = 0, Y = 0, Width = 10, Height = 1 };
var cWin = new Scheme { Normal = new Attribute(Color.Green, Color.Black), Focus = new Attribute(Color.Green, Color.Black), HotNormal = new Attribute(Color.BrightBlue, Color.Black), HotFocus = new Attribute(Color.BrightCyan, Color.Black), Disabled = new Attribute(Color.DarkGray, Color.Black) };
win.SetScheme(cWin);
win.Add(btn);
Console.WriteLine($"Button frame before layout: {btn.Frame}");
win.Layout();
Console.WriteLine($"Button frame after layout: {btn.Frame}");
Console.WriteLine($"Button viewport: {btn.Viewport}");
Console.WriteLine($"TextFormatter text: '{btn.TextFormatter.Text}'");
Console.WriteLine($"Btn text: '{btn.Text}', title: '{btn.Title}'");
Console.WriteLine($"Visible: {btn.Visible}, Enabled: {btn.Enabled}, CanFocus: {btn.CanFocus}");

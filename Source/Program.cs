using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Drawing.Color;
using Pen = System.Drawing.Pen;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

[assembly: System.Reflection.AssemblyTitle("PariPari Anti-HDR (JXR-SDR)")]
[assembly: System.Reflection.AssemblyProduct("PariPari Anti-HDR (JXR-SDR)")]
[assembly: System.Reflection.AssemblyVersion("1.7.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.7.0.0")]

namespace JxrToSdr {
public sealed class Frame {
    public int Width, Height;
    public float[] Pixels;
}
public static class Converter {
    public static Frame Load(string path) {
        using (var stream = File.OpenRead(path)) {
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var source = decoder.Frames[0];
            if (source.Format != PixelFormats.Rgba128Float && source.Format != PixelFormats.Rgb128Float &&
                source.Format != PixelFormats.Prgba128Float)
                throw new InvalidDataException(UiText.Get("This file is not supported HDR scRGB. Format: ") + source.Format);
            int w = source.PixelWidth, h = source.PixelHeight;
            if ((long)w*h > 45000000) throw new InvalidDataException(UiText.Get("Image is too large (45 megapixel limit)."));
            var converted = new FormatConvertedBitmap(source, PixelFormats.Rgba128Float, null, 0);
            byte[] bytes = new byte[checked(w*h*16)];
            converted.CopyPixels(bytes, w*16, 0);
            var pixels = new float[w*h*4];
            Buffer.BlockCopy(bytes, 0, pixels, 0, bytes.Length);
            return new Frame { Width=w, Height=h, Pixels=pixels };
        }
    }
    static double Clean(float x) { return float.IsNaN(x) || float.IsInfinity(x) ? 0 : x; }
    static double Clamp(double x) { return Math.Max(0,Math.Min(1,x)); }
    static byte Encode(double x) {
        x=Clamp(x);
        double y=x<=0.0031308 ? 12.92*x : 1.055*Math.Pow(x,1.0/2.4)-0.055;
        return (byte)Math.Round(Clamp(y)*255);
    }
    // Luminance Reinhard shoulder followed by neutral-axis gamut compression.
    // Signed scRGB is preserved until gamut mapping; there is no early RGB clipping.
    public static byte[] Render(Frame frame, double ev, double saturation, int w, int h, double shoulder = 1) {
        byte[] result=new byte[checked(w*h*3)];
        // Windows scRGB uses 80 nits per unit. Start from a 203-nit reference white.
        double exposure=Math.Pow(2,ev)*(80.0/203.0);
        Parallel.For(0,h, y=> {
            int sy=Math.Min(frame.Height-1,(int)((y+0.5)*frame.Height/h));
            for(int x=0;x<w;x++) {
                int sx=Math.Min(frame.Width-1,(int)((x+0.5)*frame.Width/w));
                int i=(sy*frame.Width+sx)*4;
                double alpha=Clamp(Clean(frame.Pixels[i+3]));
                double r=Clean(frame.Pixels[i])*exposure*alpha;
                double g=Clean(frame.Pixels[i+1])*exposure*alpha;
                double b=Clean(frame.Pixels[i+2])*exposure*alpha;
                double lum=0.2126*r+0.7152*g+0.0722*b;
                int o=(y*w+x)*3;
                if(lum<=1e-12) continue;
                double target=AutoTone.Map(lum,shoulder), scale=target/lum;
                double dr=(r*scale-target)*saturation, dg=(g*scale-target)*saturation, db=(b*scale-target)*saturation;
                double high=Math.Max(dr,Math.Max(dg,db)), low=Math.Min(dr,Math.Min(dg,db));
                double chroma=1;
                if(high>0) chroma=Math.Min(chroma,(1-target)/high);
                if(low<0) chroma=Math.Min(chroma,-target/low);
                result[o]=Encode(target+db*chroma);
                result[o+1]=Encode(target+dg*chroma);
                result[o+2]=Encode(target+dr*chroma);
            }
        });
        return result;
    }
    public static string SaveNearSource(Frame frame,string input,double ev,double saturation,double shoulder = 1) {
        return Save(frame,input,Path.GetDirectoryName(Path.GetFullPath(input)),ev,saturation,shoulder);
    }
    public static string Save(Frame frame,string input,string directory,double ev,double saturation,double shoulder = 1) {
        Directory.CreateDirectory(directory);
        var data=Render(frame,ev,saturation,frame.Width,frame.Height,shoulder);
        var bitmap=BitmapSource.Create(frame.Width,frame.Height,96,96,PixelFormats.Bgr24,null,data,frame.Width*3);
        var meta=new BitmapMetadata("png");
        meta.SetQuery("/sRGB/RenderingIntent",(byte)0);
        meta.SetQuery("/gAMA/ImageGamma",(uint)45455);
        meta.SetQuery("/tEXt/{str=Software}","PariPari Anti-HDR (JXR-SDR) 1.7");
        var encoder=new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap,null,meta,null));
        string stem=Path.GetFileNameWithoutExtension(input)+"_SDR";
        string temporary=Path.Combine(directory,"."+Guid.NewGuid().ToString("N")+".tmp");
        try {
            using(var file=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write)) encoder.Save(file);
            for(int n=0;n<10000;n++) {
                string path=Path.Combine(directory,stem+(n==0?"":"_"+n)+".png");
                try {File.Move(temporary,path);return path;}
                catch(IOException) {if(!File.Exists(path))throw;}
            }
            throw new IOException(UiText.Get("Could not find an available file name."));
        } finally {if(File.Exists(temporary))File.Delete(temporary);}
    }
    public static Bitmap Preview(Frame frame,double ev,double saturation) {
        double size=Math.Min(1,1100.0/Math.Max(frame.Width,frame.Height));
        int w=Math.Max(1,(int)(frame.Width*size)),h=Math.Max(1,(int)(frame.Height*size));
        byte[] pixels=Render(frame,ev,saturation,w,h);
        var bmp=new Bitmap(w,h,PixelFormat.Format24bppRgb);
        var locked=bmp.LockBits(new Rectangle(0,0,w,h),ImageLockMode.WriteOnly,PixelFormat.Format24bppRgb);
        try {for(int y=0;y<h;y++)Marshal.Copy(pixels,y*w*3,IntPtr.Add(locked.Scan0,y*locked.Stride),w*3);}
        finally {bmp.UnlockBits(locked);}
        return bmp;
    }
}
public sealed class MainWindow : Form {
    readonly ListBox files=new ListBox();
    readonly ImageViewport preview=new ImageViewport();
    readonly NumericUpDown brightness=new NumericUpDown(), saturation=new NumericUpDown();
    readonly Label status=new Label(), detail=new Label();
    readonly ProgressBar progress=new ProgressBar();
    readonly TechButton save=new TechButton(),saveAll=new TechButton(),cancel=new TechButton();
    readonly List<Control> locks=new List<Control>();
    Frame current; string currentPath, lastFolder; bool busy, cancelled; int version;
    readonly System.Windows.Forms.Timer debounce=new System.Windows.Forms.Timer();
    readonly Color surface=Theme.Surface, muted=Theme.Muted, accent=Theme.Mint;
    readonly Label countLabel=new Label();
    readonly Label notice=new Label();
    readonly System.Windows.Forms.Timer noticeTimer=new System.Windows.Forms.Timer();
    readonly ColorPad colorPad=new ColorPad();
    bool syncing, rendering, previewAgain;
    readonly ToolTip tips=new ToolTip();
    readonly TechButton showResult=new TechButton();
    readonly CheckBox autoButton=new TechToggle();
    readonly Button settingsReset;
    readonly FlagButton languageButton=new FlagButton();
    readonly List<Action> translations=new List<Action>();
    readonly string languageSettingsPath;
    bool autoMode;
    double manualExposure=-3, manualSaturation=50;
    public MainWindow(string[] initial) : this(initial,UiText.SettingsPath) {}
    public MainWindow(string[] initial,string settingsPath) {
        languageSettingsPath=settingsPath;UiText.Load(settingsPath);
        Text="PariPari Anti-HDR (JXR-SDR) · 1.7";
        Font=new Font("Segoe UI",10); BackColor=Theme.Background;ForeColor=Theme.Text;
        MinimumSize=new Size(1120,780); Size=new Size(1280,900); StartPosition=FormStartPosition.CenterScreen;
        AutoScaleMode=AutoScaleMode.Dpi; KeyPreview=true;
        var root=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(20,8,20,18),ColumnCount=1,RowCount=3};
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,124));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,48));Controls.Add(root);
        var header=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,64));
        header.RowStyles.Add(new RowStyle(SizeType.Percent,100));header.Controls.Add(new BrandBanner{Dock=DockStyle.Fill,Margin=Padding.Empty},0,0);root.Controls.Add(header,0,0);
        languageButton.Size=new Size(54,38);languageButton.Anchor=AnchorStyles.Right;languageButton.Margin=new Padding(6,0,0,0);header.Controls.Add(languageButton,1,0);
        languageButton.Click+=(o,e)=>{
            UiText.Russian=!UiText.Russian;ApplyLanguage();
            if(!UiText.Save(languageSettingsPath))ShowNotice(UiText.Get("Language changed for this session. Could not save your preference."));
        };
        locks.Add(languageButton);
        var split=new SplitContainer{Dock=DockStyle.Fill,FixedPanel=FixedPanel.Panel1,Width=1180,SplitterDistance=266,BackColor=BackColor,SplitterWidth=14};
        var left=new TechLayout{Dock=DockStyle.Fill,BackColor=surface,Padding=new Padding(12),ColumnCount=1,RowCount=5};
        left.RowStyles.Add(new RowStyle(SizeType.Absolute,54));left.RowStyles.Add(new RowStyle(SizeType.Absolute,32));left.RowStyles.Add(new RowStyle(SizeType.Percent,100));left.RowStyles.Add(new RowStyle(SizeType.Absolute,250));left.RowStyles.Add(new RowStyle(SizeType.Absolute,88));
        var tools=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};
        AddIcon(tools,"file","Add files (Ctrl+O)",()=>PickFiles());
        AddIcon(tools,"folder","Open folder",()=>PickFolder());
        AddIcon(tools,"reset","Clear list",()=>{version++;files.Items.Clear();current=null;currentPath=null;ReplaceImage(null);countLabel.Text=UiText.Get("FILES · 0");RefreshEnabled();});left.Controls.Add(tools,0,0);
        countLabel.Text=UiText.Get("FILES · 0");countLabel.ForeColor=muted;countLabel.Dock=DockStyle.Fill;countLabel.Font=new Font("Segoe UI",9,FontStyle.Bold);left.Controls.Add(countLabel,0,1);
        files.Dock=DockStyle.Fill;files.IntegralHeight=false;files.BorderStyle=BorderStyle.None;files.BackColor=surface;files.ForeColor=ForeColor;
        BindTip(files,"Delete — remove the selected file from the list");
        files.DrawMode=DrawMode.OwnerDrawFixed;files.ItemHeight=62;files.DrawItem+=DrawFile;
        files.SelectedIndexChanged+=async(o,e)=>await LoadSelected();locks.Add(files);left.Controls.Add(files,0,2);split.Panel1.Controls.Add(left);
        var right=new TechPanel{Dock=DockStyle.Fill,BackColor=surface,Padding=new Padding(10)};
        preview.Dock=DockStyle.Fill;right.Controls.Add(preview);
        var expand=new IconButton("expand",UiText.Get("Actual size / fit to window"));expand.Size=new Size(38,36);expand.Anchor=AnchorStyles.Top|AnchorStyles.Right;
        BindTip(expand,"Actual size / fit to window");expand.Click+=(o,e)=>preview.ToggleFit();preview.Controls.Add(expand);
        preview.Resize+=(o,e)=>expand.Location=new Point(Math.Max(0,preview.Width-expand.Width-10),10);
        expand.Location=new Point(Math.Max(0,preview.Width-expand.Width-10),10);
        notice.Dock=DockStyle.Bottom;notice.Height=38;notice.TextAlign=ContentAlignment.MiddleCenter;notice.BackColor=Color.FromArgb(12,51,58);notice.ForeColor=accent;notice.Visible=false;preview.Controls.Add(notice);
        noticeTimer.Interval=4500;noticeTimer.Tick+=(o,e)=>{notice.Visible=false;noticeTimer.Stop();};
        split.Panel2.Controls.Add(right);root.Controls.Add(split,0,1);
        brightness.Minimum=-5;brightness.Maximum=5;brightness.DecimalPlaces=1;brightness.Increment=0.1M;brightness.Value=-3M;
        saturation.Minimum=0;saturation.Maximum=130;saturation.Value=50;saturation.Increment=1;
        colorPad.Dock=DockStyle.Fill;left.Controls.Add(colorPad,0,3);locks.Add(colorPad);
        var settings=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=2,Padding=new Padding(0,8,0,0)};
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));settings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,70));settings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,64));
        settings.RowStyles.Add(new RowStyle(SizeType.Absolute,34));settings.RowStyles.Add(new RowStyle(SizeType.Absolute,34));
        AddNumber(settings,"Brightness",brightness,0);AddNumber(settings,"Color, %",saturation,1);
        var reset=new IconButton("reset",UiText.Get("Reset adjustments")){Size=new Size(34,34),Dock=DockStyle.Fill};settingsReset=reset;BindTip(reset,"Reset adjustments");reset.Click+=(o,e)=>ResetSettings();settings.Controls.Add(reset,2,1);
        BindText(autoButton,"Auto");autoButton.Appearance=Appearance.Button;autoButton.TextAlign=ContentAlignment.MiddleCenter;autoButton.Dock=DockStyle.Fill;
        autoButton.FlatStyle=FlatStyle.Flat;autoButton.FlatAppearance.BorderSize=0;autoButton.FlatAppearance.CheckedBackColor=Color.FromArgb(47,85,73);
        autoButton.BackColor=Color.FromArgb(43,51,64);autoButton.ForeColor=accent;autoButton.Cursor=Cursors.Hand;autoButton.AccessibleName=UiText.Get("Automatic adjustments");
        BindTip(autoButton,"Adjust each image automatically. Drag the point to fine-tune. Click again for manual mode.");
        autoButton.CheckedChanged+=async(o,e)=>{if(!syncing)await SetAutoMode(autoButton.Checked);};settings.Controls.Add(autoButton,2,0);locks.Add(autoButton);locks.Add(reset);left.Controls.Add(settings,0,4);
        colorPad.ValueChanged+=(o,e)=>{if(syncing)return;syncing=true;brightness.Value=(decimal)colorPad.Exposure;saturation.Value=(decimal)colorPad.Saturation;syncing=false;RememberAuto();SchedulePreview();};
        brightness.ValueChanged+=(o,e)=>SyncPad();saturation.ValueChanged+=(o,e)=>SyncPad();
        var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};
        SetupButton(save,"Save selected",async()=>await Export(false));save.Primary=true;save.Font=new Font(Font,FontStyle.Bold);actions.Controls.Add(save);locks.Add(save);
        SetupButton(saveAll,"Save all",async()=>await Export(true));actions.Controls.Add(saveAll);locks.Add(saveAll);
        SetupButton(showResult,"Show PNG in folder",()=>ShowResult());actions.Controls.Add(showResult);locks.Add(showResult);
        SetupButton(cancel,"Stop",()=>{cancelled=true;cancel.Enabled=false;});cancel.Visible=false;actions.Controls.Add(cancel);
        progress.Size=new Size(120,26);progress.Margin=new Padding(10,7,0,0);progress.Visible=false;actions.Controls.Add(progress);root.Controls.Add(actions,0,2);
        BindTip(preview,"Scroll to zoom · drag to pan · double-click to fit");
        var wheelFilter=new PreviewWheelFilter(this,preview);Application.AddMessageFilter(wheelFilter);
        FormClosed+=(o,e)=>{Application.RemoveMessageFilter(wheelFilter);noticeTimer.Stop();var old=preview.Image;preview.SetImage(null,true);if(old!=null)old.Dispose();};
        debounce.Interval=180;debounce.Tick+=async (o,e)=>{debounce.Stop();await UpdatePreview();};
        foreach(Control drop in new Control[]{this,files,preview}){drop.AllowDrop=true;drop.DragEnter+=(o,e)=>{if(!busy&&e.Data.GetDataPresent(DataFormats.FileDrop))e.Effect=DragDropEffects.Copy;};drop.DragDrop+=(o,e)=>{if(!busy)AddPaths((string[])e.Data.GetData(DataFormats.FileDrop));};}
        KeyDown+=async(o,e)=>{if(e.KeyCode==Keys.Delete&&e.Modifiers==Keys.None&&files.ContainsFocus&&!busy){e.SuppressKeyPress=true;RemoveSelected();return;}if(e.Control&&e.KeyCode==Keys.O&&!busy){e.SuppressKeyPress=true;PickFiles();}if(e.Control&&e.KeyCode==Keys.S&&!busy){e.SuppressKeyPress=true;await Export(e.Shift);}};
        FormClosing+=(o,e)=>{if(busy){e.Cancel=true;cancelled=true;status.Text=UiText.Get("Finishing the current file. You can then close the window.");}else{version++;debounce.Stop();}};
        Shown+=(o,e)=>{if(initial.Length>0)AddPaths(initial);};ApplyLanguage();RefreshEnabled();
    }
    void BindText(Control control,string key){Action apply=()=>control.Text=UiText.Get(key);translations.Add(apply);apply();}
    void BindTip(Control control,string key){Action apply=()=>{string text=UiText.Get(key);tips.SetToolTip(control,text);control.AccessibleName=text;};translations.Add(apply);apply();}
    void ApplyLanguage(){
        SuspendLayout();foreach(var apply in translations)apply();
        string languageTip=UiText.Russian?"Русский → English":"English → Русский";
        tips.SetToolTip(languageButton,languageTip);languageButton.AccessibleName=UiText.Get("Language")+": "+languageTip;languageButton.Invalidate();
        countLabel.Text=UiText.Get("FILES · ")+files.Items.Count;
        colorPad.AccessibleName=UiText.Get("Brightness and saturation");
        preview.AccessibleName=UiText.Get("Image preview");autoButton.AccessibleName=UiText.Get("Automatic adjustments");
        notice.Visible=false;noticeTimer.Stop();status.Text="";files.Invalidate();colorPad.Invalidate();preview.Invalidate();ResumeLayout(true);
    }
    void SetNumbers(double ev,double sat){syncing=true;colorPad.SetValues(ev,sat);brightness.Value=(decimal)colorPad.Exposure;saturation.Value=(decimal)colorPad.Saturation;syncing=false;}
    void RememberAuto(){var entry=files.SelectedItem as Entry;if(autoMode&&entry!=null&&entry.Auto!=null){entry.Auto.Exposure=(double)brightness.Value;entry.Auto.Saturation=(double)saturation.Value/100;}}
    void ResetSettings(){syncing=true;autoButton.Checked=false;syncing=false;autoMode=false;manualExposure=-3;manualSaturation=50;SetNumbers(-3,50);RefreshEnabled();SchedulePreview();}
    async Task SetAutoMode(bool enabled){
        if(busy||current==null)return;
        if(!enabled){autoMode=false;SetNumbers(manualExposure,manualSaturation);SchedulePreview();return;}
        manualExposure=(double)brightness.Value;manualSaturation=(double)saturation.Value;
        var frame=current;var entry=(Entry)files.SelectedItem;int ticket=++version;
        busy=true;debounce.Stop();foreach(var c in locks)c.Enabled=false;RefreshEnabled();
        try{var settings=await Task.Run(()=>AutoTone.Analyze(frame));if(ticket!=version||IsDisposed)return;
            // A new activation starts a fresh automatic pass over the batch.
            foreach(Entry item in files.Items)item.Auto=null;
            entry.Auto=settings;autoMode=true;SetNumbers(settings.Exposure,settings.Saturation*100);
        }catch(Exception ex){autoMode=false;syncing=true;autoButton.Checked=false;syncing=false;if(!IsDisposed)ShowNotice(UiText.Get("Auto adjustment failed: ")+ex.Message);}
        finally{busy=false;if(!IsDisposed){foreach(var c in locks)c.Enabled=true;RefreshEnabled();SchedulePreview();}}
    }
    void ShowNotice(string text){notice.Text=text;notice.Visible=true;notice.BringToFront();noticeTimer.Stop();noticeTimer.Start();}
    void AddIcon(FlowLayoutPanel host,string icon,string tooltip,Action click){var button=new IconButton(icon,UiText.Get(tooltip));BindTip(button,tooltip);button.Click+=(o,e)=>click();host.Controls.Add(button);locks.Add(button);}
    void SyncPad(){if(syncing)return;syncing=true;colorPad.SetValues((double)brightness.Value,(double)saturation.Value);syncing=false;RememberAuto();SchedulePreview();}
    void AddNumber(TableLayoutPanel host,string label,NumericUpDown value,int row){
        var caption=new Label{Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=muted};BindText(caption,label);host.Controls.Add(caption,0,row);
        value.Dock=DockStyle.Fill;value.BackColor=surface;value.ForeColor=ForeColor;value.BorderStyle=BorderStyle.FixedSingle;host.Controls.Add(value,1,row);locks.Add(value);
    }
    void DrawFile(object sender,DrawItemEventArgs e){
        if(e.Index<0)return;var entry=(Entry)files.Items[e.Index];bool selected=(e.State&DrawItemState.Selected)!=0;
        using(var brush=new SolidBrush(selected?Color.FromArgb(13,49,62):surface))e.Graphics.FillRectangle(brush,e.Bounds);
        if(selected){using(var pen=new Pen(Theme.Cyan,2))e.Graphics.DrawLine(pen,e.Bounds.X+1,e.Bounds.Y+5,e.Bounds.X+1,e.Bounds.Bottom-5);
            using(var pen=new Pen(Color.FromArgb(39,104,121)))e.Graphics.DrawLine(pen,e.Bounds.X+8,e.Bounds.Bottom-2,e.Bounds.Right-8,e.Bounds.Bottom-2);}
        var r=new Rectangle(e.Bounds.X+10,e.Bounds.Y+8,e.Bounds.Width-20,24);
        TextRenderer.DrawText(e.Graphics,Path.GetFileNameWithoutExtension(entry.Path),Font,r,ForeColor,TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);
        r.Y+=26;r.Height=20;using(var small=new Font("Segoe UI",8))TextRenderer.DrawText(e.Graphics,entry.SavedPath==null?UiText.Get("JXR  ·  ready to convert"):UiText.Get("PNG saved"),small,r,entry.SavedPath==null?muted:accent,TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);
    }
    void RefreshEnabled(){settingsReset.Enabled=autoButton.Enabled=colorPad.Enabled=brightness.Enabled=saturation.Enabled=!busy&&current!=null;save.Enabled=!busy&&current!=null;saveAll.Enabled=!busy&&files.Items.Count>0;showResult.Enabled=!busy&&(lastFolder!=null||((files.SelectedItem as Entry)!=null&&(files.SelectedItem as Entry).SavedPath!=null));}
    void ShowResult(){var entry=files.SelectedItem as Entry;string file=entry!=null?entry.SavedPath:null;try{if(file!=null&&File.Exists(file))System.Diagnostics.Process.Start("explorer.exe","/select,\""+file+"\"");else if(lastFolder!=null)System.Diagnostics.Process.Start("explorer.exe","\""+lastFolder+"\"");}catch(Exception ex){status.Text=ex.Message;}}
    void SetupButton(Button b,string text,Action click){BindText(b,text);b.AutoSize=true;b.Height=40;b.Padding=new Padding(13,7,13,7);b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderSize=0;b.BackColor=Theme.Background;b.ForeColor=ForeColor;b.Cursor=Cursors.Hand;b.Margin=new Padding(0,0,9,0);b.Click+=(o,e)=>click();}
    void AddButton(FlowLayoutPanel p,string title,Action action){var b=new TechButton();SetupButton(b,title,action);p.Controls.Add(b);locks.Add(b);}
    void PickFiles(){using(var d=new OpenFileDialog{Filter=UiText.Get("HDR screenshots (*.jxr;*.wdp;*.hdp)|*.jxr;*.wdp;*.hdp"),Multiselect=true,Title=UiText.Get("Select HDR screenshots")})if(d.ShowDialog()==DialogResult.OK)AddPaths(d.FileNames);}
    void PickFolder(){
        try{string path=ModernFolderPicker.Pick(this.Handle);if(path!=null)AddPaths(new[]{path});}
        catch(Exception ex){MessageBox.Show(this,ex.Message,UiText.Get("Could not open the folder picker"));}
    }
    void RemoveSelected(){
        if(busy||files.SelectedIndex<0)return;
        int index=files.SelectedIndex;version++;debounce.Stop();
        files.Items.RemoveAt(index);
        countLabel.Text=UiText.Get("FILES · ")+files.Items.Count;
        if(files.Items.Count>0){files.SelectedIndex=Math.Min(index,files.Items.Count-1);}
        else{current=null;currentPath=null;ReplaceImage(null);detail.Text=UiText.Get("SDR preview");status.Text=UiText.Get("The list is empty. Add files or drop them here");}
        RefreshEnabled();
    }
    static bool Supported(string p){return new[]{".jxr",".wdp",".hdp"}.Contains(Path.GetExtension(p).ToLowerInvariant());}
    void AddPaths(string[] paths){
        int added=0;
        try{foreach(string path in paths){var set=Directory.Exists(path)?Directory.GetFiles(path).OrderByDescending(File.GetLastWriteTime).ToArray():new[]{path};foreach(string p in set){if(!Supported(p))continue;string full=Path.GetFullPath(p);if(!files.Items.Cast<Entry>().Any(x=>string.Equals(x.Path,full,StringComparison.OrdinalIgnoreCase))){files.Items.Add(new Entry(full));added++;}}}}
        catch(Exception ex){MessageBox.Show(this,ex.Message,UiText.Get("Could not open the folder"));}
        status.Text=added>0?UiText.Get("Files in list: ")+files.Items.Count:UiText.Get("No new JXR files found");
        countLabel.Text=UiText.Get("FILES · ")+files.Items.Count;RefreshEnabled();
        if(files.SelectedIndex<0 && files.Items.Count>0)files.SelectedIndex=0;
    }
    async Task LoadSelected(){
        int ticket=++version;var entry=files.SelectedItem as Entry;current=null;currentPath=null;ReplaceImage(null);RefreshEnabled();
        if(entry==null)return;notice.Visible=false;status.Text=UiText.Get("Loading HDR…");detail.Text=entry.ToString();
        try{bool automatic=autoMode;ToneSettings settings=entry.Auto;
            var frame=await Task.Run(()=>{var f=Converter.Load(entry.Path);if(automatic&&settings==null)settings=AutoTone.Analyze(f);return f;});
            if(ticket!=version||IsDisposed)return;current=frame;currentPath=entry.Path;
            if(automatic){entry.Auto=settings;SetNumbers(settings.Exposure,settings.Saturation*100);}
            RefreshEnabled();detail.Text=entry+"  ·  "+frame.Width+" × "+frame.Height;await UpdatePreview();}
        catch(Exception ex){if(ticket==version&&!IsDisposed)ShowNotice(UiText.Get("Error: ")+ex.Message);}
    }
    void SchedulePreview(){if(!debounce.Enabled)debounce.Start();}
    async Task UpdatePreview(){
        if(current==null||busy)return;if(rendering){previewAgain=true;return;}rendering=true;previewAgain=false;int ticket=++version;var frame=current;double ev=(double)brightness.Value,sat=(double)saturation.Value/100;var entry=files.SelectedItem as Entry;double shoulder=autoMode&&entry!=null&&entry.Auto!=null?entry.Auto.Shoulder:1;
        status.Text=UiText.Get("Rendering SDR preview…");
        try{var bmp=await Task.Run(()=>FullPreview(frame,ev,sat,shoulder));if(ticket!=version||IsDisposed){bmp.Dispose();return;}
            ReplaceImage(bmp);
            status.Text=UiText.Get("SDR preview · saves at full resolution");
        }catch(Exception ex){if(ticket==version&&!IsDisposed)ShowNotice(UiText.Get("Error: ")+ex.Message);}
        finally{rendering=false;if(previewAgain&&!IsDisposed)SchedulePreview();}
    }
    static Bitmap FullPreview(Frame f,double ev,double sat,double shoulder){
        byte[] pixels=Converter.Render(f,ev,sat,f.Width,f.Height,shoulder);var bmp=new Bitmap(f.Width,f.Height,PixelFormat.Format24bppRgb);
        var data=bmp.LockBits(new Rectangle(0,0,f.Width,f.Height),ImageLockMode.WriteOnly,PixelFormat.Format24bppRgb);
        try{for(int y=0;y<f.Height;y++)Marshal.Copy(pixels,y*f.Width*3,IntPtr.Add(data.Scan0,y*data.Stride),f.Width*3);}finally{bmp.UnlockBits(data);}return bmp;
    }
    void ReplaceImage(Image img){var old=preview.Image;preview.SetImage(img,old==null||img==null);if(old!=null)old.Dispose();}
    async Task Export(bool all){
        if(busy)return;if(!all && current==null){MessageBox.Show(this,UiText.Get("Select and load a JXR file first."));return;}
        var entries=all?files.Items.Cast<Entry>().ToArray():new[]{(Entry)files.SelectedItem};if(entries.Length==0)return;
        double ev=(double)brightness.Value,sat=(double)saturation.Value/100;bool automatic=autoMode;
        busy=true;cancelled=false;version++;debounce.Stop();foreach(var c in locks)c.Enabled=false;cancel.Visible=true;cancel.Enabled=true;progress.Visible=true;
        int saved=0;var errors=new List<string>();progress.Value=0;progress.Maximum=entries.Length;
        try{foreach(var item in entries){if(cancelled)break;status.Text=UiText.Get("Saving ")+(progress.Value+1)+" / "+entries.Length+": "+item;
            try{ToneSettings settings=item.Auto==null?null:item.Auto.Copy();string savedPath=await Task.Run(()=>{
                var frame=item.Path==currentPath&&current!=null?current:Converter.Load(item.Path);
                if(automatic){if(settings==null)settings=AutoTone.Analyze(frame);return Converter.SaveNearSource(frame,item.Path,settings.Exposure,settings.Saturation,settings.Shoulder);}
                return Converter.SaveNearSource(frame,item.Path,ev,sat);
            });if(automatic)item.Auto=settings;item.SavedPath=savedPath;lastFolder=Path.GetDirectoryName(savedPath);files.Invalidate();saved++;}
            catch(Exception ex){errors.Add(item+": "+ex.Message);}progress.Value++;
        }}finally{busy=false;foreach(var c in locks)c.Enabled=true;cancel.Enabled=false;cancel.Visible=false;progress.Visible=false;RefreshEnabled();}
        status.Text=(cancelled?UiText.Get("Stopped. "):UiText.Get("Done. "))+UiText.Get("Saved next to the originals: ")+saved+(errors.Count>0?UiText.Get(" · Errors: ")+errors.Count:"");
        if(current==null)await LoadSelected();else SchedulePreview();
        ShowNotice((cancelled?UiText.Get("Stopped · "):"")+UiText.Get("Saved: ")+saved+(errors.Count>0?UiText.Get(" · Errors: ")+errors.Count:""));
        if(errors.Count>0)MessageBox.Show(this,string.Join(Environment.NewLine,errors.Take(10)),UiText.Get("Some files could not be saved"));
    }
    sealed class Entry {public string Path, SavedPath;public ToneSettings Auto;public Entry(string p){Path=p;}public override string ToString(){return System.IO.Path.GetFileName(Path);}}
}
static class ModernFolderPicker {
    public static string Pick(IntPtr owner){
        var dialog=(IFileDialog)new FileOpenDialog();IShellItem item=null;
        try{
            uint options;dialog.GetOptions(out options);
            dialog.SetOptions(options|0x20u|0x40u|0x800u|0x8u); // PICKFOLDERS, FORCEFILESYSTEM, PATHMUSTEXIST, NOCHANGEDIR
            dialog.SetTitle(UiText.Get("Select a folder with JXR screenshots"));
            dialog.SetOkButtonLabel(UiText.Get("Open folder"));
            int result=dialog.Show(owner);
            if(result==unchecked((int)0x800704C7))return null;
            Marshal.ThrowExceptionForHR(result);
            dialog.GetResult(out item);IntPtr name;
            item.GetDisplayName(0x80058000u,out name); // SIGDN_FILESYSPATH
            try{return Marshal.PtrToStringUni(name);}finally{Marshal.FreeCoTaskMem(name);}
        }finally{if(item!=null)Marshal.FinalReleaseComObject(item);Marshal.FinalReleaseComObject(dialog);}
    }
    [ComImport,Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")] class FileOpenDialog {}
    [ComImport,Guid("42F85136-DB7E-439C-85F1-E4075D135FC8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IFileDialog {
        [PreserveSig]int Show(IntPtr owner);
        void SetFileTypes(uint count,IntPtr specs);
        void SetFileTypeIndex(uint index);
        void GetFileTypeIndex(out uint index);
        void Advise(IntPtr events,out uint cookie);
        void Unadvise(uint cookie);
        void SetOptions(uint options);
        void GetOptions(out uint options);
        void SetDefaultFolder(IShellItem folder);
        void SetFolder(IShellItem folder);
        void GetFolder(out IShellItem folder);
        void GetCurrentSelection(out IShellItem item);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)]string name);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)]out string name);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)]string title);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)]string label);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)]string label);
        void GetResult(out IShellItem item);
        void AddPlace(IShellItem item,int alignment);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)]string extension);
        void Close(int result);
        void SetClientGuid(ref Guid guid);
        void ClearClientData();
        void SetFilter(IntPtr filter);
    }
    [ComImport,Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItem {
        void BindToHandler(IntPtr context,ref Guid handler,ref Guid iid,out IntPtr result);
        void GetParent(out IShellItem parent);
        void GetDisplayName(uint format,out IntPtr name);
        void GetAttributes(uint mask,out uint attributes);
        void Compare(IShellItem other,uint hint,out int order);
    }
}
static class Program {
    [STAThread] static int Main(string[] args){
        try{
            if(args.Length>0 && args[0]=="--convert-auto"){
                if(args.Length<3)throw new ArgumentException("--convert-auto INPUT OUTPUT_FOLDER");
                var frame=Converter.Load(args[1]);var settings=AutoTone.Analyze(frame);
                Console.WriteLine(Converter.Save(frame,args[1],args[2],settings.Exposure,settings.Saturation,settings.Shoulder));return 0;
            }
            if(args.Length>0 && args[0]=="--convert-near"){
                if(args.Length<2)throw new ArgumentException("--convert-near INPUT");
                Console.WriteLine(Converter.SaveNearSource(Converter.Load(args[1]),args[1],0,1));return 0;
            }
            if(args.Length>0 && args[0]=="--convert"){
                if(args.Length<3)throw new ArgumentException("--convert INPUT OUTPUT_FOLDER [EV] [SATURATION_PERCENT]");
                double ev=args.Length>3?double.Parse(args[3],System.Globalization.CultureInfo.InvariantCulture):0;
                double sat=args.Length>4?double.Parse(args[4],System.Globalization.CultureInfo.InvariantCulture)/100:1;
                string output=Converter.Save(Converter.Load(args[1]),args[1],args[2],ev,sat);
                Console.WriteLine(output);return 0;
            }
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new MainWindow(args));return 0;
        }catch(Exception ex){if(args.Length>0&&args[0].StartsWith("--convert")){Console.Error.WriteLine(ex.ToString());return 1;}MessageBox.Show(ex.Message,"PariPari Anti-HDR (JXR-SDR)");return 1;}
    }
}
}

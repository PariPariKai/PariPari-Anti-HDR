using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;

namespace JxrToSdr {
static class Theme {
    public static readonly Color Background=Color.FromArgb(7,16,23), Surface=Color.FromArgb(10,25,34),
        Cyan=Color.FromArgb(48,210,245), Mint=Color.FromArgb(57,237,194),
        Text=Color.FromArgb(221,239,247), Muted=Color.FromArgb(135,165,181);
    public static GraphicsPath Shape(RectangleF r,float cut){
        var p=new GraphicsPath();p.AddPolygon(new[]{new PointF(r.Left+cut,r.Top),new PointF(r.Right,r.Top),new PointF(r.Right,r.Bottom-cut),new PointF(r.Right-cut,r.Bottom),new PointF(r.Left,r.Bottom),new PointF(r.Left,r.Top+cut)});return p;
    }
    public static void Button(Graphics g,Rectangle bounds,bool enabled,bool hover,bool pressed,bool primary,bool focused){
        g.Clear(Background);g.SmoothingMode=SmoothingMode.AntiAlias;
        var r=new RectangleF(1,1,Math.Max(1,bounds.Width-3),Math.Max(1,bounds.Height-3));
        using(var path=Shape(r,6)){
            Color a=primary&&enabled?Color.FromArgb(30,167,203):Color.FromArgb(15,37,49);
            Color b=primary&&enabled?Mint:Color.FromArgb(9,23,32);
            if(pressed){a=Color.FromArgb(22,98,111);b=Color.FromArgb(15,57,65);}
            using(var brush=new LinearGradientBrush(r,a,b,LinearGradientMode.Horizontal))g.FillPath(brush,path);
            if(hover&&enabled)using(var glow=new Pen(Color.FromArgb(40,Cyan),5))g.DrawPath(glow,path);
            using(var pen=new Pen(!enabled?Color.FromArgb(29,53,64):hover||focused?Mint:primary?Cyan:Color.FromArgb(42,101,119),1))g.DrawPath(pen,path);
        }
        if(enabled){using(var line=new Pen(primary?Color.FromArgb(205,255,246):Color.FromArgb(91,Cyan),1))g.DrawLine(line,9,2,Math.Min(bounds.Width-10,32),2);}
        if(focused)ControlPaint.DrawFocusRectangle(g,new Rectangle(5,5,bounds.Width-11,bounds.Height-11),Cyan,Surface);
    }
    public static void Panel(Graphics g,Rectangle bounds){
        if(bounds.Width<2||bounds.Height<2)return;
        using(var brush=new LinearGradientBrush(bounds,Surface,Color.FromArgb(7,19,28),LinearGradientMode.Vertical))g.FillRectangle(brush,bounds);
        var r=new RectangleF(1,1,bounds.Width-3,bounds.Height-3);
        using(var path=Shape(r,10))using(var pen=new Pen(Color.FromArgb(27,68,84)))g.DrawPath(pen,path);
        using(var pen=new Pen(Cyan,1)){g.DrawLine(pen,12,1,65,1);g.DrawLine(pen,1,12,1,35);}
        using(var pen=new Pen(Color.FromArgb(110,Mint),1))g.DrawLine(pen,bounds.Width-65,bounds.Height-2,bounds.Width-13,bounds.Height-2);
    }
}
public class TechButton : Button {
    protected bool Hover,Pressed;
    public bool Primary {get;set;}
    public TechButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;ForeColor=Theme.Text;BackColor=Theme.Background;}
    protected override void OnMouseEnter(EventArgs e){Hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){Hover=false;Pressed=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnMouseDown(MouseEventArgs e){if(e.Button==MouseButtons.Left)Pressed=true;Invalidate();base.OnMouseDown(e);}
    protected override void OnMouseUp(MouseEventArgs e){Pressed=false;Invalidate();base.OnMouseUp(e);}
    protected override void OnEnabledChanged(EventArgs e){Invalidate();base.OnEnabledChanged(e);}
    protected override void OnPaint(PaintEventArgs e){Theme.Button(e.Graphics,ClientRectangle,Enabled,Hover,Pressed,Primary,Focused);DrawContent(e.Graphics);}
    protected virtual void DrawContent(Graphics g){TextRenderer.DrawText(g,Text,Font,ClientRectangle,!Enabled?Color.FromArgb(86,119,135):Primary?Color.FromArgb(5,27,34):Theme.Text,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);}
}
public sealed class TechToggle : CheckBox {
    bool hover;
    public TechToggle(){SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);Appearance=Appearance.Button;Cursor=Cursors.Hand;}
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnCheckedChanged(EventArgs e){Invalidate();base.OnCheckedChanged(e);}
    protected override void OnPaint(PaintEventArgs e){Theme.Button(e.Graphics,ClientRectangle,Enabled,hover,false,Checked,Focused);TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,!Enabled?Theme.Muted:Checked?Theme.Background:Theme.Cyan,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);}
}
public sealed class TechPanel : Panel {
    public TechPanel(){DoubleBuffered=true;ResizeRedraw=true;BackColor=Theme.Surface;}
    protected override void OnPaintBackground(PaintEventArgs e){Theme.Panel(e.Graphics,ClientRectangle);}
}
public sealed class TechLayout : TableLayoutPanel {
    public TechLayout(){DoubleBuffered=true;ResizeRedraw=true;BackColor=Theme.Surface;}
    protected override void OnPaintBackground(PaintEventArgs e){Theme.Panel(e.Graphics,ClientRectangle);}
}
public sealed class BrandBanner : Control {
    readonly Image banner;
    public BrandBanner(){SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.ResizeRedraw,true);BackColor=Theme.Background;AccessibleName="PariPari Anti-HDR";
        using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("PariPari.Banner")){
            if(stream!=null)using(var source=Image.FromStream(stream))banner=new Bitmap(source);
        }
    }
    protected override void OnPaint(PaintEventArgs e){
        e.Graphics.Clear(BackColor);if(banner==null)return;
        // Show the central banner strip; the supplied asset is embedded unchanged.
        var src=new RectangleF(0,banner.Height*.20f,banner.Width,banner.Height*.60f);
        float scale=Math.Min(Width/src.Width,Height/src.Height),w=src.Width*scale,h=src.Height*scale;
        e.Graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
        e.Graphics.DrawImage(banner,new RectangleF((Width-w)/2,(Height-h)/2,w,h),src,GraphicsUnit.Pixel);
    }
    protected override void Dispose(bool disposing){if(disposing&&banner!=null)banner.Dispose();base.Dispose(disposing);}
}
public sealed class FlagButton : TechButton {
    protected override void DrawContent(Graphics g){
        var r=new Rectangle((Width-30)/2,(Height-20)/2,30,20);var state=g.Save();g.SetClip(r);
        if(UiText.Russian){using(var white=new SolidBrush(Color.White))g.FillRectangle(white,r);using(var blue=new SolidBrush(Color.FromArgb(28,76,182)))g.FillRectangle(blue,r.X,r.Y+7,30,7);using(var red=new SolidBrush(Color.FromArgb(220,49,63)))g.FillRectangle(red,r.X,r.Y+14,30,6);}
        else{
            using(var blue=new SolidBrush(Color.FromArgb(25,49,108)))g.FillRectangle(blue,r);
            using(var white=new Pen(Color.White,5)){g.DrawLine(white,r.Left,r.Top,r.Right,r.Bottom);g.DrawLine(white,r.Left,r.Bottom,r.Right,r.Top);}
            using(var red=new Pen(Color.FromArgb(207,39,59),2)){g.DrawLine(red,r.Left,r.Top,r.Right,r.Bottom);g.DrawLine(red,r.Left,r.Bottom,r.Right,r.Top);}
            using(var white=new SolidBrush(Color.White)){g.FillRectangle(white,r.X+11,r.Y,8,20);g.FillRectangle(white,r.X,r.Y+6,30,8);}
            using(var red=new SolidBrush(Color.FromArgb(207,39,59))){g.FillRectangle(red,r.X+13,r.Y,4,20);g.FillRectangle(red,r.X,r.Y+8,30,4);}
        }
        g.Restore(state);using(var border=new Pen(Color.FromArgb(125,Theme.Text)))g.DrawRectangle(border,r);
    }
}
}

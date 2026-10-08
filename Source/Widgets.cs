using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace JxrToSdr {
public sealed class IconButton : Button {
    public string Kind;
    bool hover;
    public IconButton(string kind,string label){
        Kind=kind;AccessibleName=label;AccessibleRole=AccessibleRole.PushButton;
        Size=new Size(42,40);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;
        BackColor=Theme.Background;ForeColor=Theme.Cyan;
        Cursor=Cursors.Hand;Margin=new Padding(0,0,8,0);
    }
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnPaint(PaintEventArgs e){
        var g=e.Graphics;Theme.Button(g,ClientRectangle,Enabled,hover,false,false,Focused);g.SmoothingMode=SmoothingMode.AntiAlias;
        float unit=Math.Min(Width,Height)/40f;
        var state=g.Save();g.TranslateTransform(Width/2f,Height/2f);g.ScaleTransform(unit,unit);
        using(var pen=new Pen(Enabled?ForeColor:Color.FromArgb(96,110,120),1.7f)){
            pen.StartCap=LineCap.Round;pen.EndCap=LineCap.Round;pen.LineJoin=LineJoin.Round;
            if(Kind=="file"){
                g.DrawLines(pen,new[]{new PointF(1,-11),new PointF(-9,-11),new PointF(-9,10),new PointF(0,10)});
                g.DrawLines(pen,new[]{new PointF(1,-11),new PointF(8,-4),new PointF(8,0)});
                g.DrawLines(pen,new[]{new PointF(1,-11),new PointF(1,-4),new PointF(8,-4)});
                g.DrawLine(pen,7,3,7,13);g.DrawLine(pen,2,8,12,8);
            }else if(Kind=="folder"){
                g.DrawLines(pen,new[]{new PointF(11,-1),new PointF(11,-6),new PointF(-1,-6),new PointF(-4,-10),new PointF(-11,-10),new PointF(-11,9),new PointF(0,9)});
                g.DrawLine(pen,-11,-2,4,-2);g.DrawLine(pen,7,2,7,12);g.DrawLine(pen,2,7,12,7);
            }else if(Kind=="reset"){
                g.DrawArc(pen,-10,-10,20,20,-55,285);
                double a=230*Math.PI/180;float x=10*(float)Math.Cos(a),y=10*(float)Math.Sin(a);
                float tx=-(float)Math.Sin(a),ty=(float)Math.Cos(a);
                g.DrawLines(pen,new[]{new PointF(x-tx*5-ty*3,y-ty*5+tx*3),new PointF(x,y),new PointF(x-tx*5+ty*3,y-ty*5-tx*3)});
            }else if(Kind=="expand"){
                foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1}){
                    g.DrawLine(pen,x*3,y*3,x*11,y*11);
                    g.DrawLines(pen,new[]{new PointF(x*5,y*11),new PointF(x*11,y*11),new PointF(x*11,y*5)});
                }
            }
        }
        g.Restore(state);if(Focused)ControlPaint.DrawFocusRectangle(g,new Rectangle(3,3,Width-6,Height-6));
    }
}
public sealed class ColorPad : Control {
    double exposure=-3,saturation=50;
    bool dragging;
    public event EventHandler ValueChanged;
    public double Exposure {get{return exposure;}}
    public double Saturation {get{return saturation;}}
    public RectangleF Plot {get{float side=Math.Max(20,Math.Min(Width-44,Height-54));return new RectangleF((Width-side)/2,25,side,side);}}
    public ColorPad(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Selectable|ControlStyles.ResizeRedraw,true);TabStop=true;Cursor=Cursors.Cross;AccessibleName=UiText.Get("Brightness and saturation");AccessibleRole=AccessibleRole.Pane;}
    public void SetValues(double ev,double sat){
        ev=Math.Round(Math.Max(-5,Math.Min(5,ev)),1);sat=Math.Round(Math.Max(0,Math.Min(130,sat)));
        if(ev==exposure&&sat==saturation)return;exposure=ev;saturation=sat;Invalidate();if(ValueChanged!=null)ValueChanged(this,EventArgs.Empty);
    }
    public void SetFromPoint(Point point){var r=Plot;SetValues(5-10*(point.Y-r.Top)/r.Height,130*(point.X-r.Left)/r.Width);}
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left&&Plot.Contains(e.Location)){Focus();dragging=true;Capture=true;SetFromPoint(e.Location);}}
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging)SetFromPoint(e.Location);}
    protected override void OnMouseUp(MouseEventArgs e){dragging=false;Capture=false;base.OnMouseUp(e);}
    protected override void OnMouseCaptureChanged(EventArgs e){if(!Capture)dragging=false;base.OnMouseCaptureChanged(e);}
    protected override bool IsInputKey(Keys key){return key==Keys.Left||key==Keys.Right||key==Keys.Up||key==Keys.Down||base.IsInputKey(key);}
    protected override void OnKeyDown(KeyEventArgs e){
        if(e.KeyCode==Keys.Left){SetValues(exposure,saturation-1);e.Handled=true;}if(e.KeyCode==Keys.Right){SetValues(exposure,saturation+1);e.Handled=true;}
        if(e.KeyCode==Keys.Up){SetValues(exposure+.1,saturation);e.Handled=true;}if(e.KeyCode==Keys.Down){SetValues(exposure-.1,saturation);e.Handled=true;}base.OnKeyDown(e);
    }
    protected override void OnPaint(PaintEventArgs e){
        base.OnPaint(e);var g=e.Graphics;var r=Plot;g.SmoothingMode=SmoothingMode.AntiAlias;
        using(var bg=new LinearGradientBrush(r,Color.FromArgb(15,47,70),Color.FromArgb(19,112,108),LinearGradientMode.Horizontal))g.FillRectangle(bg,r);
        using(var shade=new LinearGradientBrush(r,Color.FromArgb(32,106,220,250),Color.FromArgb(220,0,5,12),LinearGradientMode.Vertical))g.FillRectangle(shade,r);
        using(var grid=new Pen(Color.FromArgb(36,74,211,234),1))for(int n=1;n<10;n++){float t=n/10f;g.DrawLine(grid,r.Left+t*r.Width,r.Top,r.Left+t*r.Width,r.Bottom);g.DrawLine(grid,r.Left,r.Top+t*r.Height,r.Right,r.Top+t*r.Height);}
        using(var axis=new Pen(Color.FromArgb(115,48,210,245),1)){g.DrawLine(axis,r.Left,r.Top+r.Height/2,r.Right,r.Top+r.Height/2);g.DrawLine(axis,r.Left+r.Width/2,r.Top,r.Left+r.Width/2,r.Bottom);g.DrawRectangle(axis,r.X,r.Y,r.Width,r.Height);}
        var muted=Theme.Muted;using(var f=new Font("Segoe UI",8)){
            TextRenderer.DrawText(g,UiText.Get("Brightness ↑"),f,new Rectangle(0,2,Width,20),muted,TextFormatFlags.HorizontalCenter);
            TextRenderer.DrawText(g,UiText.Get("Saturation →"),f,new Rectangle(0,(int)r.Bottom+9,Width,20),muted,TextFormatFlags.HorizontalCenter);
            TextRenderer.DrawText(g,"+5",f,new Point(0,(int)r.Top-5),muted);TextRenderer.DrawText(g,"−5",f,new Point(0,(int)r.Bottom-12),muted);
            TextRenderer.DrawText(g,"0",f,new Point((int)r.Left-2,(int)r.Bottom+1),muted);TextRenderer.DrawText(g,"130",f,new Point((int)r.Right-22,(int)r.Bottom+1),muted);
        }
        float x=r.Left+(float)(saturation/130)*r.Width,y=r.Top+(float)((5-exposure)/10)*r.Height;
        using(var cross=new Pen(Color.FromArgb(140,Theme.Cyan),1)){g.DrawLine(cross,r.Left,y,r.Right,y);g.DrawLine(cross,x,r.Top,x,r.Bottom);}
        if(Enabled)using(var glow=new Pen(Color.FromArgb(45,Theme.Cyan),6))g.DrawEllipse(glow,x-6,y-6,12,12);
        using(var bg=new SolidBrush(Theme.Background))g.FillEllipse(bg,x-6,y-6,12,12);
        using(var ring=new Pen(Enabled?Theme.Mint:muted,2))g.DrawEllipse(ring,x-6,y-6,12,12);
        if(Focused)ControlPaint.DrawFocusRectangle(g,new Rectangle((int)r.Left-3,(int)r.Top-3,(int)r.Width+6,(int)r.Height+6));
    }
}
public sealed class ImageViewport : Control {
    Image image;
    double zoom=1,panX,panY;
    bool fitted=true,dragging;
    Point dragStart;double dragX,dragY;
    public Image Image {get{return image;}}
    public double Zoom {get{return zoom;}}
    public PointF Pan {get{return new PointF((float)panX,(float)panY);}}
    public bool Fitted {get{return fitted;}}
    public ImageViewport(){
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Selectable|ControlStyles.ResizeRedraw,true);
        BackColor=Color.FromArgb(9,13,17);ForeColor=Theme.Muted;TabStop=true;AccessibleName=UiText.Get("Image preview");
    }
    double FitScale(){return image==null?1:Math.Min((double)Math.Max(1,ClientSize.Width)/image.Width,(double)Math.Max(1,ClientSize.Height)/image.Height);}
    public void SetImage(Image next,bool reset){image=next;if(reset||next==null)Fit();else{ClampPan();Invalidate();}}
    public void Fit(){fitted=true;zoom=FitScale();panX=panY=0;Invalidate();}
    public void ToggleFit(){if(image==null)return;if(fitted){zoom=1;fitted=false;panX=panY=0;ClampPan();Invalidate();}else Fit();}
    public PointF ImageAt(PointF point){return new PointF((float)((point.X-(ClientSize.Width-image.Width*zoom)/2-panX)/zoom),(float)((point.Y-(ClientSize.Height-image.Height*zoom)/2-panY)/zoom));}
    public void ZoomAt(Point anchor,int delta){
        if(image==null||delta==0)return;var before=ImageAt(anchor);
        zoom=Math.Max(Math.Max(.005,FitScale()*.2),Math.Min(16,zoom*Math.Pow(1.2,delta/120.0)));fitted=false;
        panX=anchor.X-(ClientSize.Width-image.Width*zoom)/2-before.X*zoom;
        panY=anchor.Y-(ClientSize.Height-image.Height*zoom)/2-before.Y*zoom;ClampPan();Invalidate();
    }
    void ClampPan(){if(image==null)return;double x=Math.Max(0,(image.Width*zoom-ClientSize.Width)/2),y=Math.Max(0,(image.Height*zoom-ClientSize.Height)/2);panX=Math.Max(-x,Math.Min(x,panX));panY=Math.Max(-y,Math.Min(y,panY));}
    protected override void OnResize(EventArgs e){base.OnResize(e);if(fitted)Fit();else{ClampPan();Invalidate();}}
    protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);}
    protected override void OnMouseWheel(MouseEventArgs e){ZoomAt(e.Location,e.Delta);base.OnMouseWheel(e);}
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left&&image!=null){Focus();dragging=true;Capture=true;dragStart=e.Location;dragX=panX;dragY=panY;Cursor=Cursors.Hand;}}
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging){panX=dragX+e.X-dragStart.X;panY=dragY+e.Y-dragStart.Y;ClampPan();Invalidate();}}
    protected override void OnMouseUp(MouseEventArgs e){dragging=false;Capture=false;Cursor=Cursors.Default;base.OnMouseUp(e);}
    protected override void OnMouseCaptureChanged(EventArgs e){if(!Capture){dragging=false;Cursor=Cursors.Default;}base.OnMouseCaptureChanged(e);}
    protected override void OnMouseDoubleClick(MouseEventArgs e){if(e.Button==MouseButtons.Left)Fit();base.OnMouseDoubleClick(e);}
    protected override void OnPaint(PaintEventArgs e){
        base.OnPaint(e);
        if(image==null){using(var f=new Font("Segoe UI",16))TextRenderer.DrawText(e.Graphics,UiText.Get("Drop an HDR screenshot / image here"),f,ClientRectangle,ForeColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.WordBreak);return;}
        e.Graphics.InterpolationMode=zoom>=1?InterpolationMode.NearestNeighbor:InterpolationMode.HighQualityBicubic;
        e.Graphics.PixelOffsetMode=PixelOffsetMode.Half;
        var destination=new RectangleF((float)((ClientSize.Width-image.Width*zoom)/2+panX),(float)((ClientSize.Height-image.Height*zoom)/2+panY),(float)(image.Width*zoom),(float)(image.Height*zoom));
        using(var attr=new System.Drawing.Imaging.ImageAttributes()){
            attr.SetWrapMode(WrapMode.TileFlipXY);
            e.Graphics.DrawImage(image,Rectangle.Round(destination),0,0,image.Width,image.Height,GraphicsUnit.Pixel,attr);
        }
    }
}
sealed class PreviewWheelFilter : IMessageFilter {
    readonly Form owner;readonly ImageViewport viewport;
    public PreviewWheelFilter(Form owner,ImageViewport viewport){this.owner=owner;this.viewport=viewport;}
    public bool PreFilterMessage(ref Message message){
        if(message.Msg!=0x20A||!owner.Enabled||Form.ActiveForm!=owner||!viewport.Visible||!viewport.IsHandleCreated)return false;
        Point point=viewport.PointToClient(Cursor.Position);
        if(!viewport.ClientRectangle.Contains(point))return false;
        int delta=(short)((message.WParam.ToInt64()>>16)&0xffff);viewport.ZoomAt(point,delta);return true;
    }
}
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

internal sealed class ScreenshotOverlay : Form
{
    private enum Tool { Freehand, Line, Rectangle }
    private enum Glyph { Freehand, Line, Rectangle, Undo, Cancel, Confirm }
    private sealed class IconButton : Button
    {
        internal Glyph Kind;
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float scale = Math.Min((Width - 14) / 24f, (Height - 14) / 24f);
            e.Graphics.TranslateTransform((Width - 24 * scale) / 2f, (Height - 24 * scale) / 2f);
            e.Graphics.ScaleTransform(scale, scale);
            using (Pen pen = new Pen(ForeColor, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round,
                LineJoin = LineJoin.Round })
            {
                if (Kind == Glyph.Freehand)
                {
                    e.Graphics.DrawPolygon(pen, new Point[] { new Point(19, 3), new Point(21, 5),
                        new Point(10, 16), new Point(6, 17), new Point(7, 13) });
                    e.Graphics.DrawLine(pen, 4, 21, 18, 21);
                }
                else if (Kind == Glyph.Line)
                {
                    e.Graphics.DrawLine(pen, 4, 19, 20, 5);
                    using (Brush endpoint = new SolidBrush(ForeColor))
                    {
                        e.Graphics.FillEllipse(endpoint, 2, 17, 4, 4);
                        e.Graphics.FillEllipse(endpoint, 18, 3, 4, 4);
                    }
                }
                else if (Kind == Glyph.Rectangle) e.Graphics.DrawRectangle(pen, 4, 5, 16, 14);
                else if (Kind == Glyph.Undo)
                {
                    e.Graphics.DrawBezier(pen, new Point(5, 12), new Point(7, 4),
                        new Point(20, 6), new Point(20, 16));
                    e.Graphics.DrawLines(pen, new Point[] { new Point(9, 7), new Point(5, 12), new Point(11, 14) });
                }
                else if (Kind == Glyph.Cancel)
                {
                    e.Graphics.DrawLine(pen, 6, 6, 18, 18);
                    e.Graphics.DrawLine(pen, 18, 6, 6, 18);
                }
                else e.Graphics.DrawLines(pen, new Point[] { new Point(4, 12), new Point(10, 18),
                    new Point(20, 6) });
            }
        }
    }
    private sealed class Mark
    {
        internal Tool Tool;
        internal readonly List<Point> Points = new List<Point>();
        internal int Width;
        internal Color Color;
    }

    private readonly Bitmap desktop;
    private readonly Rectangle desktopBounds;
    private readonly List<Mark> marks = new List<Mark>();
    private readonly Panel toolbar = new Panel();
    private readonly FlowLayoutPanel leftActions = new FlowLayoutPanel(), rightActions = new FlowLayoutPanel();
    private readonly Panel options = new Panel();
    private readonly FlowLayoutPanel widthOptions = new FlowLayoutPanel(), colorOptions = new FlowLayoutPanel();
    private readonly IconButton penButton, lineButton, boxButton;
    private readonly Panel linePreview = new Panel();
    private readonly List<Button> swatches = new List<Button>();
    private readonly ToolTip hints = new ToolTip();
    private Tool? tool;
    private int toolbarWidth = 360;
    private int penWidth = 3;
    private Color penColor = Color.Red;
    private Point start;
    private Rectangle selection;
    private bool selecting;
    private Mark drawing;

    internal ScreenshotOverlay(Bitmap desktop, Rectangle desktopBounds)
    {
        this.desktop = desktop;
        this.desktopBounds = desktopBounds;
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Bounds = desktopBounds;
        TopMost = true;
        KeyPreview = true;
        DoubleBuffered = true;
        Cursor = Cursors.Cross;
        toolbar.BackColor = Color.FromArgb(245, 248, 251);
        toolbar.BorderStyle = BorderStyle.FixedSingle;
        toolbar.Visible = false;
        foreach (FlowLayoutPanel row in new[] { leftActions, rightActions, widthOptions, colorOptions })
        {
            row.AutoSize = true;
            row.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            row.WrapContents = false;
            row.Margin = Padding.Empty;
        }
        widthOptions.WrapContents = colorOptions.WrapContents = true;
        penButton = AddToolButton(Tool.Freehand, "画笔");
        lineButton = AddToolButton(Tool.Line, "直线");
        boxButton = AddToolButton(Tool.Rectangle, "矩形");
        AddActionButton("重选", delegate { marks.Clear(); selection = Rectangle.Empty; tool = null;
            options.Visible = false; toolbar.Visible = false; Invalidate(); });
        AddIconButton(Glyph.Undo, "撤销", Color.FromArgb(38, 67, 83), delegate
        { if (marks.Count > 0) { marks.RemoveAt(marks.Count - 1); Invalidate(selection); } });
        AddIconButton(Glyph.Cancel, "取消", Color.FromArgb(212, 63, 63), delegate { Close(); });
        AddIconButton(Glyph.Confirm, "确认并复制", Color.FromArgb(30, 145, 89), delegate { CopySelection(); });
        linePreview.Size = new Size(64, 36);
        linePreview.BackColor = Color.White;
        linePreview.BorderStyle = BorderStyle.FixedSingle;
        linePreview.Margin = new Padding(3);
        linePreview.Paint += delegate(object sender, PaintEventArgs paint)
        {
            paint.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen preview = new Pen(penColor, penWidth) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                paint.Graphics.DrawLine(preview, 12, linePreview.Height / 2, linePreview.Width - 12, linePreview.Height / 2);
        };
        widthOptions.Controls.Add(linePreview);
        TrackBar thickness = new TrackBar { AutoSize = false, Minimum = 1, Maximum = 6, Value = penWidth,
            TickStyle = TickStyle.None, Size = new Size(112, 36), Margin = new Padding(3) };
        thickness.ValueChanged += delegate { penWidth = thickness.Value; linePreview.Invalidate(); };
        widthOptions.Controls.Add(thickness);
        colorOptions.Controls.Add(new Label { Text = "颜色", AutoSize = true, TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Microsoft YaHei UI", 9f), Margin = new Padding(3, 8, 3, 3) });
        foreach (Color color in new Color[] { Color.Black, Color.Red, Color.Orange, Color.Gold,
            Color.ForestGreen, Color.DeepSkyBlue, Color.RoyalBlue, Color.MediumPurple })
        {
            Button swatch = new Button { Size = new Size(22, 22), BackColor = color,
                FlatStyle = FlatStyle.Flat, Margin = new Padding(2, 5, 2, 2), Cursor = Cursors.Hand,
                AccessibleName = "选择" + color.Name + "颜色" };
            swatch.FlatAppearance.BorderColor = Color.FromArgb(161, 174, 184);
            swatch.Click += delegate { SetColor(color); };
            hints.SetToolTip(swatch, color.Name);
            swatches.Add(swatch);
            colorOptions.Controls.Add(swatch);
        }
        Button moreColors = AddActionButton("更多", delegate
        {
            using (ColorDialog dialog = new ColorDialog { Color = penColor, FullOpen = true })
                if (dialog.ShowDialog(this) == DialogResult.OK) SetColor(dialog.Color);
        }, colorOptions);
        moreColors.Size = new Size(50, 32);
        hints.SetToolTip(moreColors, "自定义颜色");
        options.Controls.Add(widthOptions);
        options.Controls.Add(colorOptions);
        options.Visible = false;
        toolbar.Controls.Add(leftActions);
        toolbar.Controls.Add(rightActions);
        toolbar.Controls.Add(options);
        Controls.Add(toolbar);
        SetColor(penColor);
        LayoutToolbar();
    }

    private IconButton AddToolButton(Tool kind, string title)
    {
        IconButton button = new IconButton { Kind = (Glyph)kind, Size = new Size(38, 36), Margin = new Padding(3),
            FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = Color.FromArgb(38, 67, 83),
            Cursor = Cursors.Hand, AccessibleName = title };
        button.FlatAppearance.BorderColor = Color.FromArgb(210, 222, 230);
        button.Click += delegate { SelectTool(kind); };
        hints.SetToolTip(button, title);
        leftActions.Controls.Add(button);
        return button;
    }

    private IconButton AddIconButton(Glyph icon, string title, Color color, Action action)
    {
        IconButton button = new IconButton { Kind = icon, Size = new Size(38, 36), Margin = new Padding(3),
            FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = color,
            Cursor = Cursors.Hand, AccessibleName = title };
        button.FlatAppearance.BorderColor = Color.FromArgb(210, 222, 230);
        button.Click += delegate { action(); };
        hints.SetToolTip(button, title);
        rightActions.Controls.Add(button);
        return button;
    }

    private Button AddActionButton(string title, Action action, FlowLayoutPanel parent = null)
    {
        Font font = new Font("Microsoft YaHei UI", 9f);
        Button button = new Button { Text = title,
            Size = new Size(Math.Max(60, TextRenderer.MeasureText(title, font).Width + 18), Math.Max(36, font.Height + 14)),
            Margin = new Padding(3),
            Font = font, FlatStyle = FlatStyle.Flat,
            BackColor = Color.White, Cursor = Cursors.Hand };
        button.FlatAppearance.BorderColor = Color.FromArgb(210, 222, 230);
        button.Click += delegate { action(); };
        (parent ?? leftActions).Controls.Add(button);
        return button;
    }

    private void SelectTool(Tool choice)
    {
        tool = choice;
        penButton.BackColor = choice == Tool.Freehand ? Color.FromArgb(207, 235, 249) : Color.White;
        lineButton.BackColor = choice == Tool.Line ? Color.FromArgb(207, 235, 249) : Color.White;
        boxButton.BackColor = choice == Tool.Rectangle ? Color.FromArgb(207, 235, 249) : Color.White;
        options.Visible = true;
        LayoutToolbar();
        PositionToolbar();
    }

    private void SetColor(Color color)
    {
        penColor = color;
        foreach (Button swatch in swatches)
        {
            bool selected = swatch.BackColor.ToArgb() == color.ToArgb();
            swatch.FlatAppearance.BorderColor = selected ? Color.FromArgb(31, 130, 177) : Color.FromArgb(161, 174, 184);
            swatch.FlatAppearance.BorderSize = selected ? 3 : 1;
        }
        linePreview.Invalidate();
    }

    private void LayoutToolbar()
    {
        leftActions.PerformLayout();
        rightActions.PerformLayout();
        leftActions.Location = new Point(5, 5);
        rightActions.Location = new Point(Math.Max(5, toolbarWidth - rightActions.Width - 5),
            leftActions.Width + rightActions.Width + 18 <= toolbarWidth ? 5 : leftActions.Bottom + 3);
        int primaryBottom = Math.Max(leftActions.Bottom, rightActions.Bottom);
        widthOptions.MaximumSize = colorOptions.MaximumSize = new Size(Math.Max(1, toolbarWidth - 12), 0);
        widthOptions.Location = new Point(0, 0);
        widthOptions.PerformLayout();
        colorOptions.Location = new Point(0, widthOptions.Bottom + 3);
        colorOptions.PerformLayout();
        options.Location = new Point(5, primaryBottom + 5);
        options.Size = new Size(toolbarWidth - 12, colorOptions.Bottom);
        toolbar.Size = new Size(toolbarWidth, (tool.HasValue ? options.Bottom : primaryBottom) + 6);
    }

    internal static Rectangle SelectionBounds(Point a, Point b)
    {
        return Rectangle.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y),
            Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        if (selection.IsEmpty)
        {
            start = e.Location;
            selecting = true;
            Capture = true;
            return;
        }
        if (!selection.Contains(e.Location)) return;
        if (!tool.HasValue) return;
        drawing = new Mark { Tool = tool.Value, Width = penWidth, Color = penColor };
        drawing.Points.Add(e.Location);
        Capture = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (selecting)
        {
            selection = Rectangle.Intersect(ClientRectangle, SelectionBounds(start, e.Location));
            Invalidate();
        }
        else if (drawing != null)
        {
            drawing.Points.Add(new Point(Math.Max(selection.Left, Math.Min(selection.Right - 1, e.X)),
                Math.Max(selection.Top, Math.Min(selection.Bottom - 1, e.Y))));
            Invalidate(selection);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left) return;
        Capture = false;
        if (selecting)
        {
            selecting = false;
            selection = Rectangle.Intersect(ClientRectangle, SelectionBounds(start, e.Location));
            if (selection.Width < 4 || selection.Height < 4) selection = Rectangle.Empty;
            else PositionToolbar();
            Invalidate();
        }
        else if (drawing != null)
        {
            drawing.Points.Add(new Point(Math.Max(selection.Left, Math.Min(selection.Right - 1, e.X)),
                Math.Max(selection.Top, Math.Min(selection.Bottom - 1, e.Y))));
            marks.Add(drawing);
            drawing = null;
            Invalidate(selection);
        }
    }

    private void PositionToolbar()
    {
        toolbar.Visible = true;
        Point selectedScreen = new Point(desktopBounds.Left + selection.Left, desktopBounds.Top + selection.Top);
        Rectangle monitor = Screen.FromPoint(selectedScreen).Bounds;
        Rectangle visible = Rectangle.Intersect(ClientRectangle, new Rectangle(monitor.Left - desktopBounds.Left,
            monitor.Top - desktopBounds.Top, monitor.Width, monitor.Height));
        toolbarWidth = Math.Max(1, Math.Min(360, visible.Width - 16));
        LayoutToolbar();
        int left = Math.Max(visible.Left,
            Math.Min(visible.Right - toolbar.Width, selection.Left + (selection.Width - toolbar.Width) / 2));
        int below = selection.Bottom + 8;
        int top = below + toolbar.Height <= visible.Bottom
            ? below : Math.Max(visible.Top, selection.Top - toolbar.Height - 8);
        toolbar.Location = new Point(left, top);
        toolbar.BringToFront();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.DrawImageUnscaled(desktop, Point.Empty);
        using (Brush shade = new SolidBrush(Color.FromArgb(105, Color.Black)))
            e.Graphics.FillRectangle(shade, ClientRectangle);
        if (selection.IsEmpty) return;
        e.Graphics.DrawImage(desktop, selection, selection, GraphicsUnit.Pixel);
        e.Graphics.SetClip(selection);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        foreach (Mark mark in marks) DrawMark(e.Graphics, mark, Point.Empty);
        if (drawing != null) DrawMark(e.Graphics, drawing, Point.Empty);
        e.Graphics.ResetClip();
        using (Pen border = new Pen(Color.FromArgb(0, 153, 220), 2)) e.Graphics.DrawRectangle(border,
            selection.Left, selection.Top, selection.Width - 1, selection.Height - 1);
    }

    private static void DrawMark(Graphics graphics, Mark mark, Point offset)
    {
        Point first = new Point(mark.Points[0].X + offset.X, mark.Points[0].Y + offset.Y);
        Point last = new Point(mark.Points[mark.Points.Count - 1].X + offset.X,
            mark.Points[mark.Points.Count - 1].Y + offset.Y);
        using (Pen pen = new Pen(mark.Color, mark.Width) { StartCap = LineCap.Round, EndCap = LineCap.Round,
            LineJoin = LineJoin.Round })
        {
            if (mark.Tool == Tool.Line) graphics.DrawLine(pen, first, last);
            else if (mark.Tool == Tool.Rectangle)
            {
                Rectangle box = SelectionBounds(first, last);
                graphics.DrawRectangle(pen, box.X, box.Y, box.Width, box.Height);
            }
            else if (mark.Points.Count == 1)
            {
                using (Brush dot = new SolidBrush(mark.Color))
                    graphics.FillEllipse(dot, first.X - mark.Width / 2,
                        first.Y - mark.Width / 2, mark.Width, mark.Width);
            }
            else
                for (int i = 1; i < mark.Points.Count; i++)
                    graphics.DrawLine(pen, mark.Points[i - 1].X + offset.X, mark.Points[i - 1].Y + offset.Y,
                        mark.Points[i].X + offset.X, mark.Points[i].Y + offset.Y);
        }
    }

    private Bitmap BuildSelectionBitmap()
    {
        Bitmap cropped = new Bitmap(selection.Width, selection.Height, PixelFormat.Format32bppArgb);
        try
        {
            using (Graphics graphics = Graphics.FromImage(cropped))
            {
                graphics.DrawImage(desktop, new Rectangle(Point.Empty, cropped.Size), selection, GraphicsUnit.Pixel);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Point offset = new Point(-selection.Left, -selection.Top);
                foreach (Mark mark in marks) DrawMark(graphics, mark, offset);
            }
            return cropped;
        }
        catch { cropped.Dispose(); throw; }
    }

    private void CopySelection()
    {
        if (selection.IsEmpty) return;
        using (Bitmap cropped = BuildSelectionBitmap())
        {
            try { Clipboard.SetDataObject(cropped, true, 10, 100); }
            catch (Exception error) { MessageBox.Show(this, error.Message, "复制截图失败"); return; }
        }
        Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) { Close(); e.Handled = true; }
        else if (e.KeyCode == Keys.Enter && !selection.IsEmpty && !toolbar.ContainsFocus)
        { CopySelection(); e.Handled = true; }
        base.OnKeyDown(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { hints.Dispose(); desktop.Dispose(); }
        base.Dispose(disposing);
    }
}

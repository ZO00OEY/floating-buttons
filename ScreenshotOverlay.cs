using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

internal sealed class ScreenshotOverlay : Form
{
    private enum Tool { Freehand, Line, Rectangle }
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
    private readonly FlowLayoutPanel toolbar = new FlowLayoutPanel();
    private readonly Button penButton, lineButton, boxButton;
    private Tool tool = Tool.Freehand;
    private int penWidth = 4;
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
        toolbar.AutoSize = true;
        toolbar.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        toolbar.WrapContents = true;
        toolbar.Padding = new Padding(5);
        toolbar.BackColor = Color.FromArgb(245, 248, 251);
        toolbar.BorderStyle = BorderStyle.FixedSingle;
        toolbar.Visible = false;
        penButton = AddButton("画笔", delegate { SelectTool(Tool.Freehand); });
        lineButton = AddButton("直线", delegate { SelectTool(Tool.Line); });
        boxButton = AddButton("矩形", delegate { SelectTool(Tool.Rectangle); });
        ComboBox widths = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Microsoft YaHei UI", 9f), Margin = new Padding(3, 4, 3, 3) };
        widths.Width = TextRenderer.MeasureText("12 像素", widths.Font).Width + SystemInformation.VerticalScrollBarWidth + 16;
        widths.Items.AddRange(new object[] { "2 像素", "4 像素", "8 像素", "12 像素" });
        widths.SelectedIndex = 1;
        widths.SelectedIndexChanged += delegate { penWidth = new int[] { 2, 4, 8, 12 }[widths.SelectedIndex]; };
        toolbar.Controls.Add(widths);
        AddButton("颜色", delegate
        {
            using (ColorDialog dialog = new ColorDialog { Color = penColor, FullOpen = true })
                if (dialog.ShowDialog(this) == DialogResult.OK) penColor = dialog.Color;
        });
        AddButton("撤销", delegate { if (marks.Count > 0) { marks.RemoveAt(marks.Count - 1); Invalidate(selection); } });
        AddButton("重选", delegate { marks.Clear(); selection = Rectangle.Empty; toolbar.Visible = false; Invalidate(); });
        Button copy = AddButton("复制", delegate { CopySelection(); });
        copy.BackColor = Color.FromArgb(31, 130, 177);
        copy.ForeColor = Color.White;
        AddButton("取消", delegate { Close(); });
        Controls.Add(toolbar);
        SelectTool(Tool.Freehand);
    }

    private Button AddButton(string title, Action action)
    {
        Font font = new Font("Microsoft YaHei UI", 9f);
        Button button = new Button { Text = title,
            Size = new Size(Math.Max(60, TextRenderer.MeasureText(title, font).Width + 18), Math.Max(36, font.Height + 14)),
            Margin = new Padding(3),
            Font = font, FlatStyle = FlatStyle.Flat,
            BackColor = Color.White, Cursor = Cursors.Hand };
        button.FlatAppearance.BorderColor = Color.FromArgb(210, 222, 230);
        button.Click += delegate { action(); };
        toolbar.Controls.Add(button);
        return button;
    }

    private void SelectTool(Tool choice)
    {
        tool = choice;
        penButton.BackColor = choice == Tool.Freehand ? Color.FromArgb(207, 235, 249) : Color.White;
        lineButton.BackColor = choice == Tool.Line ? Color.FromArgb(207, 235, 249) : Color.White;
        boxButton.BackColor = choice == Tool.Rectangle ? Color.FromArgb(207, 235, 249) : Color.White;
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
        drawing = new Mark { Tool = tool, Width = penWidth, Color = penColor };
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
        toolbar.PerformLayout();
        Point selectedScreen = new Point(desktopBounds.Left + selection.Left, desktopBounds.Top + selection.Top);
        Rectangle monitor = Screen.FromPoint(selectedScreen).Bounds;
        Rectangle visible = Rectangle.Intersect(ClientRectangle, new Rectangle(monitor.Left - desktopBounds.Left,
            monitor.Top - desktopBounds.Top, monitor.Width, monitor.Height));
        toolbar.MaximumSize = new Size(Math.Max(300, visible.Width - 16), 0);
        toolbar.PerformLayout();
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
        if (disposing) desktop.Dispose();
        base.Dispose(disposing);
    }
}

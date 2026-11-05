using System.Drawing.Drawing2D;

namespace NfcLens;

// A payment card drawn at the standard proportions. The front shows the network, chip, number and cardholder;
// the back shows dates and application details. Click to flip. Masked values stay masked unless Reveal is on.
sealed class CardView : Control
{
    const double AspectRatio = 1.586;
    static readonly Color FaceStart = Color.FromArgb(30, 58, 138);
    static readonly Color FaceEnd = Color.FromArgb(79, 70, 229);
    static readonly Color MutedInk = Color.FromArgb(199, 210, 254);
    readonly Dictionary<string, Image?> logos = new();
    readonly System.Windows.Forms.Timer timer = new() { Interval = 15 };
    CardSummary? summary;
    bool showBack;
    bool shrinking = true;
    float scale = 1f;

    public CardView()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
        timer.Tick += (_, _) => Step();
    }

    public void SetSummary(CardSummary? value)
    {
        summary = value;
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        int wanted = (int)Math.Round(Width / AspectRatio);
        if (Height != wanted) Height = wanted;
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        if (summary == null || timer.Enabled) return;
        shrinking = true;
        timer.Start();
    }

    // Flip in two halves: narrow to a line, swap sides, then widen again.
    void Step()
    {
        if (shrinking)
        {
            scale = Math.Max(0f, scale - 0.14f);
            if (scale == 0f) { showBack = !showBack; shrinking = false; }
        }
        else
        {
            scale = Math.Min(1f, scale + 0.14f);
            if (scale == 1f) timer.Stop();
        }
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (summary == null || Width < 40 || Height < 40) return;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        float centre = Width / 2f;
        g.TranslateTransform(centre, 0);
        g.ScaleTransform(Math.Max(scale, 0.01f), 1f);
        g.TranslateTransform(-centre, 0);

        var bounds = new RectangleF(0, 0, Width - 1, Height - 1);
        using var face = RoundedRect(bounds, Width * 0.045f);
        using var fill = new LinearGradientBrush(bounds, FaceStart, FaceEnd, 30f);
        g.FillPath(fill, face);

        if (showBack) DrawBack(g);
        else DrawFront(g);
    }

    void DrawFront(Graphics g)
    {
        float u = Width / 100f, pad = u * 6;
        DrawBrand(g, u, pad);
        DrawChip(g, new RectangleF(pad, Height * 0.30f, u * 12, u * 9));

        using var numberFont = new Font("Consolas", u * 4.6f, FontStyle.Regular, GraphicsUnit.Pixel);
        g.DrawString(summary!.PanDisplay, numberFont, Brushes.White, pad, Height * 0.55f);

        using var labelFont = new Font("Segoe UI", u * 2.2f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var nameFont = new Font("Segoe UI", u * 3.6f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var muted = new SolidBrush(MutedInk);
        g.DrawString("CARDHOLDER", labelFont, muted, pad, Height * 0.78f);
        g.DrawString(summary.NameDisplay, nameFont, Brushes.White, pad, Height * 0.78f + u * 3.5f);
    }

    void DrawBack(Graphics g)
    {
        float u = Width / 100f, pad = u * 6;
        using (var stripe = new SolidBrush(Color.FromArgb(11, 15, 26)))
            g.FillRectangle(stripe, 0, u * 14, Width, u * 9);

        float column = (Width - pad * 2) / 2f;
        var rows = new (string Label, string Value)[][]
        {
            [("Expires", summary!.ExpiryDisplay), ("Valid from", summary.EffectiveDisplay)],
            [("Card sequence", summary.SequenceDisplay), ("Issued in", summary.IssuerCountry ?? "Not read")],
            [("Application", summary.ApplicationLabel ?? "Not read")],
            [("Application ID", summary.Aid ?? "Not read")],
        };
        float y = u * 26;
        foreach (var row in rows)
        {
            for (int c = 0; c < row.Length; c++)
                DrawField(g, row[c].Label, row[c].Value, pad + c * column, y, column - u * 2);
            y += u * 9f;
        }
    }

    void DrawField(Graphics g, string label, string value, float x, float y, float width)
    {
        float u = Width / 100f;
        using var labelFont = new Font("Segoe UI", u * 2f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var valueFont = new Font("Segoe UI", u * 3.4f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var muted = new SolidBrush(MutedInk);
        using var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        g.DrawString(label.ToUpperInvariant(), labelFont, muted, new RectangleF(x, y, width, u * 3), format);
        g.DrawString(value, valueFont, Brushes.White, new RectangleF(x, y + u * 3.4f, width, u * 4.5f), format);
    }

    // Network mark in the top-right corner. Uses Assets\brand_visa.png or brand_mastercard.png when supplied.
    void DrawBrand(Graphics g, float u, float pad)
    {
        var logo = Logo(summary!.Brand);
        if (logo != null)
        {
            float h = u * 9, w = logo.Width * h / logo.Height;
            g.DrawImage(logo, Width - pad - w, pad, w, h);
            return;
        }
        string text = (summary.Brand ?? "Payment card").ToUpperInvariant();
        using var font = new Font("Segoe UI", u * 4.2f, FontStyle.Bold, GraphicsUnit.Pixel);
        var size = g.MeasureString(text, font);
        g.DrawString(text, font, Brushes.White, Width - pad - size.Width, pad);
    }

    Image? Logo(string? brand)
    {
        string file = brand switch { "Visa" => "brand_visa.png", "Mastercard" => "brand_mastercard.png", _ => "" };
        if (file.Length == 0) return null;
        if (!logos.TryGetValue(file, out var image))
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Assets", file);
            image = File.Exists(path) ? Image.FromFile(path) : null;
            logos[file] = image;
        }
        return image;
    }

    static void DrawChip(Graphics g, RectangleF r)
    {
        using var path = RoundedRect(r, r.Height * 0.18f);
        using var fill = new LinearGradientBrush(r, Color.FromArgb(243, 217, 139), Color.FromArgb(185, 139, 46), 45f);
        using var pen = new Pen(Color.FromArgb(120, 90, 30), 0.6f);
        g.FillPath(fill, path);
        g.DrawPath(pen, path);
        g.DrawLine(pen, r.Left + r.Width * 0.35f, r.Top, r.Left + r.Width * 0.35f, r.Bottom);
        g.DrawLine(pen, r.Left + r.Width * 0.65f, r.Top, r.Left + r.Width * 0.65f, r.Bottom);
        g.DrawLine(pen, r.Left, r.Top + r.Height / 2, r.Right, r.Top + r.Height / 2);
    }

    // Shared with TileView so the cards and tiles have matching corners.
    internal static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        float d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Dispose();
            foreach (var logo in logos.Values) logo?.Dispose();
        }
        base.Dispose(disposing);
    }
}

// One decoded field as a white tile. Click the tile to see what the tag means.
sealed class TileView : Control
{
    readonly CardTile tile;

    public TileView(CardTile tile)
    {
        this.tile = tile;
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
        Height = 64;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        var bounds = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
        using var path = CardView.RoundedRect(bounds, 12);
        using var fill = new SolidBrush(Color.White);
        using var border = new Pen(Color.FromArgb(226, 232, 240));
        g.FillPath(fill, path);
        g.DrawPath(border, path);

        string heading = tile.Title + (tile.Tag == null ? "" : "  ·  " + tile.Tag);
        bool hidden = tile.Value == "[hidden]";
        using var headingFont = new Font("Segoe UI", 8.5f);
        using var valueFont = new Font("Segoe UI", 11f, FontStyle.Bold);
        using var headingBrush = new SolidBrush(Color.FromArgb(100, 116, 139));
        using var valueBrush = new SolidBrush(hidden ? Color.FromArgb(148, 163, 184) : Color.FromArgb(15, 23, 42));
        using var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        g.DrawString(heading, headingFont, headingBrush, new RectangleF(14, 9, Width - 28, 18), format);
        g.DrawString(tile.Value, valueFont, valueBrush, new RectangleF(14, 28, Width - 28, 26), format);
    }
}

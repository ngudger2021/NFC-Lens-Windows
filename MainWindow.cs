using System.Collections.Concurrent;
using System.Text;

namespace NfcLens;

public partial class MainWindow : Form
{
    InspectionSession session = new();
    readonly ConcurrentQueue<string> pendingLog = new();
    readonly System.Windows.Forms.Timer logTimer = new() { Interval = 50 };
    bool busy;
    byte[] classicKey = Convert.FromHexString("FFFFFFFFFFFF");
    bool classicKeyB;

    public MainWindow()
    {
        InitializeComponent();
        refresh.Click += (_, _) => RefreshReaders();
        read.Click += async (_, _) => await ReadCard();
        demo.Click += (_, _) => LoadDemo();
        reveal.CheckedChanged += (_, _) => { Render(); RenderLog(); };
        logTimer.Tick += (_, _) => DrainLog();
        export.Click += (_, _) => Export();
        scanMode.SelectedIndex = 0;
        classicSettings.Click += (_, _) =>
        {
            using var dialog = new ClassicKeyDialog(classicKey, classicKeyB);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(classicKey);
            classicKey = dialog.Key;
            classicKeyB = dialog.KeyB;
        };
        cards.Resize += (_, _) => ReflowCards();
        Shown += (_, _) => { Render(); RenderLog(); RefreshReaders(); };
        FormClosed += (_, _) =>
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(classicKey);
            logTimer.Dispose();
            help.Dispose();
        };
    }

    void RefreshReaders()
    {
        try
        {
            using var pc = new Pcsc();
            var names = pc.Readers();
            readers.Items.Clear();
            readers.Items.AddRange(names);
            if (names.Length > 0) readers.SelectedIndex = 0;
            status.Text = names.Length == 0 ? "No readers found. Connect a PC/SC reader and install its driver, then refresh." : $"{names.Length} reader(s) available. Place or insert a card.";
        }
        catch (Exception ex) { status.Text = ex.Message; }
    }

    async Task ReadCard()
    {
        if (busy || readers.SelectedItem is not string name) return;
        bool emvOnly = scanMode.SelectedIndex == 1;
        byte[] scanKey = (byte[])classicKey.Clone();
        bool scanKeyB = classicKeyB;
        busy = true;
        read.Enabled = refresh.Enabled = demo.Enabled = readers.Enabled = scanMode.Enabled = classicSettings.Enabled = export.Enabled = false;
        progress.Visible = true;

        session = new InspectionSession();
        session.Log("Connecting to reader: " + name);
        pages.SelectedTab = livePage;
        status.Text = "Detecting NFC / EMV… Keep the card on the reader.";
        Render();
        RenderLog();
        logTimer.Start();
        try
        {
            var scan = await Task.Run(() =>
            {
                using var pc = new Pcsc();
                pc.Connect(name);
                var atr = pc.Atr();
                pendingLog.Enqueue("Connected. ATR: " + Convert.ToHexString(atr));
                void Report(string message) => pendingLog.Enqueue(message);
                var scanner = new CardScan(pc.Exchange, Report);
                try
                {
                    scanner.Read(atr, emvOnly, scanKey, scanKeyB);
                }
                catch (Exception ex) when (ex is IOException or FormatException)
                {
                    Report("Read stopped: " + ex.Message);
                    scanner.Nfc.Add(new("Read status", ex.Message));
                }
                return (scanner.Emv, scanner.Nfc);
            });
            session.AddEmv(scan.Emv);
            foreach (var field in scan.Nfc) session.Add(field);
            DrainLog();
            session.Log("Scan complete.");
            status.Text = scan.Nfc.Any(field => field.Name == "Read status")
                ? "Scan finished with limitations. See Read status in Card details and the live log."
                : "Scan complete. Tap a field to see what it means.";
        }
        catch (Exception ex)
        {
            DrainLog();
            status.Text = ex.Message;
            session.Log("Read stopped: " + ex.Message);
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(scanKey);
            logTimer.Stop();
            busy = false;
            progress.Visible = false;
            read.Enabled = refresh.Enabled = demo.Enabled = readers.Enabled = scanMode.Enabled = classicSettings.Enabled = export.Enabled = true;
            pages.SelectedTab = detailsPage;
            Render();
            RenderLog();
        }
    }

    void LoadDemo()
    {
        session = InspectionSession.Demo();
        pages.SelectedTab = detailsPage;
        status.Text = "Fictional demo; no card scanned.";
        Render();
        RenderLog();
    }

    // Rebuilds the card summary and the grouped field tiles from the current session.
    void Render()
    {
        cards.SuspendLayout();
        foreach (Control child in cards.Controls) child.Dispose();
        cards.Controls.Clear();

        var summary = session.Summary(reveal.Checked);
        if (summary != null)
        {
            var face = new CardView { Margin = new Padding(0, 0, 0, 4) };
            face.SetSummary(summary);
            cards.Controls.Add(face);
            cards.Controls.Add(Hint("Click the card to flip it"));
        }

        var tiles = session.Tiles(reveal.Checked);
        if (tiles.Count == 0)
        {
            cards.Controls.Add(new Label
            {
                Text = "No card data yet.\n\nRead a card or tag against the reader, or load the demo to see fictional data.",
                AutoSize = true,
                ForeColor = Color.SlateGray,
                Font = new Font("Segoe UI", 11F),
                Padding = new Padding(4, 16, 4, 4)
            });
        }

        string? group = null;
        foreach (var tile in tiles)
        {
            if (tile.Group != group)
            {
                group = tile.Group;
                cards.Controls.Add(SectionHeader(group));
            }
            var view = new TileView(tile) { Margin = new Padding(0, 0, 0, 8) };
            view.Click += (_, _) => ShowExplanation(tile);
            cards.Controls.Add(view);
        }
        ReflowCards();
        cards.ResumeLayout();
    }

    void ReflowCards()
    {
        int width = Math.Max(240, cards.ClientSize.Width - cards.Padding.Horizontal - 20);
        foreach (Control child in cards.Controls)
        {
            if (child is CardView) child.Width = Math.Min(width, 560);
            else if (child is TileView) child.Width = width;
            else if (child is Label label) label.MaximumSize = new Size(width, 0);
        }
    }

    static Label Hint(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Color.SlateGray,
        Font = new Font("Segoe UI", 9F),
        Margin = new Padding(0, 0, 0, 10)
    };

    static Label SectionHeader(string group) => new()
    {
        Text = GroupLabel(group).ToUpperInvariant(),
        AutoSize = true,
        ForeColor = Color.SlateGray,
        Font = new Font("Segoe UI", 9F, FontStyle.Bold),
        Margin = new Padding(2, 14, 0, 6)
    };

    static string GroupLabel(string group) => group switch
    {
        "Card" => "Card data",
        "Application" => "Application",
        "Processing" => "Processing",
        _ => "Device"
    };

    void ShowExplanation(CardTile tile)
    {
        string heading = tile.Tag == null ? "Non-EMV field" : "Tag " + tile.Tag;
        string note = tile.Sensitive ? "\n\nThis value is masked unless Reveal is on." : "";
        MessageBox.Show(this, $"{heading}\n\n{tile.Explanation}\n\nValue: {tile.Value}{note}", tile.Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    void DrainLog()
    {
        bool changed = false;
        while (pendingLog.TryDequeue(out var message))
        {
            session.Log(message);
            changed = true;
        }
        if (changed) RenderLog();
    }

    void RenderLog()
    {
        log.Text = session.Transcript(reveal.Checked);
        if (log.TextLength > 0) log.AppendText(Environment.NewLine);
        log.SelectionStart = log.TextLength;
        log.ScrollToCaret();
    }

    void Export()
    {
        using var dialog = new SaveFileDialog { Filter = "Text report|*.txt", FileName = "NFC-Lens-report.txt" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            string report = session.Report(reveal.Checked).Replace("NFC Lens Android report", "NFC Lens report");
            File.WriteAllText(dialog.FileName, report, Encoding.UTF8);
            status.Text = "Report saved.";
        }
        catch (Exception ex) { status.Text = "Could not save: " + ex.Message; }
    }
}

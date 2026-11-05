using System.Text;

namespace NfcLens;

public partial class MainWindow : Form
{
    readonly List<(string Label, byte[] Data)> results = [];
    readonly List<NfcField> nfcFields = [];
    readonly List<(DateTime Time, string Message)> transcript = [];
    readonly System.Collections.Concurrent.ConcurrentQueue<(DateTime Time, string Message)> pendingLog = new();
    readonly System.Windows.Forms.Timer logTimer = new() { Interval = 50 };
    readonly Font keyFont = new("Consolas", 10, FontStyle.Bold);
    bool busy;
    byte[] classicKey = Convert.FromHexString("FFFFFFFFFFFF");
    bool classicKeyB;
    void UpdateLearningMode()
    {
        learningPage.Enabled = learningMode.Checked;
        if (!learningMode.Checked)
        {
            if (pages.SelectedTab == learningPage) pages.SelectedIndex = 0;
            return;
        }
        ResetLessons();
        foreach (var entry in transcript) AddLearning(entry.Message);
        pages.SelectedTab = learningPage;
    }
    void ResetLessons()
    {
        lessonList.Items.Clear();
        lessonList.DisplayMember = nameof(Lesson.Title);
        lessonList.Items.Add(LearningGuide.Introduction);
        lessonList.SelectedIndex = 0;
    }
    void AddLearning(string message)
    {
        if (!learningMode.Checked) return;
        var lesson = LearningGuide.Explain(message);
        if (lesson != null) lessonList.Items.Add(lesson);
    }
    void ShowLesson()
    {
        if (lessonList.SelectedItem is Lesson lesson) lessonText.Text = lesson.Body.Replace("\n", Environment.NewLine);
    }
    void RunPractice()
    {
        ResetLessons();
        foreach (var message in LearningGuide.Practice)
        {
            var lesson = LearningGuide.Explain(message);
            if (lesson != null) lessonList.Items.Add(lesson with { Title = "Practice: " + lesson.Title });
        }
        lessonList.SelectedIndex = 1;
    }
    void ExplainField(TreeNode? node)
    {
        if (!learningMode.Checked || node?.Tag is not string tag) return;
        if (tag == "NFC")
        {
            lessonList.Items.Add(new Lesson("NFC tags and NDEF", LearningGuide.NfcIntroduction));
            lessonList.SelectedIndex = lessonList.Items.Count - 1;
            pages.SelectedTab = learningPage;
            return;
        }
        var lesson = new Lesson($"Field {tag}: {Tlv.Name(tag)}", $"{FieldHelp.Tag(tag)}\n\nTLV means Tag-Length-Value. The tag identifies the field; length tells the parser how many value bytes follow. Constructed tags contain nested fields.\n\nTry it: compare this field with its parent in the card details tree. Learning notes never copy cardholder values.");
        lessonList.Items.Add(lesson);
        lessonList.SelectedIndex = lessonList.Items.Count - 1;
        pages.SelectedTab = learningPage;
    }
    public MainWindow()
    {
        InitializeComponent();
        refresh.Click += (_, _) => RefreshReaders();
        read.Click += async (_, _) => await ReadCard();
        demo.Click += (_, _) => LoadDemo();
        reveal.CheckedChanged += (_, _) => Render();
        logTimer.Tick += (_, _) => DrainLog();
        export.Click += (_, _) => Export();
        learningMode.CheckedChanged += (_, _) => UpdateLearningMode();
        lessonList.SelectedIndexChanged += (_, _) => ShowLesson();
        practice.Click += (_, _) => RunPractice();
        scanMode.SelectedIndex = 0;
        classicSettings.Click += (_, _) =>
        {
            using var dialog = new ClassicKeyDialog(classicKey, classicKeyB);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(classicKey);
            classicKey = dialog.Key;
            classicKeyB = dialog.KeyB;
        };
        tags.AfterSelect += (_, e) => ExplainField(e.Node);
        Shown += (_, _) => { Render(); RefreshReaders(); };
        FormClosed += (_, _) => { System.Security.Cryptography.CryptographicOperations.ZeroMemory(classicKey); logTimer.Dispose(); keyFont.Dispose(); help.Dispose(); };
    }
    void RefreshReaders()
    {
        try { using var pc = new Pcsc(); var names = pc.Readers(); readers.Items.Clear(); readers.Items.AddRange(names); if (names.Length > 0) readers.SelectedIndex = 0; status.Text = names.Length == 0 ? "No readers found. Connect a PC/SC reader and install its driver, then refresh." : $"{names.Length} reader(s) available. Place or insert a card."; }
        catch (Exception ex) { status.Text = ex.Message; }
    }
    async Task ReadCard()
    {
        if (busy || readers.SelectedItem is not string name) return;
        bool emvOnly = scanMode.SelectedIndex == 1;
        byte[] scanKey = (byte[])classicKey.Clone();
        bool scanKeyB = classicKeyB;
        scanMode.Enabled = classicSettings.Enabled = false;
        busy = true; read.Enabled = refresh.Enabled = demo.Enabled = readers.Enabled = false;
        if (learningMode.Checked) ResetLessons();
        pages.SelectedIndex = learningMode.Checked ? 2 : 1;
        results.Clear(); nfcFields.Clear(); transcript.Clear(); Render(); status.Text = "Detecting NFC / EMV… Keep the card on the reader.";
        transcript.Add((DateTime.Now, "Connecting to reader: " + name));
        RenderLog();
        logTimer.Start();
        try
        {
            var scan = await Task.Run(() =>
            {
                using var pc = new Pcsc(); pc.Connect(name);
                var atr = pc.Atr();
                pendingLog.Enqueue((DateTime.Now, "Connected. ATR: " + Convert.ToHexString(atr)));
                void Report(string message) => pendingLog.Enqueue((DateTime.Now, message));
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
            results.AddRange(scan.Emv);
            nfcFields.AddRange(scan.Nfc);
            DrainLog();
            transcript.Add((DateTime.Now, "Scan complete."));
            status.Text = nfcFields.Any(field => field.Name == "Read status")
                ? "Scan finished with limitations. See Read status in Card details and the live log."
                : "Scan complete. Open Card details for NFC records or EMV fields.";
        }
        catch (Exception ex) { DrainLog(); status.Text = ex.Message; transcript.Add((DateTime.Now, "Read stopped: " + ex.Message)); }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(scanKey);
            logTimer.Stop(); busy = false;
            read.Enabled = refresh.Enabled = demo.Enabled = readers.Enabled = scanMode.Enabled = classicSettings.Enabled = true;
            Render();
        }
    }
    void LoadDemo()
    {
        results.Clear(); nfcFields.Clear(); transcript.Clear();
        nfcFields.Add(new("Demo NFC tag", "Fictional NDEF text and URI records; no tag scanned."));
        nfcFields.AddRange(Ndef.Decode(Convert.FromHexString("9101085402656E48656C6C6F51010C55046578616D706C652E636F6D")));
        results.Add(("DEMO • fictional card record", Convert.FromHexString("70235A0847617390010100105F24032912315F200C44454D4F2F434152442020205F340101")));
        transcript.Add((DateTime.Now, "DEMO ONLY — no reader connected and no real card scanned.")); status.Text = "Demo loaded • fictional data"; Render();
        if (learningMode.Checked) { RunPractice(); pages.SelectedTab = learningPage; }
    }
    void Render()
    {
        tags.BeginUpdate(); tags.Nodes.Clear();
        var summary = tags.Nodes.Add("★ KEY CARD INFORMATION");
        summary.ToolTipText = "A shortcut to important fields from the records below.\nThese are the same card data, grouped by their source record.\nHover over a field to learn what it means.";
        summary.NodeFont = keyFont;
        summary.BackColor = Color.FromArgb(220, 238, 255);
        if (results.Count > 0) summary.Nodes.Add("Sort code: not identified by this reader flow; cannot be derived from PAN.");
        foreach (var field in nfcFields)
        {
            var node = summary.Nodes.Add(field.Name + ": " + (field.Sensitive && !reveal.Checked ? "[hidden]" : field.Value));
            node.Tag = "NFC";
            node.ToolTipText = "NFC tag / NDEF field. Enable Learning mode and select this entry for an explanation.";
        }
        int found = 0;
        foreach (var (label, data) in results)
        {
            try
            {
                var important = Tlv.Parse(data).SelectMany(t => t.Flatten()).Where(t => Tlv.Key(t.Tag)).ToList();
                if (important.Count == 0) continue;
                var group = summary.Nodes.Add(label);
                group.ToolTipText = FieldHelp.Heading(label);
                foreach (var item in important) { AddNode(group, item); found++; }
            }
            catch (FormatException) { /* The full tree below shows decode errors. */ }
        }
        if (found == 0 && nfcFields.Count == 0) summary.Nodes.Add("Read an NFC tag or card, or load the demo to see fields.");
        else if (found > 0) summary.Nodes.Add("Missing Track 1 / Track 2 fields were not returned in the scanned records.");
        foreach (var (label, data) in results)
        {
            var parent = tags.Nodes.Add(label);
            parent.ToolTipText = FieldHelp.Heading(label);
            try { foreach (var tlv in Tlv.Parse(data)) AddNode(parent, tlv); }
            catch (Exception ex) { parent.Nodes.Add("Cannot decode: " + ex.Message); }
        }
        tags.ExpandAll();
        tags.TopNode = summary;
        tags.EndUpdate();
        RenderLog();
    }
    void DrainLog()
    {
        bool changed = false;
        while (pendingLog.TryDequeue(out var entry))
        {
            transcript.Add(entry);
            AddLearning(entry.Message);
            log.AppendText($"[{entry.Time:HH:mm:ss.fff}] {EmvScanner.DisplayLog(entry.Message, reveal.Checked)}{Environment.NewLine}");
            changed = true;
        }
        if (changed) { log.SelectionStart = log.TextLength; log.ScrollToCaret(); }
    }
    void RenderLog()
    {
        log.Text = string.Join(Environment.NewLine, transcript.Select(entry => $"[{entry.Time:HH:mm:ss.fff}] {EmvScanner.DisplayLog(entry.Message, reveal.Checked)}"));
        if (log.TextLength > 0) log.AppendText(Environment.NewLine);
        log.SelectionStart = log.TextLength; log.ScrollToCaret();
    }
    void AddNode(TreeNode parent, Tlv item)
    {
        string value = !reveal.Checked && Tlv.Sensitive(item.Tag) ? "[hidden]" : item.Constructed ? "" : item.Display();
        var node = parent.Nodes.Add($"{item.Tag}  {Tlv.Name(item.Tag)}  {value}");
        node.Tag = item.Tag;
        node.ToolTipText = $"{item.Tag} — {Tlv.Name(item.Tag)}\n{FieldHelp.Tag(item.Tag)}\nValue length: {item.Value.Length} byte(s).";
        if (Tlv.Key(item.Tag))
        {
            node.NodeFont = keyFont;
            node.ForeColor = Color.FromArgb(12, 55, 100);
            node.BackColor = Color.FromArgb(225, 240, 255);
        }
        foreach (var child in item.Children) AddNode(node, child);
    }
    void Export()
    {
        using var dialog = new SaveFileDialog { Filter = "Text report|*.txt", FileName = "NFC-Lens-report.txt" };
        if (dialog.ShowDialog() != DialogResult.OK) return;
        try
        {
            var sb = new StringBuilder("NFC Lens report\r\n");
            void Walk(TreeNode n, int depth) { sb.AppendLine(new string(' ', depth * 2) + n.Text); foreach (TreeNode c in n.Nodes) Walk(c, depth + 1); }
            foreach (TreeNode node in tags.Nodes) Walk(node, 0);
            sb.AppendLine().AppendLine(log.Text); File.WriteAllText(dialog.FileName, sb.ToString()); status.Text = "Report saved.";
        }
        catch (Exception ex) { status.Text = "Could not save: " + ex.Message; }
    }
}


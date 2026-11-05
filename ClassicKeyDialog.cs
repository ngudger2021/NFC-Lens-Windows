namespace NfcLens;

sealed class ClassicKeyDialog : Form
{
    readonly TextBox keyText = new() { Dock = DockStyle.Fill, MaxLength = 12, UseSystemPasswordChar = true };
    readonly ComboBox keyType = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    public byte[] Key => Convert.FromHexString(keyText.Text);
    public bool KeyB => keyType.SelectedIndex == 1;
    public ClassicKeyDialog(byte[] key, bool keyB)
    {
        Text = "MIFARE Classic key";
        ClientSize = new Size(460, 255);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = MinimizeBox = false;
        Font = new Font("Segoe UI", 10);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 1, RowCount = 6 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        layout.Controls.Add(new Label { Text = "Use one known key for each sector. Factory-default key: FFFFFFFFFFFF. Settings apply only to Classic cards and stay in memory for this session.", Dock = DockStyle.Fill });
        layout.Controls.Add(new Label { Text = "Key (12 hexadecimal characters)", AutoSize = true });
        keyText.Text = Convert.ToHexString(key);
        layout.Controls.Add(keyText);
        keyType.Items.AddRange(["Key A", "Key B"]);
        keyType.SelectedIndex = keyB ? 1 : 0;
        layout.Controls.Add(keyType);
        var defaults = new Button { Text = "Use factory default", AutoSize = true };
        defaults.Click += (_, _) => { keyText.Text = "FFFFFFFFFFFF"; keyType.SelectedIndex = 0; };
        layout.Controls.Add(defaults);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
        var save = new Button { Text = "Apply", AutoSize = true };
        save.Click += (_, _) =>
        {
            if (keyText.Text.Length != 12 || !keyText.Text.All(Uri.IsHexDigit))
            {
                MessageBox.Show(this, "Enter exactly 12 hexadecimal characters (0–9, A–F).", "Invalid key");
                return;
            }
            DialogResult = DialogResult.OK;
        };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        buttons.Controls.AddRange([save, cancel]);
        layout.Controls.Add(buttons);
        Controls.Add(layout);
        AcceptButton = save; CancelButton = cancel;
    }
}

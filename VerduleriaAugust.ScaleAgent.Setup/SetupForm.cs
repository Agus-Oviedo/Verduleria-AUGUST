using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Diagnostics;

namespace VerduleriaAugust.ScaleAgent.Setup;

public sealed class SetupForm : Form
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("VerduleriaAugust.ScaleAgent.ApiKey.v1");
    private readonly TextBox _server = Field("http://localhost:5266");
    private readonly NumericUpDown _scaleId = new() { Minimum = 1, Maximum = 999999, Value = 1, Dock = DockStyle.Fill };
    private readonly ComboBox _mode = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _port = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown };
    private readonly TextBox _key = Field("");
    private readonly NumericUpDown _weight = new() { DecimalPlaces = 3, Increment = .050m, Minimum = 0, Maximum = 1000, Value = 1.250m, Dock = DockStyle.Fill };
    private readonly Label _status = new() { AutoSize = true, ForeColor = Color.FromArgb(80, 100, 88), Padding = new Padding(0, 9, 0, 9) };
    private readonly Label _serviceStatus = new() { AutoSize = true, ForeColor = Color.FromArgb(80, 100, 88), Padding = new Padding(0, 7, 0, 7) };
    private const string ServiceName = "VerduleriaAugustScaleAgent";

    public SetupForm()
    {
        Text = "Configurador Balanza Augustu"; Width = 620; Height = 760; MinimumSize = new Size(560, 690);
        StartPosition = FormStartPosition.CenterScreen; BackColor = Color.FromArgb(255, 250, 240); Font = new Font("Segoe UI", 10);
        _mode.Items.AddRange(["Simulador (sin balanza)", "Balanza real por puerto COM"]); _mode.SelectedIndex = 0;
        _port.Items.AddRange(System.IO.Ports.SerialPort.GetPortNames()); if (_port.Items.Count == 0) _port.Items.Add("COM1"); _port.SelectedIndex = 0;
        var title = new Label { Text = "Augustu · Vincular balanza", Font = new Font("Segoe UI", 24, FontStyle.Bold), AutoSize = true, ForeColor = Color.FromArgb(37, 37, 34) };
        var subtitle = new Label { Text = "Configurá esta PC sin modificar archivos internos.", AutoSize = true, ForeColor = Color.FromArgb(110, 126, 116) };
        var logoPath = Path.Combine(AppContext.BaseDirectory, "augustu-logo.jpeg");
        var logo = new PictureBox { Width = 62, Height = 62, SizeMode = PictureBoxSizeMode.Zoom, Image = File.Exists(logoPath) ? Image.FromFile(logoPath) : null, Margin = new Padding(0, 0, 14, 6) };
        var form = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true, Padding = new Padding(32, 25, 32, 15) }; form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36)); form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
        form.Controls.Add(logo, 0, 0); form.Controls.Add(title, 1, 0); form.Controls.Add(subtitle, 0, 1); form.SetColumnSpan(subtitle, 2);
        AddRow(form, 2, "Dirección del servidor", _server); AddRow(form, 3, "Número de balanza", _scaleId); AddRow(form, 4, "Modo", _mode); AddRow(form, 5, "Puerto", _port); AddRow(form, 6, "Peso de prueba (kg)", _weight); AddRow(form, 7, "Clave de vinculación", _key);
        var test = Button("Probar conexión", Color.White, Color.FromArgb(165, 28, 32)); test.Click += async (_, _) => await TestAsync();
        var save = Button("Probar y guardar", Color.White, Color.FromArgb(165, 28, 32)); save.Click += async (_, _) => { if (await TestAsync()) Save(); };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight }; buttons.Controls.Add(test); buttons.Controls.Add(save);
        form.Controls.Add(buttons, 0, 8); form.SetColumnSpan(buttons, 2); form.Controls.Add(_status, 0, 9); form.SetColumnSpan(_status, 2);
        var serviceTitle = new Label { Text = "Inicio automático", Font = new Font("Segoe UI", 15, FontStyle.Bold), AutoSize = true, ForeColor = Color.FromArgb(35, 61, 46), Padding = new Padding(0, 14, 0, 2) }; form.Controls.Add(serviceTitle, 0, 10); form.SetColumnSpan(serviceTitle, 2);
        var serviceHelp = new Label { Text = "Instalá el agente como servicio para que funcione al encender la PC, sin abrir ninguna ventana.", AutoSize = true, ForeColor = Color.FromArgb(110, 126, 116) }; form.Controls.Add(serviceHelp, 0, 11); form.SetColumnSpan(serviceHelp, 2);
        var install = Button("Instalar e iniciar agente", Color.White, Color.FromArgb(165, 28, 32)); install.Click += async (_, _) => await InstallServiceAsync();
        var refresh = Button("Consultar estado", Color.FromArgb(142, 23, 27), Color.White); refresh.Click += async (_, _) => await RefreshServiceStatusAsync();
        var serviceButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true }; serviceButtons.Controls.Add(install); serviceButtons.Controls.Add(refresh); form.Controls.Add(serviceButtons, 0, 12); form.SetColumnSpan(serviceButtons, 2);
        form.Controls.Add(_serviceStatus, 0, 13); form.SetColumnSpan(_serviceStatus, 2); Controls.Add(form);
        Shown += async (_, _) => await RefreshServiceStatusAsync();
    }

    private async Task<bool> TestAsync()
    {
        try
        {
            ValidateFields(); _status.Text = "Probando comunicación…"; _status.ForeColor = Color.FromArgb(80, 100, 88);
            using var client = new HttpClient { BaseAddress = new Uri(NormalizeServer()), Timeout = TimeSpan.FromSeconds(8) };
            using var request = new HttpRequestMessage(HttpMethod.Post, $"api/Balanzas/{(int)_scaleId.Value}/lecturas"); request.Headers.Add("X-Agent-Key", _key.Text.Trim());
            request.Content = JsonContent.Create(new { lecturaId = Guid.NewGuid(), pesoKg = _weight.Value, estable = true, fechaLectura = DateTimeOffset.UtcNow });
            using var response = await client.SendAsync(request); if (!response.IsSuccessStatusCode) throw new InvalidOperationException(response.StatusCode == System.Net.HttpStatusCode.Unauthorized ? "La clave o el número de balanza no son correctos." : $"La API respondió {response.StatusCode}.");
            _status.Text = "✓ Conexión correcta. La API recibió el peso de prueba."; _status.ForeColor = Color.FromArgb(34, 128, 75); return true;
        }
        catch (Exception ex) { _status.Text = "No se pudo conectar: " + ex.Message; _status.ForeColor = Color.FromArgb(177, 70, 61); return false; }
    }

    private void Save()
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "VerduleriaAugust", "ScaleAgent"); Directory.CreateDirectory(folder);
            var keyPath = Path.Combine(folder, "agent.key"); var protectedKey = ProtectedData.Protect(Encoding.UTF8.GetBytes(_key.Text.Trim()), Entropy, DataProtectionScope.LocalMachine); File.WriteAllText(keyPath, Convert.ToBase64String(protectedKey));
            var config = new { Scale = new { Mode = _mode.SelectedIndex == 0 ? "Simulator" : "Serial", PortName = _port.Text.Trim(), BaudRate = 9600, PollIntervalMilliseconds = 500, ResponseTimeoutMilliseconds = 1000, InterByteTimeoutMilliseconds = 20, MaxRetryDelaySeconds = 30, SimulatorWeightKg = _weight.Value, SimulatorStable = true }, Api = new { BaseUrl = NormalizeServer(), ScaleId = (int)_scaleId.Value, ApiKeyProtectedFile = keyPath } };
            File.WriteAllText(Path.Combine(folder, "agent.settings.json"), JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
            _status.Text = "✓ Configuración guardada y clave protegida por Windows."; _key.Clear();
        }
        catch (UnauthorizedAccessException) { _status.Text = "Windows no permitió guardar. Abrí el configurador como administrador."; _status.ForeColor = Color.FromArgb(177, 70, 61); }
    }

    private async Task InstallServiceAsync()
    {
        try
        {
            var agentPath = FindAgentExecutable();
            if (agentPath is null)
            {
                using var picker = new OpenFileDialog { Title = "Seleccioná VerduleriaAugust.ScaleAgent.exe", Filter = "Agente August|VerduleriaAugust.ScaleAgent.exe" };
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                agentPath = picker.FileName;
            }

            _serviceStatus.Text = "Solicitando permisos de administrador…";
            var command = $"create {ServiceName} binPath= \"\\\"{agentPath}\\\"\" start= auto DisplayName= \"Augustu - Agente de balanza\"";
            var createResult = await RunScAsync(command, elevate: true);
            if (createResult != 0 && !await ServiceExistsAsync()) throw new InvalidOperationException("Windows no pudo instalar el servicio.");
            await RunScAsync($"description {ServiceName} \"Lee la balanza local y envía el peso a la API central.\"", elevate: true);
            var startResult = await RunScAsync($"start {ServiceName}", elevate: true);
            if (startResult != 0) await Task.Delay(800);
            await RefreshServiceStatusAsync();
        }
        catch (Exception ex) { _serviceStatus.Text = "No se pudo instalar: " + ex.Message; _serviceStatus.ForeColor = Color.FromArgb(177, 70, 61); }
    }

    private async Task RefreshServiceStatusAsync()
    {
        try
        {
            var result = await RunScCaptureAsync($"query {ServiceName}");
            if (result.ExitCode != 0) { _serviceStatus.Text = "○ Agente todavía no instalado"; _serviceStatus.ForeColor = Color.FromArgb(115, 126, 119); return; }
            var running = result.Output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase);
            _serviceStatus.Text = running ? "✓ Agente instalado y funcionando" : "● Agente instalado, pero detenido";
            _serviceStatus.ForeColor = running ? Color.FromArgb(34, 128, 75) : Color.FromArgb(177, 117, 40);
        }
        catch (Exception ex) { _serviceStatus.Text = "No se pudo consultar el servicio: " + ex.Message; }
    }

    private static string? FindAgentExecutable()
    {
        var besideSetup = Path.Combine(AppContext.BaseDirectory, "VerduleriaAugust.ScaleAgent.exe");
        if (File.Exists(besideSetup)) return besideSetup;
        var packagedAgent = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Agent", "VerduleriaAugust.ScaleAgent.exe"));
        if (File.Exists(packagedAgent)) return packagedAgent;
        var developmentBuild = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "VerduleriaAugust.ScaleAgent", "bin", "Debug", "net9.0", "VerduleriaAugust.ScaleAgent.exe"));
        return File.Exists(developmentBuild) ? developmentBuild : null;
    }

    private static async Task<bool> ServiceExistsAsync() => (await RunScCaptureAsync($"query {ServiceName}")).ExitCode == 0;
    private static async Task<int> RunScAsync(string arguments, bool elevate)
    {
        using var process = Process.Start(new ProcessStartInfo("sc.exe", arguments) { UseShellExecute = elevate, Verb = elevate ? "runas" : "", WindowStyle = ProcessWindowStyle.Hidden });
        if (process is null) throw new InvalidOperationException("No se pudo iniciar la herramienta de servicios de Windows.");
        await process.WaitForExitAsync(); return process.ExitCode;
    }
    private static async Task<(int ExitCode, string Output)> RunScCaptureAsync(string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo("sc.exe", arguments) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true });
        if (process is null) throw new InvalidOperationException("No se pudo consultar Windows.");
        var output = await process.StandardOutput.ReadToEndAsync(); await process.WaitForExitAsync(); return (process.ExitCode, output);
    }

    private void ValidateFields() { if (!Uri.TryCreate(NormalizeServer(), UriKind.Absolute, out _)) throw new InvalidOperationException("La dirección del servidor no es válida."); if (string.IsNullOrWhiteSpace(_key.Text)) throw new InvalidOperationException("Pegá la clave de vinculación."); if (string.IsNullOrWhiteSpace(_port.Text)) throw new InvalidOperationException("Elegí un puerto COM."); }
    private string NormalizeServer() => _server.Text.Trim().TrimEnd('/') + "/";
    private static TextBox Field(string value) => new() { Text = value, Dock = DockStyle.Fill };
    private static Button Button(string text, Color foreground, Color background) => new() { Text = text, AutoSize = true, Height = 40, Padding = new Padding(14, 5, 14, 5), FlatStyle = FlatStyle.Flat, ForeColor = foreground, BackColor = background };
    private static void AddRow(TableLayoutPanel panel, int row, string label, Control control) { panel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); panel.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 9, 0, 9) }, 0, row); panel.Controls.Add(control, 1, row); }
}

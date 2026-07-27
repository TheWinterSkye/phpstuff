using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using NAudio.Wave;
using NAudio.Dsp;

namespace NightVoice;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

public sealed class MainForm : Form
{
    private readonly ComboBox inputBox = new();
    private readonly ComboBox virtualOutBox = new();
    private readonly ComboBox monitorOutBox = new();
    private readonly ComboBox presetBox = new();
    private readonly CheckBox monitorToggle = new();
    private readonly Button startButton = new();
    private readonly Button stopButton = new();
    private readonly Button refreshButton = new();
    private readonly TrackBar mixSlider = Slider(0, 100, 100);
    private readonly TrackBar pitchSlider = Slider(-12, 12, 0);
    private readonly TrackBar driveSlider = Slider(0, 100, 0);
    private readonly TrackBar echoSlider = Slider(0, 100, 0);
    private readonly TrackBar chorusSlider = Slider(0, 100, 0);
    private readonly TrackBar reverbSlider = Slider(0, 100, 0);
    private readonly TrackBar gateSlider = Slider(-60, -10, -48);
    private readonly TrackBar bassSlider = Slider(-12, 12, 0);
    private readonly TrackBar trebleSlider = Slider(-12, 12, 0);
    private readonly ProgressBar inputMeter = new();
    private readonly ProgressBar outputMeter = new();
    private readonly Label statusLabel = new();
    private readonly Dictionary<TrackBar, Label> valueLabels = new();

    private WaveInEvent? capture;
    private WaveOutEvent? virtualOutput;
    private WaveOutEvent? monitorOutput;
    private BufferedWaveProvider? virtualBuffer;
    private BufferedWaveProvider? monitorBuffer;
    private VoiceProcessor? processor;
    private bool running;

    private static readonly Color Back = Color.FromArgb(14, 15, 20);
    private static readonly Color Panel = Color.FromArgb(24, 26, 34);
    private static readonly Color Panel2 = Color.FromArgb(32, 35, 46);
    private static readonly Color TextMain = Color.FromArgb(240, 241, 246);
    private static readonly Color TextMuted = Color.FromArgb(166, 171, 190);
    private static readonly Color Accent = Color.FromArgb(221, 74, 164);

    public MainForm()
    {
        Text = "NightVoice — Live Voice Mod";
        ClientSize = new Size(1080, 720);
        MinimumSize = new Size(940, 650);
        BackColor = Back;
        ForeColor = TextMain;
        Font = new Font("Segoe UI", 10f);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;

        BuildUi();
        LoadDevices();
        LoadPresets();
        ApplyPreset("Clean Studio");
        FormClosing += (_, _) => StopAudio();
    }

    private static TrackBar Slider(int min, int max, int value) => new()
    {
        Minimum = min,
        Maximum = max,
        Value = value,
        TickStyle = TickStyle.None,
        Height = 32,
        Dock = DockStyle.Fill
    };

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 2,
            RowCount = 3,
            BackColor = Back
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        Controls.Add(root);

        var header = new Panel { Dock = DockStyle.Fill, BackColor = Back };
        root.Controls.Add(header, 0, 0);
        root.SetColumnSpan(header, 2);

        var title = new Label
        {
            Text = "NIGHTVOICE",
            Font = new Font("Segoe UI Semibold", 24f, FontStyle.Bold),
            ForeColor = TextMain,
            AutoSize = true,
            Location = new Point(4, 3)
        };
        var subtitle = new Label
        {
            Text = "Real-time voice effects for Windows",
            ForeColor = TextMuted,
            AutoSize = true,
            Location = new Point(8, 52)
        };
        header.Controls.Add(title);
        header.Controls.Add(subtitle);

        var left = Card();
        left.Padding = new Padding(18);
        root.Controls.Add(left, 0, 1);
        var leftFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Panel
        };
        left.Controls.Add(leftFlow);

        AddSectionTitle(leftFlow, "AUDIO ROUTING");
        AddCombo(leftFlow, "Microphone input", inputBox);
        AddCombo(leftFlow, "Virtual microphone output", virtualOutBox);
        AddCombo(leftFlow, "Headphone monitor output", monitorOutBox);

        monitorToggle.Text = "Monitor my processed voice";
        monitorToggle.ForeColor = TextMain;
        monitorToggle.AutoSize = true;
        monitorToggle.Margin = new Padding(3, 12, 3, 10);
        monitorToggle.CheckedChanged += (_, _) => UpdateMonitorState();
        leftFlow.Controls.Add(monitorToggle);

        refreshButton.Text = "Refresh devices";
        StyleSecondaryButton(refreshButton);
        refreshButton.Click += (_, _) => LoadDevices();
        leftFlow.Controls.Add(refreshButton);

        AddSectionTitle(leftFlow, "VOICE PRESET");
        presetBox.Width = 270;
        presetBox.DropDownStyle = ComboBoxStyle.DropDownList;
        StyleCombo(presetBox);
        presetBox.SelectedIndexChanged += (_, _) =>
        {
            if (presetBox.SelectedItem is string name) ApplyPreset(name);
        };
        leftFlow.Controls.Add(presetBox);

        startButton.Text = "START VOICE MOD";
        startButton.Width = 270;
        startButton.Height = 46;
        startButton.Margin = new Padding(3, 20, 3, 6);
        StylePrimaryButton(startButton);
        startButton.Click += (_, _) => StartAudio();
        leftFlow.Controls.Add(startButton);

        stopButton.Text = "STOP";
        stopButton.Width = 270;
        stopButton.Height = 38;
        stopButton.Enabled = false;
        StyleSecondaryButton(stopButton);
        stopButton.Click += (_, _) => StopAudio();
        leftFlow.Controls.Add(stopButton);

        var right = Card();
        right.Padding = new Padding(20);
        root.Controls.Add(right, 1, 1);

        var effects = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            BackColor = Panel,
            Padding = new Padding(4)
        };
        effects.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        effects.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (int i = 0; i < 5; i++) effects.RowStyles.Add(new RowStyle(SizeType.Percent, 20));
        right.Controls.Add(effects);

        AddKnobPanel(effects, 0, 0, "Wet / Dry Mix", mixSlider, "%");
        AddKnobPanel(effects, 1, 0, "Pitch", pitchSlider, " st");
        AddKnobPanel(effects, 0, 1, "Drive", driveSlider, "%");
        AddKnobPanel(effects, 1, 1, "Noise Gate", gateSlider, " dB");
        AddKnobPanel(effects, 0, 2, "Echo", echoSlider, "%");
        AddKnobPanel(effects, 1, 2, "Chorus", chorusSlider, "%");
        AddKnobPanel(effects, 0, 3, "Reverb", reverbSlider, "%");
        AddKnobPanel(effects, 1, 3, "Bass", bassSlider, " dB");
        AddKnobPanel(effects, 0, 4, "Treble", trebleSlider, " dB");

        var meterPanel = new Panel { Dock = DockStyle.Fill, BackColor = Panel2, Padding = new Padding(16) };
        effects.Controls.Add(meterPanel, 1, 4);
        var meterLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Panel2 };
        meterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        meterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        meterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        meterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        meterPanel.Controls.Add(meterLayout);
        meterLayout.Controls.Add(MutedLabel("INPUT LEVEL"), 0, 0);
        inputMeter.Dock = DockStyle.Fill;
        meterLayout.Controls.Add(inputMeter, 0, 1);
        meterLayout.Controls.Add(MutedLabel("OUTPUT LEVEL"), 0, 2);
        outputMeter.Dock = DockStyle.Fill;
        meterLayout.Controls.Add(outputMeter, 0, 3);

        var footer = new Panel { Dock = DockStyle.Fill, BackColor = Back };
        root.Controls.Add(footer, 0, 2);
        root.SetColumnSpan(footer, 2);
        statusLabel.Text = "Ready — choose your microphone and virtual cable.";
        statusLabel.ForeColor = TextMuted;
        statusLabel.AutoSize = true;
        statusLabel.Location = new Point(5, 15);
        footer.Controls.Add(statusLabel);
    }

    private static Panel Card() => new() { Dock = DockStyle.Fill, BackColor = Panel, Margin = new Padding(0, 0, 14, 0) };

    private static Label MutedLabel(string text) => new()
    {
        Text = text,
        ForeColor = TextMuted,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold)
    };

    private static void AddSectionTitle(Control parent, string text)
    {
        parent.Controls.Add(new Label
        {
            Text = text,
            ForeColor = Accent,
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            Width = 275,
            Height = 30,
            Margin = new Padding(3, 8, 3, 2),
            TextAlign = ContentAlignment.BottomLeft
        });
    }

    private static void AddCombo(Control parent, string label, ComboBox box)
    {
        parent.Controls.Add(new Label { Text = label, ForeColor = TextMuted, Width = 275, Height = 24, Margin = new Padding(3, 8, 3, 0) });
        box.Width = 270;
        box.DropDownStyle = ComboBoxStyle.DropDownList;
        StyleCombo(box);
        parent.Controls.Add(box);
    }

    private static void StyleCombo(ComboBox box)
    {
        box.BackColor = Panel2;
        box.ForeColor = TextMain;
        box.FlatStyle = FlatStyle.Flat;
        box.Height = 34;
    }

    private static void StylePrimaryButton(Button button)
    {
        button.BackColor = Accent;
        button.ForeColor = Color.White;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
    }

    private static void StyleSecondaryButton(Button button)
    {
        button.BackColor = Panel2;
        button.ForeColor = TextMain;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Color.FromArgb(65, 69, 85);
        button.FlatAppearance.BorderSize = 1;
        button.Cursor = Cursors.Hand;
    }

    private void AddKnobPanel(TableLayoutPanel parent, int col, int row, string name, TrackBar slider, string suffix)
    {
        var box = new Panel { Dock = DockStyle.Fill, BackColor = Panel2, Margin = new Padding(6), Padding = new Padding(14) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Panel2 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        box.Controls.Add(layout);
        layout.Controls.Add(new Label { Text = name, Dock = DockStyle.Fill, ForeColor = TextMain, Font = new Font("Segoe UI Semibold", 10f) }, 0, 0);
        layout.Controls.Add(slider, 0, 1);
        var value = new Label { Text = slider.Value + suffix, Dock = DockStyle.Fill, ForeColor = Accent, TextAlign = ContentAlignment.MiddleRight };
        valueLabels[slider] = value;
        layout.Controls.Add(value, 0, 2);
        slider.ValueChanged += (_, _) =>
        {
            value.Text = slider.Value + suffix;
            UpdateProcessorSettings();
        };
        parent.Controls.Add(box, col, row);
    }

    private void LoadDevices()
    {
        string? inputName = inputBox.SelectedItem?.ToString();
        string? vName = virtualOutBox.SelectedItem?.ToString();
        string? mName = monitorOutBox.SelectedItem?.ToString();

        inputBox.Items.Clear();
        for (int i = 0; i < WaveIn.DeviceCount; i++) inputBox.Items.Add(WaveIn.GetCapabilities(i).ProductName);

        virtualOutBox.Items.Clear();
        monitorOutBox.Items.Clear();
        for (int i = 0; i < WaveOut.DeviceCount; i++)
        {
            string n = WaveOut.GetCapabilities(i).ProductName;
            virtualOutBox.Items.Add(n);
            monitorOutBox.Items.Add(n);
        }

        RestoreSelection(inputBox, inputName, 0);
        RestoreSelection(virtualOutBox, vName, FindCableIndex(virtualOutBox));
        RestoreSelection(monitorOutBox, mName, 0);
        statusLabel.Text = $"Found {inputBox.Items.Count} inputs and {virtualOutBox.Items.Count} outputs.";
    }

    private static void RestoreSelection(ComboBox box, string? name, int fallback)
    {
        if (name != null && box.Items.Contains(name)) box.SelectedItem = name;
        else if (box.Items.Count > 0) box.SelectedIndex = Math.Clamp(fallback, 0, box.Items.Count - 1);
    }

    private static int FindCableIndex(ComboBox box)
    {
        for (int i = 0; i < box.Items.Count; i++)
        {
            string n = box.Items[i]?.ToString()?.ToLowerInvariant() ?? "";
            if (n.Contains("cable") || n.Contains("virtual")) return i;
        }
        return 0;
    }

    private void LoadPresets()
    {
        presetBox.Items.AddRange(new object[]
        {
            "Clean Studio", "Warm Broadcast", "Bright Pop", "Deep Voice", "Soft Feminine",
            "Telephone", "Robot", "Android", "Demon", "Ghost", "Goblin", "Space Radio",
            "Dream Chorus", "Dark Whisper"
        });
        presetBox.SelectedIndex = 0;
    }

    private void ApplyPreset(string name)
    {
        var p = VoicePreset.Get(name);
        mixSlider.Value = p.Mix;
        pitchSlider.Value = p.Pitch;
        driveSlider.Value = p.Drive;
        echoSlider.Value = p.Echo;
        chorusSlider.Value = p.Chorus;
        reverbSlider.Value = p.Reverb;
        gateSlider.Value = p.Gate;
        bassSlider.Value = p.Bass;
        trebleSlider.Value = p.Treble;
        UpdateProcessorSettings();
        statusLabel.Text = $"Preset loaded: {name}";
    }

    private void StartAudio()
    {
        if (running) return;
        if (inputBox.SelectedIndex < 0 || virtualOutBox.SelectedIndex < 0)
        {
            MessageBox.Show("Choose a microphone and output device first.", "NightVoice", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            var format = new WaveFormat(48000, 16, 1);
            processor = new VoiceProcessor(48000);
            UpdateProcessorSettings();

            virtualBuffer = new BufferedWaveProvider(format)
            {
                BufferDuration = TimeSpan.FromMilliseconds(400),
                DiscardOnBufferOverflow = true
            };
            virtualOutput = new WaveOutEvent { DeviceNumber = virtualOutBox.SelectedIndex, DesiredLatency = 90, NumberOfBuffers = 3 };
            virtualOutput.Init(virtualBuffer);
            virtualOutput.Play();

            capture = new WaveInEvent
            {
                DeviceNumber = inputBox.SelectedIndex,
                WaveFormat = format,
                BufferMilliseconds = 20,
                NumberOfBuffers = 3
            };
            capture.DataAvailable += CaptureOnDataAvailable;
            capture.RecordingStopped += (_, e) =>
            {
                if (e.Exception != null) BeginInvoke(() => statusLabel.Text = "Audio stopped: " + e.Exception.Message);
            };
            capture.StartRecording();

            running = true;
            startButton.Enabled = false;
            stopButton.Enabled = true;
            inputBox.Enabled = virtualOutBox.Enabled = false;
            statusLabel.Text = "LIVE — processed voice is being sent to the selected output.";
            UpdateMonitorState();
        }
        catch (Exception ex)
        {
            StopAudio();
            MessageBox.Show("NightVoice could not start audio.\n\n" + ex.Message, "Audio error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void CaptureOnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (processor == null || virtualBuffer == null) return;
        byte[] processed = new byte[e.BytesRecorded];
        Buffer.BlockCopy(e.Buffer, 0, processed, 0, e.BytesRecorded);
        var samples = new short[e.BytesRecorded / 2];
        Buffer.BlockCopy(processed, 0, samples, 0, e.BytesRecorded);

        float inPeak = 0f;
        for (int i = 0; i < samples.Length; i++) inPeak = Math.Max(inPeak, Math.Abs(samples[i] / 32768f));
        float outPeak = processor.Process(samples);
        Buffer.BlockCopy(samples, 0, processed, 0, e.BytesRecorded);

        virtualBuffer.AddSamples(processed, 0, processed.Length);
        if (monitorToggle.Checked && monitorBuffer != null) monitorBuffer.AddSamples(processed, 0, processed.Length);

        BeginInvoke(() =>
        {
            inputMeter.Value = Math.Clamp((int)(inPeak * 100), 0, 100);
            outputMeter.Value = Math.Clamp((int)(outPeak * 100), 0, 100);
        });
    }

    private void UpdateMonitorState()
    {
        if (!running) return;
        try
        {
            monitorOutput?.Stop();
            monitorOutput?.Dispose();
            monitorOutput = null;
            monitorBuffer = null;

            if (monitorToggle.Checked)
            {
                if (monitorOutBox.SelectedIndex < 0) return;
                var format = new WaveFormat(48000, 16, 1);
                monitorBuffer = new BufferedWaveProvider(format)
                {
                    BufferDuration = TimeSpan.FromMilliseconds(400),
                    DiscardOnBufferOverflow = true
                };
                monitorOutput = new WaveOutEvent { DeviceNumber = monitorOutBox.SelectedIndex, DesiredLatency = 80, NumberOfBuffers = 3 };
                monitorOutput.Init(monitorBuffer);
                monitorOutput.Play();
                statusLabel.Text = "LIVE — voice monitoring is ON. Headphones are strongly recommended.";
            }
            else statusLabel.Text = "LIVE — voice monitoring is OFF.";
        }
        catch (Exception ex)
        {
            monitorToggle.Checked = false;
            statusLabel.Text = "Monitor could not start: " + ex.Message;
        }
    }

    private void StopAudio()
    {
        running = false;
        try { capture?.StopRecording(); } catch { }
        try { virtualOutput?.Stop(); } catch { }
        try { monitorOutput?.Stop(); } catch { }
        capture?.Dispose();
        virtualOutput?.Dispose();
        monitorOutput?.Dispose();
        capture = null;
        virtualOutput = null;
        monitorOutput = null;
        virtualBuffer = null;
        monitorBuffer = null;
        processor = null;

        startButton.Enabled = true;
        stopButton.Enabled = false;
        inputBox.Enabled = virtualOutBox.Enabled = true;
        inputMeter.Value = outputMeter.Value = 0;
        statusLabel.Text = "Stopped.";
    }

    private void UpdateProcessorSettings()
    {
        processor?.Configure(new VoiceSettings
        {
            Mix = mixSlider.Value / 100f,
            PitchSemitones = pitchSlider.Value,
            Drive = driveSlider.Value / 100f,
            Echo = echoSlider.Value / 100f,
            Chorus = chorusSlider.Value / 100f,
            Reverb = reverbSlider.Value / 100f,
            GateDb = gateSlider.Value,
            BassDb = bassSlider.Value,
            TrebleDb = trebleSlider.Value,
            Mode = presetBox.SelectedItem?.ToString() ?? "Clean Studio"
        });
    }
}

public sealed class VoiceSettings
{
    public float Mix { get; set; } = 1;
    public int PitchSemitones { get; set; }
    public float Drive { get; set; }
    public float Echo { get; set; }
    public float Chorus { get; set; }
    public float Reverb { get; set; }
    public float GateDb { get; set; } = -48;
    public float BassDb { get; set; }
    public float TrebleDb { get; set; }
    public string Mode { get; set; } = "Clean Studio";
}

public sealed record VoicePreset(int Mix, int Pitch, int Drive, int Echo, int Chorus, int Reverb, int Gate, int Bass, int Treble)
{
    public static VoicePreset Get(string name) => name switch
    {
        "Warm Broadcast" => new(100, 0, 12, 0, 0, 4, -46, 5, -2),
        "Bright Pop" => new(100, 1, 4, 0, 8, 8, -50, -2, 6),
        "Deep Voice" => new(100, -4, 10, 4, 3, 10, -46, 7, -4),
        "Soft Feminine" => new(100, 3, 3, 0, 8, 9, -50, -3, 5),
        "Telephone" => new(100, 0, 18, 2, 0, 0, -42, -12, 10),
        "Robot" => new(100, 0, 28, 5, 0, 0, -45, -4, 5),
        "Android" => new(100, -1, 18, 3, 18, 3, -46, 0, 6),
        "Demon" => new(100, -7, 38, 18, 9, 30, -43, 8, -7),
        "Ghost" => new(100, 4, 5, 35, 30, 50, -52, -5, 4),
        "Goblin" => new(100, 7, 24, 7, 7, 7, -45, -8, 8),
        "Space Radio" => new(100, -1, 22, 20, 12, 12, -44, -6, 9),
        "Dream Chorus" => new(85, 2, 2, 16, 62, 38, -52, 0, 4),
        "Dark Whisper" => new(100, -3, 9, 20, 24, 42, -56, 4, -6),
        _ => new(100, 0, 0, 0, 0, 2, -50, 0, 0)
    };
}

public sealed class VoiceProcessor
{
    private readonly int sampleRate;
    private VoiceSettings settings = new();
    private BiQuadFilter lowShelf;
    private BiQuadFilter highShelf;
    private BiQuadFilter telephoneHighPass;
    private BiQuadFilter telephoneLowPass;
    private readonly float[] echoBuffer;
    private readonly float[] reverbA;
    private readonly float[] reverbB;
    private readonly float[] chorusBuffer;
    private readonly float[] pitchBuffer;
    private int echoPos, revAPos, revBPos, chorusPos, pitchWrite;
    private double lfoPhase, robotPhase, pitchRead;
    private readonly Random random = new();

    public VoiceProcessor(int sampleRate)
    {
        this.sampleRate = sampleRate;
        lowShelf = BiQuadFilter.LowShelf(sampleRate, 180, 0.8f, 0);
        highShelf = BiQuadFilter.HighShelf(sampleRate, 3500, 0.8f, 0);
        telephoneHighPass = BiQuadFilter.HighPassFilter(sampleRate, 320, 0.8f);
        telephoneLowPass = BiQuadFilter.LowPassFilter(sampleRate, 3200, 0.8f);
        echoBuffer = new float[sampleRate * 2];
        reverbA = new float[(int)(sampleRate * 0.083)];
        reverbB = new float[(int)(sampleRate * 0.127)];
        chorusBuffer = new float[(int)(sampleRate * 0.08)];
        pitchBuffer = new float[(int)(sampleRate * 0.12)];
        pitchRead = pitchBuffer.Length / 2.0;
    }

    public void Configure(VoiceSettings newSettings)
    {
        settings = newSettings;
        lowShelf = BiQuadFilter.LowShelf(sampleRate, 180, 0.8f, settings.BassDb);
        highShelf = BiQuadFilter.HighShelf(sampleRate, 3500, 0.8f, settings.TrebleDb);
    }

    public float Process(short[] samples)
    {
        float peak = 0f;
        float gateLinear = MathF.Pow(10f, settings.GateDb / 20f);
        float pitchRatio = MathF.Pow(2f, settings.PitchSemitones / 12f);

        for (int i = 0; i < samples.Length; i++)
        {
            float dry = samples[i] / 32768f;
            float x = Math.Abs(dry) < gateLinear ? 0f : dry;
            x = lowShelf.Transform(x);
            x = highShelf.Transform(x);

            if (settings.PitchSemitones != 0) x = PitchShift(x, pitchRatio);

            string mode = settings.Mode;
            if (mode == "Telephone" || mode == "Space Radio")
            {
                x = telephoneHighPass.Transform(x);
                x = telephoneLowPass.Transform(x);
            }
            if (mode == "Robot")
            {
                robotPhase += 2 * Math.PI * 72 / sampleRate;
                if (robotPhase > 2 * Math.PI) robotPhase -= 2 * Math.PI;
                x *= (float)Math.Sin(robotPhase);
            }
            else if (mode == "Android")
            {
                robotPhase += 2 * Math.PI * 34 / sampleRate;
                if (robotPhase > 2 * Math.PI) robotPhase -= 2 * Math.PI;
                x = x * 0.78f + x * (float)Math.Sin(robotPhase) * 0.32f;
            }
            else if (mode == "Dark Whisper")
            {
                float noise = ((float)random.NextDouble() * 2f - 1f) * Math.Min(0.12f, Math.Abs(x) * 1.7f);
                x = x * 0.52f + noise;
            }

            if (settings.Drive > 0)
            {
                float gain = 1f + settings.Drive * 11f;
                x = MathF.Tanh(x * gain) / MathF.Tanh(gain * 0.72f);
            }

            if (settings.Chorus > 0) x = ApplyChorus(x, settings.Chorus);
            if (settings.Echo > 0) x = ApplyEcho(x, settings.Echo);
            if (settings.Reverb > 0) x = ApplyReverb(x, settings.Reverb);

            x = dry * (1f - settings.Mix) + x * settings.Mix;
            x = MathF.Tanh(x * 1.08f) * 0.92f;
            peak = Math.Max(peak, Math.Abs(x));
            samples[i] = (short)Math.Clamp((int)(x * 32767f), short.MinValue, short.MaxValue);
        }
        return peak;
    }

    private float ApplyEcho(float x, float amount)
    {
        int delay = (int)(sampleRate * (0.13 + amount * 0.27));
        int read = (echoPos - delay + echoBuffer.Length) % echoBuffer.Length;
        float delayed = echoBuffer[read];
        echoBuffer[echoPos] = x + delayed * (0.18f + amount * 0.46f);
        echoPos = (echoPos + 1) % echoBuffer.Length;
        return x + delayed * amount * 0.55f;
    }

    private float ApplyReverb(float x, float amount)
    {
        float a = reverbA[revAPos];
        float b = reverbB[revBPos];
        reverbA[revAPos] = x + a * 0.73f;
        reverbB[revBPos] = x + b * 0.69f + a * 0.12f;
        revAPos = (revAPos + 1) % reverbA.Length;
        revBPos = (revBPos + 1) % reverbB.Length;
        return x + (a + b) * amount * 0.24f;
    }

    private float ApplyChorus(float x, float amount)
    {
        chorusBuffer[chorusPos] = x;
        lfoPhase += 2 * Math.PI * 0.34 / sampleRate;
        if (lfoPhase > 2 * Math.PI) lfoPhase -= 2 * Math.PI;
        double delay = sampleRate * (0.012 + 0.006 * Math.Sin(lfoPhase));
        double rp = chorusPos - delay;
        while (rp < 0) rp += chorusBuffer.Length;
        int i0 = (int)rp;
        int i1 = (i0 + 1) % chorusBuffer.Length;
        float frac = (float)(rp - i0);
        float delayed = chorusBuffer[i0] * (1 - frac) + chorusBuffer[i1] * frac;
        chorusPos = (chorusPos + 1) % chorusBuffer.Length;
        return x + delayed * amount * 0.48f;
    }

    private float PitchShift(float x, float ratio)
    {
        pitchBuffer[pitchWrite] = x;
        int size = pitchBuffer.Length;
        pitchRead += ratio;
        while (pitchRead >= size) pitchRead -= size;
        int distance = (pitchWrite - (int)pitchRead + size) % size;
        if (distance < sampleRate * 0.01 || distance > size - sampleRate * 0.01)
            pitchRead = (pitchWrite - size / 2 + size) % size;
        int i0 = (int)pitchRead;
        int i1 = (i0 + 1) % size;
        float frac = (float)(pitchRead - i0);
        float y = pitchBuffer[i0] * (1 - frac) + pitchBuffer[i1] * frac;
        pitchWrite = (pitchWrite + 1) % size;
        return y;
    }
}

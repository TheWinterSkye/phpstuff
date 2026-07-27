using System;
using System.Buffers;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using NAudio.Wave;

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
    private readonly ComboBox qualityBox = new();
    private readonly CheckBox monitorToggle = new();
    private readonly CheckBox preserveToggle = new();
    private readonly Button startButton = new();
    private readonly Button stopButton = new();
    private readonly Button refreshButton = new();

    private readonly TrackBar mixSlider = Slider(0, 100, 100);
    private readonly TrackBar pitchSlider = Slider(-12, 12, 0);
    private readonly TrackBar formantSlider = Slider(-8, 8, 0);
    private readonly TrackBar driveSlider = Slider(0, 100, 0);
    private readonly TrackBar echoSlider = Slider(0, 100, 0);
    private readonly TrackBar chorusSlider = Slider(0, 100, 0);
    private readonly TrackBar reverbSlider = Slider(0, 100, 2);
    private readonly TrackBar gateSlider = Slider(-65, -10, -56);
    private readonly TrackBar bassSlider = Slider(-12, 12, 0);
    private readonly TrackBar trebleSlider = Slider(-12, 12, 1);

    private readonly ProgressBar inputMeter = new();
    private readonly ProgressBar outputMeter = new();
    private readonly Label statusLabel = new();
    private readonly Label latencyLabel = new();
    private readonly Dictionary<TrackBar, Label> valueLabels = new();

    private WaveInEvent? capture;
    private WaveOutEvent? virtualOutput;
    private WaveOutEvent? monitorOutput;
    private BufferedWaveProvider? virtualBuffer;
    private BufferedWaveProvider? monitorBuffer;
    private VoiceProcessor? processor;
    private bool running;
    private volatile bool monitorEnabled;
    private long lastMeterUpdate;

    private static readonly Color Back = Color.FromArgb(14, 15, 20);
    private static readonly Color Panel = Color.FromArgb(24, 26, 34);
    private static readonly Color Panel2 = Color.FromArgb(32, 35, 46);
    private static readonly Color TextMain = Color.FromArgb(240, 241, 246);
    private static readonly Color TextMuted = Color.FromArgb(166, 171, 190);
    private static readonly Color Accent = Color.FromArgb(221, 74, 164);

    public MainForm()
    {
        Text = "NightVoice 2 — Natural Pitch Engine";
        ClientSize = new Size(1120, 780);
        MinimumSize = new Size(980, 700);
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
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 345));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        Controls.Add(root);

        var header = new Panel { Dock = DockStyle.Fill, BackColor = Back };
        root.Controls.Add(header, 0, 0);
        root.SetColumnSpan(header, 2);
        header.Controls.Add(new Label
        {
            Text = "NIGHTVOICE 2",
            Font = new Font("Segoe UI Semibold", 24f, FontStyle.Bold),
            ForeColor = TextMain,
            AutoSize = true,
            Location = new Point(4, 3)
        });
        header.Controls.Add(new Label
        {
            Text = "Higher-quality pitch shifting with independent formant control",
            ForeColor = TextMuted,
            AutoSize = true,
            Location = new Point(8, 52)
        });

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
        monitorToggle.Margin = new Padding(3, 12, 3, 8);
        monitorToggle.CheckedChanged += (_, _) =>
        {
            monitorEnabled = monitorToggle.Checked;
            UpdateMonitorState();
        };
        leftFlow.Controls.Add(monitorToggle);

        refreshButton.Text = "Refresh devices";
        refreshButton.Width = 285;
        StyleSecondaryButton(refreshButton);
        refreshButton.Click += (_, _) => LoadDevices();
        leftFlow.Controls.Add(refreshButton);

        AddSectionTitle(leftFlow, "VOICE ENGINE");
        AddCombo(leftFlow, "Pitch quality", qualityBox);
        qualityBox.Items.AddRange(new object[]
        {
            "Low latency",
            "Balanced — recommended",
            "Studio — smoothest"
        });
        qualityBox.SelectedIndex = 1;
        qualityBox.SelectedIndexChanged += (_, _) => UpdateProcessorSettings();

        preserveToggle.Text = "Preserve natural tone while shifting";
        preserveToggle.ForeColor = TextMain;
        preserveToggle.Checked = true;
        preserveToggle.AutoSize = true;
        preserveToggle.Margin = new Padding(3, 10, 3, 7);
        preserveToggle.CheckedChanged += (_, _) => UpdateProcessorSettings();
        leftFlow.Controls.Add(preserveToggle);

        AddSectionTitle(leftFlow, "VOICE PRESET");
        presetBox.Width = 285;
        presetBox.DropDownStyle = ComboBoxStyle.DropDownList;
        StyleCombo(presetBox);
        presetBox.SelectedIndexChanged += (_, _) =>
        {
            if (presetBox.SelectedItem is string name) ApplyPreset(name);
        };
        leftFlow.Controls.Add(presetBox);

        startButton.Text = "START VOICE MOD";
        startButton.Width = 285;
        startButton.Height = 46;
        startButton.Margin = new Padding(3, 18, 3, 6);
        StylePrimaryButton(startButton);
        startButton.Click += (_, _) => StartAudio();
        leftFlow.Controls.Add(startButton);

        stopButton.Text = "STOP";
        stopButton.Width = 285;
        stopButton.Height = 38;
        stopButton.Enabled = false;
        StyleSecondaryButton(stopButton);
        stopButton.Click += (_, _) => StopAudio();
        leftFlow.Controls.Add(stopButton);

        var right = Card();
        right.Padding = new Padding(16);
        root.Controls.Add(right, 1, 1);

        var effects = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 6,
            BackColor = Panel,
            Padding = new Padding(4)
        };
        effects.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        effects.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (int i = 0; i < 6; i++) effects.RowStyles.Add(new RowStyle(SizeType.Percent, 16.6667f));
        right.Controls.Add(effects);

        AddKnobPanel(effects, 0, 0, "Wet / Dry Mix", mixSlider, "%");
        AddKnobPanel(effects, 1, 0, "Pitch", pitchSlider, " st");
        AddKnobPanel(effects, 0, 1, "Formant", formantSlider, " st");
        AddKnobPanel(effects, 1, 1, "Drive", driveSlider, "%");
        AddKnobPanel(effects, 0, 2, "Noise Gate", gateSlider, " dB");
        AddKnobPanel(effects, 1, 2, "Bass", bassSlider, " dB");
        AddKnobPanel(effects, 0, 3, "Treble", trebleSlider, " dB");
        AddKnobPanel(effects, 1, 3, "Chorus", chorusSlider, "%");
        AddKnobPanel(effects, 0, 4, "Echo", echoSlider, "%");
        AddKnobPanel(effects, 1, 4, "Reverb", reverbSlider, "%");

        var tips = new Panel { Dock = DockStyle.Fill, BackColor = Panel2, Margin = new Padding(6), Padding = new Padding(14) };
        tips.Controls.Add(new Label
        {
            Text = "NATURAL VOICE TIP\nUse small pitch changes. Move formant separately to change apparent vocal-tract size without forcing pitch too far.",
            Dock = DockStyle.Fill,
            ForeColor = TextMuted,
            Font = new Font("Segoe UI", 9f),
            TextAlign = ContentAlignment.MiddleLeft
        });
        effects.Controls.Add(tips, 0, 5);

        var meterPanel = new Panel { Dock = DockStyle.Fill, BackColor = Panel2, Margin = new Padding(6), Padding = new Padding(14) };
        effects.Controls.Add(meterPanel, 1, 5);
        var meterLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, BackColor = Panel2 };
        meterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        meterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 16));
        meterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        meterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 16));
        meterLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        meterPanel.Controls.Add(meterLayout);
        meterLayout.Controls.Add(MutedLabel("INPUT LEVEL"), 0, 0);
        inputMeter.Dock = DockStyle.Fill;
        meterLayout.Controls.Add(inputMeter, 0, 1);
        meterLayout.Controls.Add(MutedLabel("OUTPUT LEVEL"), 0, 2);
        outputMeter.Dock = DockStyle.Fill;
        meterLayout.Controls.Add(outputMeter, 0, 3);
        latencyLabel.Text = "Engine latency: —";
        latencyLabel.ForeColor = TextMuted;
        latencyLabel.Dock = DockStyle.Fill;
        latencyLabel.TextAlign = ContentAlignment.BottomLeft;
        meterLayout.Controls.Add(latencyLabel, 0, 4);

        var footer = new Panel { Dock = DockStyle.Fill, BackColor = Back };
        root.Controls.Add(footer, 0, 2);
        root.SetColumnSpan(footer, 2);
        statusLabel.Text = "Ready — choose your microphone and virtual cable.";
        statusLabel.ForeColor = TextMuted;
        statusLabel.AutoSize = true;
        statusLabel.Location = new Point(5, 17);
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
            Width = 290,
            Height = 30,
            Margin = new Padding(3, 8, 3, 2),
            TextAlign = ContentAlignment.BottomLeft
        });
    }

    private static void AddCombo(Control parent, string label, ComboBox box)
    {
        parent.Controls.Add(new Label { Text = label, ForeColor = TextMuted, Width = 290, Height = 24, Margin = new Padding(3, 8, 3, 0) });
        box.Width = 285;
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
        var box = new Panel { Dock = DockStyle.Fill, BackColor = Panel2, Margin = new Padding(6), Padding = new Padding(12) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Panel2 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
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
        string? virtualName = virtualOutBox.SelectedItem?.ToString();
        string? monitorName = monitorOutBox.SelectedItem?.ToString();

        inputBox.Items.Clear();
        for (int i = 0; i < WaveIn.DeviceCount; i++) inputBox.Items.Add(WaveIn.GetCapabilities(i).ProductName);

        virtualOutBox.Items.Clear();
        monitorOutBox.Items.Clear();
        for (int i = 0; i < WaveOut.DeviceCount; i++)
        {
            string name = WaveOut.GetCapabilities(i).ProductName;
            virtualOutBox.Items.Add(name);
            monitorOutBox.Items.Add(name);
        }

        RestoreSelection(inputBox, inputName, 0);
        RestoreSelection(virtualOutBox, virtualName, FindCableIndex(virtualOutBox));
        RestoreSelection(monitorOutBox, monitorName, 0);
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
            string name = box.Items[i]?.ToString()?.ToLowerInvariant() ?? string.Empty;
            if (name.Contains("cable") || name.Contains("virtual")) return i;
        }
        return 0;
    }

    private void LoadPresets()
    {
        presetBox.Items.AddRange(new object[]
        {
            "Clean Studio", "Warm Broadcast", "Bright Natural", "Natural Higher", "Natural Lower",
            "Soft Feminine", "Deep Voice", "Telephone", "Robot", "Android", "Demon", "Ghost",
            "Goblin", "Space Radio", "Dream Chorus", "Dark Whisper"
        });
        presetBox.SelectedIndex = 0;
    }

    private void ApplyPreset(string name)
    {
        VoicePreset preset = VoicePreset.Get(name);
        mixSlider.Value = preset.Mix;
        pitchSlider.Value = preset.Pitch;
        formantSlider.Value = preset.Formant;
        preserveToggle.Checked = preset.Preserve;
        qualityBox.SelectedIndex = preset.Quality;
        driveSlider.Value = preset.Drive;
        echoSlider.Value = preset.Echo;
        chorusSlider.Value = preset.Chorus;
        reverbSlider.Value = preset.Reverb;
        gateSlider.Value = preset.Gate;
        bassSlider.Value = preset.Bass;
        trebleSlider.Value = preset.Treble;
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
            processor = new VoiceProcessor(48000, Math.Max(0, qualityBox.SelectedIndex));
            UpdateProcessorSettings();

            virtualBuffer = new BufferedWaveProvider(format)
            {
                BufferDuration = TimeSpan.FromMilliseconds(900),
                DiscardOnBufferOverflow = true
            };
            virtualOutput = new WaveOutEvent { DeviceNumber = virtualOutBox.SelectedIndex, DesiredLatency = 75, NumberOfBuffers = 3 };
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
                if (e.Exception != null) BeginInvoke((Action)(() => statusLabel.Text = "Audio stopped: " + e.Exception.Message));
            };
            capture.StartRecording();

            running = true;
            startButton.Enabled = false;
            stopButton.Enabled = true;
            inputBox.Enabled = virtualOutBox.Enabled = false;
            latencyLabel.Text = $"Engine latency: about {processor.LatencyMilliseconds} ms";
            statusLabel.Text = "LIVE — processed voice is being sent to the selected output.";
            UpdateMonitorState();
        }
        catch (DllNotFoundException)
        {
            StopAudio();
            MessageBox.Show("NightPitch.dll is missing. Keep it in the same extracted folder as NightVoice.exe.", "Pitch engine missing", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            StopAudio();
            MessageBox.Show("NightVoice could not start audio.\n\n" + ex.Message, "Audio error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void CaptureOnDataAvailable(object? sender, WaveInEventArgs e)
    {
        VoiceProcessor? currentProcessor = processor;
        BufferedWaveProvider? currentVirtualBuffer = virtualBuffer;
        if (currentProcessor == null || currentVirtualBuffer == null) return;

        int sampleCount = e.BytesRecorded / 2;
        short[] samples = ArrayPool<short>.Shared.Rent(sampleCount);
        byte[] processed = ArrayPool<byte>.Shared.Rent(e.BytesRecorded);
        try
        {
            Buffer.BlockCopy(e.Buffer, 0, samples, 0, e.BytesRecorded);
            float inputPeak = 0f;
            for (int i = 0; i < sampleCount; i++) inputPeak = Math.Max(inputPeak, Math.Abs(samples[i] / 32768f));

            float outputPeak = currentProcessor.Process(samples, sampleCount);
            Buffer.BlockCopy(samples, 0, processed, 0, e.BytesRecorded);
            currentVirtualBuffer.AddSamples(processed, 0, e.BytesRecorded);

            BufferedWaveProvider? currentMonitor = monitorBuffer;
            if (monitorEnabled && currentMonitor != null) currentMonitor.AddSamples(processed, 0, e.BytesRecorded);

            long now = Environment.TickCount64;
            if (now - lastMeterUpdate >= 50)
            {
                lastMeterUpdate = now;
                BeginInvoke((Action)(() =>
                {
                    inputMeter.Value = Math.Clamp((int)(inputPeak * 100), 0, 100);
                    outputMeter.Value = Math.Clamp((int)(outputPeak * 100), 0, 100);
                }));
            }
        }
        catch (Exception ex)
        {
            BeginInvoke((Action)(() => statusLabel.Text = "Processing error: " + ex.Message));
        }
        finally
        {
            ArrayPool<short>.Shared.Return(samples);
            ArrayPool<byte>.Shared.Return(processed);
        }
    }

    private void UpdateMonitorState()
    {
        monitorEnabled = monitorToggle.Checked;
        if (!running) return;
        try
        {
            monitorOutput?.Stop();
            monitorOutput?.Dispose();
            monitorOutput = null;
            monitorBuffer = null;

            if (monitorEnabled)
            {
                if (monitorOutBox.SelectedIndex < 0) return;
                var format = new WaveFormat(48000, 16, 1);
                monitorBuffer = new BufferedWaveProvider(format)
                {
                    BufferDuration = TimeSpan.FromMilliseconds(900),
                    DiscardOnBufferOverflow = true
                };
                monitorOutput = new WaveOutEvent { DeviceNumber = monitorOutBox.SelectedIndex, DesiredLatency = 70, NumberOfBuffers = 3 };
                monitorOutput.Init(monitorBuffer);
                monitorOutput.Play();
                statusLabel.Text = "LIVE — monitoring is ON. Use headphones to prevent feedback.";
            }
            else
            {
                statusLabel.Text = "LIVE — monitoring is OFF.";
            }
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
        monitorEnabled = false;
        try { capture?.StopRecording(); } catch { }
        try { virtualOutput?.Stop(); } catch { }
        try { monitorOutput?.Stop(); } catch { }
        capture?.Dispose();
        virtualOutput?.Dispose();
        monitorOutput?.Dispose();
        processor?.Dispose();
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
        latencyLabel.Text = "Engine latency: —";
        statusLabel.Text = "Stopped.";
    }

    private void UpdateProcessorSettings()
    {
        VoiceProcessor? current = processor;
        if (current == null) return;
        try
        {
            current.Configure(new VoiceSettings
            {
                Mix = mixSlider.Value / 100f,
                PitchSemitones = pitchSlider.Value,
                FormantSemitones = formantSlider.Value,
                PreserveFormants = preserveToggle.Checked,
                Quality = Math.Max(0, qualityBox.SelectedIndex),
                Drive = driveSlider.Value / 100f,
                Echo = echoSlider.Value / 100f,
                Chorus = chorusSlider.Value / 100f,
                Reverb = reverbSlider.Value / 100f,
                GateDb = gateSlider.Value,
                BassDb = bassSlider.Value,
                TrebleDb = trebleSlider.Value,
                Mode = presetBox.SelectedItem?.ToString() ?? "Clean Studio"
            });
            latencyLabel.Text = $"Engine latency: about {current.LatencyMilliseconds} ms";
        }
        catch (Exception ex)
        {
            statusLabel.Text = "Could not update effect: " + ex.Message;
        }
    }
}

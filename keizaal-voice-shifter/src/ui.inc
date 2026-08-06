#include <fstream>
#include <sstream>

class VoiceApp {
public:
    explicit VoiceApp(HINSTANCE instance) : instance_(instance) {}

    int Run() {
        INITCOMMONCONTROLSEX icc{sizeof(icc), ICC_STANDARD_CLASSES | ICC_BAR_CLASSES | ICC_PROGRESS_CLASS};
        InitCommonControlsEx(&icc);

        WNDCLASSEXW wc{sizeof(wc)};
        wc.style = CS_HREDRAW | CS_VREDRAW;
        wc.lpfnWndProc = &VoiceApp::StaticWndProc;
        wc.hInstance = instance_;
        wc.hCursor = LoadCursorW(nullptr, IDC_ARROW);
        wc.hbrBackground = CreateSolidBrush(BG);
        wc.lpszClassName = L"KeizaalVoiceShifterWindow";
        wc.hIcon = LoadIconW(nullptr, IDI_APPLICATION);
        wc.hIconSm = LoadIconW(nullptr, IDI_APPLICATION);
        if (!RegisterClassExW(&wc)) return 1;

        hwnd_ = CreateWindowExW(
            0, wc.lpszClassName, L"Keizaal Voice Shifter",
            WS_OVERLAPPEDWINDOW | WS_CLIPCHILDREN,
            CW_USEDEFAULT, CW_USEDEFAULT, 1180, 900,
            nullptr, nullptr, instance_, this);
        if (!hwnd_) return 2;

        ShowWindow(hwnd_, SW_SHOW);
        UpdateWindow(hwnd_);

        MSG msg{};
        while (GetMessageW(&msg, nullptr, 0, 0) > 0) {
            TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }
        return static_cast<int>(msg.wParam);
    }

private:
    enum Id : int {
        ID_REFRESH = 100, ID_START, ID_STOP,
        ID_INPUT, ID_VIRTUAL, ID_MONITOR, ID_QUALITY, ID_PRESET,
        ID_MONITOR_CHECK, ID_BYPASS, ID_FORMANT_COMP, ID_GATE_ENABLE,
        ID_SAVE, ID_LOAD,
        ID_PITCH, ID_FINE, ID_FORMANT, ID_FORMANT_BASE, ID_TONALITY,
        ID_MIX, ID_CONSONANT,
        ID_INPUT_GAIN, ID_GATE, ID_HIGHPASS, ID_WARMTH, ID_PRESENCE,
        ID_AIR, ID_DEESS, ID_COMP, ID_OUTPUT_GAIN,
        ID_IN_METER, ID_OUT_METER,
        ID_STATUS, ID_ENGINE_INFO,
        ID_FIRST_VALUE = 1000
    };

    struct SliderInfo {
        int id{};
        HWND slider{};
        HWND value{};
        double divisor{1};
        std::wstring suffix;
        int decimals{};
    };

    static LRESULT CALLBACK StaticWndProc(HWND h, UINT m, WPARAM w, LPARAM l) {
        VoiceApp* self = nullptr;
        if (m == WM_NCCREATE) {
            auto* cs = reinterpret_cast<CREATESTRUCTW*>(l);
            self = static_cast<VoiceApp*>(cs->lpCreateParams);
            self->hwnd_ = h;
            SetWindowLongPtrW(h, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(self));
        } else {
            self = reinterpret_cast<VoiceApp*>(GetWindowLongPtrW(h, GWLP_USERDATA));
        }
        return self ? self->WndProc(m, w, l) : DefWindowProcW(h, m, w, l);
    }

    LRESULT WndProc(UINT msg, WPARAM wp, LPARAM lp) {
        switch (msg) {
        case WM_CREATE:
            BuildUi();
            RefreshDevices();
            SetTimer(hwnd_, 1, 60, nullptr);
            return 0;
        case WM_COMMAND:
            OnCommand(LOWORD(wp), HIWORD(wp));
            return 0;
        case WM_HSCROLL:
            UpdateAllValues();
            PushSettings();
            return 0;
        case WM_TIMER:
            UpdateMeters();
            return 0;
        case WM_SIZE:
            InvalidateRect(hwnd_, nullptr, FALSE);
            return 0;
        case WM_GETMINMAXINFO: {
            auto* mm = reinterpret_cast<MINMAXINFO*>(lp);
            mm->ptMinTrackSize = {1080, 760};
            return 0;
        }
        case WM_CTLCOLORSTATIC: {
            HDC dc = reinterpret_cast<HDC>(wp);
            HWND control = reinterpret_cast<HWND>(lp);
            auto it = labelColors_.find(control);
            SetTextColor(dc, it == labelColors_.end() ? TEXT : it->second);
            SetBkColor(dc, BG);
            SetBkMode(dc, TRANSPARENT);
            return reinterpret_cast<LRESULT>(bgBrush_);
        }
        case WM_CTLCOLORBTN: {
            HDC dc = reinterpret_cast<HDC>(wp);
            SetTextColor(dc, TEXT);
            SetBkColor(dc, BG);
            SetBkMode(dc, TRANSPARENT);
            return reinterpret_cast<LRESULT>(bgBrush_);
        }
        case WM_CTLCOLOREDIT:
        case WM_CTLCOLORLISTBOX: {
            HDC dc = reinterpret_cast<HDC>(wp);
            SetTextColor(dc, RGB(18, 22, 29));
            SetBkColor(dc, RGB(245, 247, 250));
            return reinterpret_cast<LRESULT>(whiteBrush_);
        }
        case WM_PAINT:
            Paint();
            return 0;
        case WM_CLOSE:
            engine_.stop();
            DestroyWindow(hwnd_);
            return 0;
        case WM_DESTROY:
            KillTimer(hwnd_, 1);
            if (titleFont_) DeleteObject(titleFont_);
            if (headingFont_) DeleteObject(headingFont_);
            if (normalFont_) DeleteObject(normalFont_);
            if (smallFont_) DeleteObject(smallFont_);
            if (bgBrush_) DeleteObject(bgBrush_);
            if (whiteBrush_) DeleteObject(whiteBrush_);
            PostQuitMessage(0);
            return 0;
        }
        return DefWindowProcW(hwnd_, msg, wp, lp);
    }

    void BuildUi() {
        bgBrush_ = CreateSolidBrush(BG);
        whiteBrush_ = CreateSolidBrush(RGB(245, 247, 250));
        titleFont_ = MakeFont(25, FW_SEMIBOLD);
        headingFont_ = MakeFont(14, FW_BOLD);
        normalFont_ = MakeFont(14, FW_NORMAL);
        smallFont_ = MakeFont(12, FW_NORMAL);

        Label(L"KEIZAAL VOICE SHIFTER", 22, 15, 420, 34, titleFont_, TEXT);
        Label(L"High-quality real-time pitch + formant control for Skyrim roleplay", 22, 49, 560, 22, normalFont_, RGB(185, 198, 218));
        Button(L"Refresh Devices", ID_REFRESH, 785, 24, 130, 38);
        Button(L"Start Voice", ID_START, 925, 24, 108, 38);
        Button(L"Stop", ID_STOP, 1043, 24, 88, 38);
        EnableWindow(GetDlgItem(hwnd_, ID_STOP), FALSE);

        Heading(L"AUDIO ROUTING", 38, 94);
        Label(L"Voice input", 38, 126, 280, 20, headingFont_, TEXT);
        Combo(ID_INPUT, 38, 148, 330, 300);
        Label(L"Processed virtual output", 390, 126, 330, 20, headingFont_, TEXT);
        Combo(ID_VIRTUAL, 390, 148, 330, 300);
        Label(L"Headphone monitor output", 742, 126, 350, 20, headingFont_, TEXT);
        Combo(ID_MONITOR, 742, 148, 350, 300);
        Label(L"Choose your microphone or a recording-side virtual cable.", 38, 180, 330, 34, smallFont_, MUTED);
        Label(L"Choose CABLE Input / CABLE In 16ch — this is where the app sends audio.", 390, 180, 330, 34, smallFont_, MUTED);
        Label(L"Select headphones. Monitoring is muted until you enable the toggle below.", 742, 180, 350, 34, smallFont_, MUTED);

        Check(L"Hear processed voice in headphones", ID_MONITOR_CHECK, 38, 220, 295, 24, false);
        Check(L"Bypass processing", ID_BYPASS, 345, 220, 165, 24, false);
        Check(L"Automatic pitch/formant compensation", ID_FORMANT_COMP, 520, 220, 300, 24, true);
        Check(L"Enable noise gate", ID_GATE_ENABLE, 832, 220, 185, 24, true);

        Label(L"Quality", 38, 259, 80, 20, headingFont_, TEXT);
        Combo(ID_QUALITY, 118, 254, 180, 150);
        SendDlgItemMessageW(hwnd_, ID_QUALITY, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(L"Low Latency"));
        SendDlgItemMessageW(hwnd_, ID_QUALITY, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(L"Balanced"));
        SendDlgItemMessageW(hwnd_, ID_QUALITY, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(L"Best Voice"));
        SendDlgItemMessageW(hwnd_, ID_QUALITY, CB_SETCURSEL, 1, 0);
        Label(L"Preset", 320, 259, 80, 20, headingFont_, TEXT);
        Combo(ID_PRESET, 384, 254, 220, 220);
        for (auto* p : {L"Natural Clean", L"Noble Lift", L"Deep Guard", L"Ancient Priestess", L"Daedric Whisper"})
            SendDlgItemMessageW(hwnd_, ID_PRESET, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(p));
        SendDlgItemMessageW(hwnd_, ID_PRESET, CB_SETCURSEL, 0, 0);
        Button(L"Apply Preset", ID_PRESET + 50, 614, 253, 110, 30);
        Button(L"Save Custom", ID_SAVE, 734, 253, 110, 30);
        Button(L"Load Custom", ID_LOAD, 854, 253, 110, 30);

        Heading(L"VOICE SHAPE", 38, 311);
        AddSlider(L"Pitch shift", ID_PITCH, 38, 340, 480, -12, 12, 0, 1, L" semitones", 0);
        AddSlider(L"Fine pitch", ID_FINE, 38, 385, 480, -100, 100, 0, 1, L" cents", 0);
        AddSlider(L"Formant shift", ID_FORMANT, 38, 430, 480, -120, 120, 0, 10, L" semitones", 1);
        AddSlider(L"Formant base", ID_FORMANT_BASE, 38, 475, 480, 80, 300, 180, 1, L" Hz", 0);
        AddSlider(L"Tonality limit", ID_TONALITY, 38, 520, 480, 3000, 12000, 8000, 1, L" Hz", 0);
        AddSlider(L"Character mix", ID_MIX, 38, 565, 480, 0, 100, 100, 1, L"%", 0);
        AddSlider(L"Consonant protection", ID_CONSONANT, 38, 610, 480, 0, 100, 70, 1, L"%", 0);

        Heading(L"VOICE CLEANUP + TONE", 595, 311);
        AddSlider(L"Input gain", ID_INPUT_GAIN, 595, 340, 500, -180, 180, 0, 10, L" dB", 1);
        AddSlider(L"Gate threshold", ID_GATE, 595, 385, 500, -800, -300, -520, 10, L" dB", 1);
        AddSlider(L"High-pass filter", ID_HIGHPASS, 595, 430, 500, 40, 180, 75, 1, L" Hz", 0);
        AddSlider(L"Warmth", ID_WARMTH, 595, 475, 500, -120, 120, 0, 10, L" dB", 1);
        AddSlider(L"Presence", ID_PRESENCE, 595, 520, 500, -120, 120, 0, 10, L" dB", 1);
        AddSlider(L"Air", ID_AIR, 595, 565, 500, -120, 120, 0, 10, L" dB", 1);
        AddSlider(L"De-esser", ID_DEESS, 595, 610, 500, 0, 100, 25, 1, L"%", 0);
        AddSlider(L"Compression", ID_COMP, 595, 655, 500, 0, 100, 30, 1, L"%", 0);
        AddSlider(L"Output gain", ID_OUTPUT_GAIN, 595, 700, 500, -180, 120, 0, 10, L" dB", 1);

        Heading(L"SIGNAL", 38, 674);
        Label(L"Input", 38, 702, 70, 20, smallFont_, MUTED);
        Meter(ID_IN_METER, 92, 702, 400, 18);
        Label(L"Output", 38, 730, 70, 20, smallFont_, MUTED);
        Meter(ID_OUT_METER, 92, 730, 400, 18);

        Label(L"Stopped — choose devices, then press Start Voice.", 38, 774, 1050, 24, normalFont_, TEXT, ID_STATUS);
        Label(L"Route: app → CABLE Input / In 16ch; Skyrim microphone → matching CABLE Output / Out 16ch.", 38, 801, 1050, 22, smallFont_, MUTED, ID_ENGINE_INFO);

        UpdateAllValues();
    }

    HFONT MakeFont(int size, int weight) {
        HDC dc = GetDC(hwnd_);
        int h = -MulDiv(size, GetDeviceCaps(dc, LOGPIXELSY), 72);
        ReleaseDC(hwnd_, dc);
        return CreateFontW(h, 0, 0, 0, weight, FALSE, FALSE, FALSE, DEFAULT_CHARSET,
            OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY,
            DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI");
    }

    HWND Label(const wchar_t* text, int x, int y, int w, int h, HFONT font, COLORREF color, int id = 0) {
        HWND c = CreateWindowExW(0, L"STATIC", text, WS_CHILD | WS_VISIBLE | SS_LEFT,
            x, y, w, h, hwnd_, reinterpret_cast<HMENU>(static_cast<INT_PTR>(id)), instance_, nullptr);
        SendMessageW(c, WM_SETFONT, reinterpret_cast<WPARAM>(font), TRUE);
        labelColors_[c] = color;
        return c;
    }

    void Heading(const wchar_t* text, int x, int y) {
        Label(text, x, y, 430, 22, headingFont_, ACCENT);
    }

    HWND Button(const wchar_t* text, int id, int x, int y, int w, int h) {
        HWND c = CreateWindowExW(0, L"BUTTON", text, WS_CHILD | WS_VISIBLE | WS_TABSTOP | BS_PUSHBUTTON,
            x, y, w, h, hwnd_, reinterpret_cast<HMENU>(static_cast<INT_PTR>(id)), instance_, nullptr);
        SendMessageW(c, WM_SETFONT, reinterpret_cast<WPARAM>(normalFont_), TRUE);
        return c;
    }

    HWND Check(const wchar_t* text, int id, int x, int y, int w, int h, bool checked) {
        HWND c = CreateWindowExW(0, L"BUTTON", text, WS_CHILD | WS_VISIBLE | WS_TABSTOP | BS_AUTOCHECKBOX,
            x, y, w, h, hwnd_, reinterpret_cast<HMENU>(static_cast<INT_PTR>(id)), instance_, nullptr);
        SendMessageW(c, WM_SETFONT, reinterpret_cast<WPARAM>(normalFont_), TRUE);
        SendMessageW(c, BM_SETCHECK, checked ? BST_CHECKED : BST_UNCHECKED, 0);
        return c;
    }

    HWND Combo(int id, int x, int y, int w, int dropH) {
        HWND c = CreateWindowExW(WS_EX_CLIENTEDGE, L"COMBOBOX", nullptr,
            WS_CHILD | WS_VISIBLE | WS_TABSTOP | CBS_DROPDOWNLIST | WS_VSCROLL,
            x, y, w, dropH, hwnd_, reinterpret_cast<HMENU>(static_cast<INT_PTR>(id)), instance_, nullptr);
        SendMessageW(c, WM_SETFONT, reinterpret_cast<WPARAM>(normalFont_), TRUE);
        return c;
    }

    void Meter(int id, int x, int y, int w, int h) {
        HWND c = CreateWindowExW(0, PROGRESS_CLASSW, nullptr, WS_CHILD | WS_VISIBLE | PBS_SMOOTH,
            x, y, w, h, hwnd_, reinterpret_cast<HMENU>(static_cast<INT_PTR>(id)), instance_, nullptr);
        SendMessageW(c, PBM_SETRANGE, 0, MAKELPARAM(0, 1000));
        SendMessageW(c, PBM_SETBARCOLOR, 0, GOOD);
        SendMessageW(c, PBM_SETBKCOLOR, 0, INNER);
    }

    void AddSlider(const wchar_t* label, int id, int x, int y, int w,
                   int minV, int maxV, int value, double divisor,
                   const wchar_t* suffix, int decimals) {
        Label(label, x, y, 170, 20, normalFont_, TEXT);
        HWND val = Label(L"", x + w - 110, y, 110, 20, smallFont_, MUTED, ID_FIRST_VALUE + id);
        HWND s = CreateWindowExW(0, TRACKBAR_CLASSW, nullptr,
            WS_CHILD | WS_VISIBLE | WS_TABSTOP | TBS_HORZ | TBS_NOTICKS,
            x + 174, y - 4, w - 292, 30, hwnd_, reinterpret_cast<HMENU>(static_cast<INT_PTR>(id)), instance_, nullptr);
        SendMessageW(s, TBM_SETRANGEMIN, FALSE, minV);
        SendMessageW(s, TBM_SETRANGEMAX, FALSE, maxV);
        SendMessageW(s, TBM_SETPOS, TRUE, value);
        sliders_.push_back({id, s, val, divisor, suffix, decimals});
    }

    void Paint() {
        PAINTSTRUCT ps{};
        HDC dc = BeginPaint(hwnd_, &ps);
        RECT r{}; GetClientRect(hwnd_, &r);
        HBRUSH bg = CreateSolidBrush(BG);
        FillRect(dc, &r, bg);
        DeleteObject(bg);

        HBRUSH header = CreateSolidBrush(RGB(18, 23, 32));
        RECT hr{0, 0, r.right, 82}; FillRect(dc, &hr, header); DeleteObject(header);
        HPEN line = CreatePen(PS_SOLID, 1, RGB(49, 60, 78));
        HGDIOBJ oldPen = SelectObject(dc, line);
        MoveToEx(dc, 0, 82, nullptr); LineTo(dc, r.right, 82);
        SelectObject(dc, oldPen); DeleteObject(line);

        DrawCard(dc, 20, 88, r.right - 20, 294);
        DrawCard(dc, 20, 304, 560, 655);
        DrawCard(dc, 577, 304, r.right - 20, 752);
        DrawCard(dc, 20, 668, 560, 762);
        DrawCard(dc, 20, 768, r.right - 20, 835);
        EndPaint(hwnd_, &ps);
    }

    static void DrawCard(HDC dc, int l, int t, int r, int b) {
        HBRUSH brush = CreateSolidBrush(RGB(24, 31, 43));
        HPEN pen = CreatePen(PS_SOLID, 1, RGB(49, 60, 78));
        HGDIOBJ ob = SelectObject(dc, brush), op = SelectObject(dc, pen);
        RoundRect(dc, l, t, r, b, 14, 14);
        SelectObject(dc, ob); SelectObject(dc, op);
        DeleteObject(brush); DeleteObject(pen);
    }

    void OnCommand(int id, int code) {
        if (id == ID_REFRESH && code == BN_CLICKED) RefreshDevices();
        else if (id == ID_START && code == BN_CLICKED) StartAudio();
        else if (id == ID_STOP && code == BN_CLICKED) StopAudio();
        else if (id == ID_PRESET + 50 && code == BN_CLICKED) ApplySelectedPreset();
        else if (id == ID_SAVE && code == BN_CLICKED) SaveCustom();
        else if (id == ID_LOAD && code == BN_CLICKED) LoadCustom();
        else if ((id == ID_MONITOR_CHECK || id == ID_BYPASS || id == ID_FORMANT_COMP || id == ID_GATE_ENABLE) && code == BN_CLICKED) PushSettings();
        else if ((id == ID_INPUT || id == ID_VIRTUAL || id == ID_MONITOR || id == ID_QUALITY) && code == CBN_SELCHANGE) PushSettings();
    }

    void RefreshDevices() {
        if (engine_.running()) return;
        SetStatus(L"Refreshing Windows audio devices…", false);
        if (!engine_.refresh()) { SetStatus(engine_.error(), true); return; }

        HWND in = GetDlgItem(hwnd_, ID_INPUT), out = GetDlgItem(hwnd_, ID_VIRTUAL), mon = GetDlgItem(hwnd_, ID_MONITOR);
        SendMessageW(in, CB_RESETCONTENT, 0, 0);
        SendMessageW(out, CB_RESETCONTENT, 0, 0);
        SendMessageW(mon, CB_RESETCONTENT, 0, 0);
        int none = static_cast<int>(SendMessageW(mon, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(L"No monitor device")));
        SendMessageW(mon, CB_SETITEMDATA, none, static_cast<LPARAM>(-1));

        int inputBest = -1;
        for (size_t i = 0; i < engine_.captures().size(); ++i) {
            const auto& d = engine_.captures()[i];
            int row = static_cast<int>(SendMessageW(in, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(d.name.c_str())));
            SendMessageW(in, CB_SETITEMDATA, row, static_cast<LPARAM>(i));
            if (inputBest < 0 && (containsI(d.name, L"yeti") || d.isDefault)) inputBest = row;
        }

        int virtualBest = -1, monitorBest = none;
        for (size_t i = 0; i < engine_.playbacks().size(); ++i) {
            const auto& d = engine_.playbacks()[i];
            int ro = static_cast<int>(SendMessageW(out, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(d.name.c_str())));
            SendMessageW(out, CB_SETITEMDATA, ro, static_cast<LPARAM>(i));
            int rm = static_cast<int>(SendMessageW(mon, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(d.name.c_str())));
            SendMessageW(mon, CB_SETITEMDATA, rm, static_cast<LPARAM>(i));

            if (containsI(d.name, L"CABLE In 16ch")) virtualBest = ro;
            else if (virtualBest < 0 && containsI(d.name, L"CABLE Input")) virtualBest = ro;
            else if (virtualBest < 0 && containsI(d.name, L"CABLE")) virtualBest = ro;

            if (monitorBest == none && !containsI(d.name, L"CABLE") && (containsI(d.name, L"headphone") || containsI(d.name, L"yeti") || d.isDefault))
                monitorBest = rm;
        }

        if (inputBest < 0 && SendMessageW(in, CB_GETCOUNT, 0, 0) > 0) inputBest = 0;
        if (virtualBest < 0 && SendMessageW(out, CB_GETCOUNT, 0, 0) > 0) virtualBest = 0;
        SendMessageW(in, CB_SETCURSEL, inputBest, 0);
        SendMessageW(out, CB_SETCURSEL, virtualBest, 0);
        SendMessageW(mon, CB_SETCURSEL, monitorBest, 0);
        SetStatus(L"Devices ready. Confirm the virtual output, then press Start Voice.", false);
    }

    std::optional<size_t> SelectedIndex(int comboId, bool allowNone = false) const {
        HWND c = GetDlgItem(hwnd_, comboId);
        LRESULT row = SendMessageW(c, CB_GETCURSEL, 0, 0);
        if (row == CB_ERR) return std::nullopt;
        LRESULT data = SendMessageW(c, CB_GETITEMDATA, row, 0);
        if (data == CB_ERR || (allowNone && data < 0)) return std::nullopt;
        return static_cast<size_t>(data);
    }

    void StartAudio() {
        auto ci = SelectedIndex(ID_INPUT), vi = SelectedIndex(ID_VIRTUAL), mi = SelectedIndex(ID_MONITOR, true);
        if (!ci || !vi) { SetStatus(L"Select both a voice input and a processed virtual output.", true); return; }
        PushSettings();
        int quality = static_cast<int>(SendDlgItemMessageW(hwnd_, ID_QUALITY, CB_GETCURSEL, 0, 0));
        if (quality < 0) quality = 1;
        if (!engine_.start(*ci, *vi, mi, quality)) { SetStatus(engine_.error(), true); return; }

        EnableRunningUi(true);
        SetStatus(L"Voice processing is running. Skyrim should listen to the matching CABLE Output / Out 16ch recording device.", false);
    }

    void StopAudio() {
        engine_.stop();
        EnableRunningUi(false);
        SetStatus(L"Stopped safely.", false);
    }

    void EnableRunningUi(bool running) {
        EnableWindow(GetDlgItem(hwnd_, ID_START), !running);
        EnableWindow(GetDlgItem(hwnd_, ID_STOP), running);
        EnableWindow(GetDlgItem(hwnd_, ID_REFRESH), !running);
        for (int id : {ID_INPUT, ID_VIRTUAL, ID_MONITOR, ID_QUALITY}) EnableWindow(GetDlgItem(hwnd_, id), !running);
    }

    VoiceSettings ReadSettings() const {
        VoiceSettings s;
        s.pitch = static_cast<float>(Pos(ID_PITCH));
        s.fine = static_cast<float>(Pos(ID_FINE));
        s.formant = static_cast<float>(Pos(ID_FORMANT)) / 10.0f;
        s.formantBase = static_cast<float>(Pos(ID_FORMANT_BASE));
        s.tonality = static_cast<float>(Pos(ID_TONALITY));
        s.mix = static_cast<float>(Pos(ID_MIX)) / 100.0f;
        s.consonant = static_cast<float>(Pos(ID_CONSONANT)) / 100.0f;
        s.inputGain = static_cast<float>(Pos(ID_INPUT_GAIN)) / 10.0f;
        s.gate = static_cast<float>(Pos(ID_GATE)) / 10.0f;
        s.highpass = static_cast<float>(Pos(ID_HIGHPASS));
        s.warmth = static_cast<float>(Pos(ID_WARMTH)) / 10.0f;
        s.presence = static_cast<float>(Pos(ID_PRESENCE)) / 10.0f;
        s.air = static_cast<float>(Pos(ID_AIR)) / 10.0f;
        s.deess = static_cast<float>(Pos(ID_DEESS)) / 100.0f;
        s.comp = static_cast<float>(Pos(ID_COMP)) / 100.0f;
        s.outputGain = static_cast<float>(Pos(ID_OUTPUT_GAIN)) / 10.0f;
        s.monitor = IsChecked(ID_MONITOR_CHECK);
        s.bypass = IsChecked(ID_BYPASS);
        s.formantComp = IsChecked(ID_FORMANT_COMP);
        s.gateEnabled = IsChecked(ID_GATE_ENABLE);
        return s;
    }

    void PushSettings() { engine_.settings(ReadSettings()); }
    int Pos(int id) const { return static_cast<int>(SendDlgItemMessageW(hwnd_, id, TBM_GETPOS, 0, 0)); }
    bool IsChecked(int id) const { return SendDlgItemMessageW(hwnd_, id, BM_GETCHECK, 0, 0) == BST_CHECKED; }
    void SetPos(int id, int value) { SendDlgItemMessageW(hwnd_, id, TBM_SETPOS, TRUE, value); }
    void SetCheck(int id, bool value) { SendDlgItemMessageW(hwnd_, id, BM_SETCHECK, value ? BST_CHECKED : BST_UNCHECKED, 0); }

    void UpdateAllValues() {
        for (const auto& s : sliders_) {
            double value = static_cast<double>(SendMessageW(s.slider, TBM_GETPOS, 0, 0)) / s.divisor;
            std::wostringstream os;
            os.setf(std::ios::fixed);
            os.precision(s.decimals);
            os << value << s.suffix;
            SetWindowTextW(s.value, os.str().c_str());
        }
    }

    void ApplySelectedPreset() {
        int p = static_cast<int>(SendDlgItemMessageW(hwnd_, ID_PRESET, CB_GETCURSEL, 0, 0));
        SetPos(ID_PITCH, 0); SetPos(ID_FINE, 0); SetPos(ID_FORMANT, 0); SetPos(ID_FORMANT_BASE, 180);
        SetPos(ID_TONALITY, 8000); SetPos(ID_MIX, 100); SetPos(ID_CONSONANT, 70);
        SetPos(ID_INPUT_GAIN, 0); SetPos(ID_GATE, -520); SetPos(ID_HIGHPASS, 75);
        SetPos(ID_WARMTH, 0); SetPos(ID_PRESENCE, 0); SetPos(ID_AIR, 0);
        SetPos(ID_DEESS, 25); SetPos(ID_COMP, 30); SetPos(ID_OUTPUT_GAIN, 0);
        SetCheck(ID_FORMANT_COMP, true); SetCheck(ID_GATE_ENABLE, true);
        switch (p) {
        case 1: SetPos(ID_PITCH, 2); SetPos(ID_FORMANT, 15); SetPos(ID_WARMTH, 10); SetPos(ID_PRESENCE, 8); SetPos(ID_AIR, 5); SetPos(ID_CONSONANT, 78); break;
        case 2: SetPos(ID_PITCH, -3); SetPos(ID_FORMANT, -20); SetPos(ID_FORMANT_BASE, 135); SetPos(ID_WARMTH, 22); SetPos(ID_PRESENCE, -4); SetPos(ID_AIR, -8); SetPos(ID_CONSONANT, 75); break;
        case 3: SetPos(ID_PITCH, 1); SetPos(ID_FORMANT, 22); SetPos(ID_FORMANT_BASE, 205); SetPos(ID_WARMTH, 10); SetPos(ID_PRESENCE, 10); SetPos(ID_AIR, 10); SetPos(ID_CONSONANT, 82); break;
        case 4: SetPos(ID_PITCH, -6); SetPos(ID_FORMANT, -38); SetPos(ID_FORMANT_BASE, 115); SetPos(ID_MIX, 90); SetPos(ID_CONSONANT, 88); SetPos(ID_WARMTH, -8); SetPos(ID_PRESENCE, 18); SetPos(ID_AIR, -10); SetPos(ID_COMP, 45); break;
        default: break;
        }
        UpdateAllValues(); PushSettings();
        SetStatus(L"Preset applied. Speak normally and fine-tune pitch, formant, and consonant protection.", false);
    }

    std::filesystem::path ConfigPath() const {
        wchar_t path[MAX_PATH]{};
        if (SUCCEEDED(SHGetFolderPathW(nullptr, CSIDL_APPDATA, nullptr, SHGFP_TYPE_CURRENT, path))) {
            std::filesystem::path dir = std::filesystem::path(path) / L"KeizaalVoiceShifter";
            std::error_code ec; std::filesystem::create_directories(dir, ec);
            return dir / L"custom.ini";
        }
        return L"custom.ini";
    }

    void SaveCustom() {
        std::wofstream f(ConfigPath());
        if (!f) { SetStatus(L"Could not save the custom preset.", true); return; }
        for (const auto& s : sliders_) f << s.id << L"=" << SendMessageW(s.slider, TBM_GETPOS, 0, 0) << L"\n";
        f << ID_FORMANT_COMP << L"=" << IsChecked(ID_FORMANT_COMP) << L"\n";
        f << ID_GATE_ENABLE << L"=" << IsChecked(ID_GATE_ENABLE) << L"\n";
        SetStatus(L"Custom preset saved in your Windows AppData folder.", false);
    }

    void LoadCustom() {
        std::wifstream f(ConfigPath());
        if (!f) { SetStatus(L"No custom preset has been saved yet.", true); return; }
        std::wstring line;
        while (std::getline(f, line)) {
            auto eq = line.find(L'='); if (eq == std::wstring::npos) continue;
            try {
                int id = std::stoi(line.substr(0, eq)); int value = std::stoi(line.substr(eq + 1));
                if (id == ID_FORMANT_COMP || id == ID_GATE_ENABLE) SetCheck(id, value != 0);
                else SetPos(id, value);
            } catch (...) {}
        }
        UpdateAllValues(); PushSettings();
        SetStatus(L"Custom preset loaded.", false);
    }

    void UpdateMeters() {
        auto meter = [&](int id, float peak) {
            float db = ampdb(peak);
            int value = static_cast<int>(clampf((db + 60.0f) / 60.0f, 0, 1) * 1000);
            SendDlgItemMessageW(hwnd_, id, PBM_SETPOS, value, 0);
        };
        meter(ID_IN_METER, engine_.inPeak()); meter(ID_OUT_METER, engine_.outPeak());
        if (engine_.running()) {
            int ms = static_cast<int>(std::lround(engine_.latencyFrames() * 1000.0 / SAMPLE_RATE));
            std::wostringstream os;
            os << L"Estimated DSP/buffer latency: " << ms << L" ms   |   underruns: " << engine_.underruns()
               << L"   |   overflows: " << engine_.overflows();
            SetWindowTextW(GetDlgItem(hwnd_, ID_ENGINE_INFO), os.str().c_str());
        }
    }

    void SetStatus(const std::wstring& text, bool error) {
        HWND c = GetDlgItem(hwnd_, ID_STATUS);
        SetWindowTextW(c, text.c_str());
        labelColors_[c] = error ? RGB(255, 132, 132) : TEXT;
        InvalidateRect(c, nullptr, TRUE);
    }

    HINSTANCE instance_{};
    HWND hwnd_{};
    HFONT titleFont_{}, headingFont_{}, normalFont_{}, smallFont_{};
    HBRUSH bgBrush_{}, whiteBrush_{};
    std::unordered_map<HWND, COLORREF> labelColors_;
    std::vector<SliderInfo> sliders_;
    AudioEngine engine_;
};

} // namespace

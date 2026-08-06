#include <fstream>
#include <sstream>

#include "common.inc"
#include "audio.inc"
#include "ui.inc"

int WINAPI wWinMain(HINSTANCE instance, HINSTANCE, PWSTR, int) {
    VoiceApp app(instance);
    return app.Run();
}

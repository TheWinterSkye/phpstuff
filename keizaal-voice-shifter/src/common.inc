#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <commctrl.h>
#include <uxtheme.h>
#include <shlobj.h>

#define MA_NO_DECODING
#define MA_NO_ENCODING
#define MA_NO_RESOURCE_MANAGER
#define MA_NO_NODE_GRAPH
#define MA_ENABLE_ONLY_SPECIFIC_BACKENDS
#define MA_ENABLE_WASAPI
#define MINIAUDIO_IMPLEMENTATION
#include "miniaudio.h"
#include "signalsmith-stretch.h"

#include <algorithm>
#include <array>
#include <atomic>
#include <chrono>
#include <cmath>
#include <cstdint>
#include <cstring>
#include <cwctype>
#include <filesystem>
#include <mutex>
#include <numbers>
#include <optional>
#include <string>
#include <thread>
#include <unordered_map>
#include <unordered_set>
#include <vector>

namespace {
constexpr int SAMPLE_RATE = 48000;
constexpr int PROCESS_FRAMES = 256;
constexpr COLORREF BG = RGB(13,17,24);
constexpr COLORREF TEXT = RGB(239,244,252);
constexpr COLORREF MUTED = RGB(166,179,198);
constexpr COLORREF ACCENT = RGB(233,80,187);
constexpr COLORREF INNER = RGB(31,40,55);
constexpr COLORREF GOOD = RGB(70,201,155);

inline float clampf(float v,float lo,float hi){ return std::max(lo,std::min(hi,v)); }
inline float dbamp(float db){ return std::pow(10.0f,db/20.0f); }
inline float ampdb(float a){ return 20.0f*std::log10(std::max(a,0.000001f)); }

inline std::wstring widen(const char* s){
    if(!s||!*s) return L"";
    int n=MultiByteToWideChar(CP_UTF8,0,s,-1,nullptr,0);
    std::wstring out(static_cast<size_t>(std::max(n,0)),L'\0');
    if(n>1) MultiByteToWideChar(CP_UTF8,0,s,-1,out.data(),n);
    if(!out.empty() && out.back()==L'\0') out.pop_back();
    return out;
}
inline std::wstring lower(std::wstring s){ for(auto& c:s)c=static_cast<wchar_t>(std::towlower(c)); return s; }
inline bool containsI(const std::wstring& s,const std::wstring& n){ return lower(s).find(lower(n))!=std::wstring::npos; }

struct VoiceSettings{
    float pitch=0, fine=0, formant=0, formantBase=180, tonality=8000, mix=1, consonant=0.70f;
    float inputGain=0, gate=-52, highpass=75, warmth=0, presence=0, air=0, deess=0.25f, comp=0.30f, outputGain=0;
    bool formantComp=true, gateEnabled=true, monitor=false, bypass=false;
};

struct DeviceEntry{ ma_device_id id{}; std::wstring name; bool isDefault=false; };

class SpscRing{
public:
    explicit SpscRing(size_t cap=1u<<19):data_(cap),cap_(cap){}
    void reset(){r_.store(0);w_.store(0);std::fill(data_.begin(),data_.end(),0);}
    size_t available()const{return static_cast<size_t>(w_.load(std::memory_order_acquire)-r_.load(std::memory_order_acquire));}
    size_t push(const float* p,size_t n){
        auto w=w_.load(std::memory_order_relaxed), r=r_.load(std::memory_order_acquire);
        n=std::min(n,cap_-static_cast<size_t>(w-r));
        for(size_t i=0;i<n;i++)data_[static_cast<size_t>((w+i)%cap_)]=p[i];
        w_.store(w+n,std::memory_order_release); return n;
    }
    size_t pop(float* p,size_t n){
        auto r=r_.load(std::memory_order_relaxed), w=w_.load(std::memory_order_acquire);
        n=std::min(n,static_cast<size_t>(w-r));
        for(size_t i=0;i<n;i++)p[i]=data_[static_cast<size_t>((r+i)%cap_)];
        r_.store(r+n,std::memory_order_release); return n;
    }
    size_t discard(size_t n){ auto r=r_.load(),w=w_.load(); n=std::min(n,static_cast<size_t>(w-r)); r_.store(r+n); return n; }
private:
    std::vector<float> data_; size_t cap_; alignas(64) std::atomic<uint64_t> r_{0},w_{0};
};

class Biquad{
public:
    float run(float x){float y=b0*x+z1;z1=b1*x-a1*y+z2;z2=b2*x-a2*y;return y;}
    void hp(float sr,float f){
        f=clampf(f,10,sr*.45f); float w=2*std::numbers::pi_v<float>*f/sr,c=cos(w),s=sin(w),a=s/(2*.7071f),a0=1+a;
        set((1+c)*.5f/a0,-(1+c)/a0,(1+c)*.5f/a0,-2*c/a0,(1-a)/a0);
    }
    void peak(float sr,float f,float q,float g){
        float A=pow(10.0f,g/40),w=2*std::numbers::pi_v<float>*f/sr,c=cos(w),s=sin(w),al=s/(2*q),a0=1+al/A;
        set((1+al*A)/a0,-2*c/a0,(1-al*A)/a0,-2*c/a0,(1-al/A)/a0);
    }
    void lowShelf(float sr,float f,float g){ shelf(sr,f,g,false); }
    void highShelf(float sr,float f,float g){ shelf(sr,f,g,true); }
private:
    void set(float B0,float B1,float B2,float A1,float A2){b0=B0;b1=B1;b2=B2;a1=A1;a2=A2;}
    void shelf(float sr,float f,float g,bool high){
        float A=pow(10.0f,g/40),w=2*std::numbers::pi_v<float>*f/sr,c=cos(w),s=sin(w),al=s*.7071067f,b=2*sqrt(A)*al;
        if(!high){float a0=(A+1)+(A-1)*c+b;set(A*((A+1)-(A-1)*c+b)/a0,2*A*((A-1)-(A+1)*c)/a0,A*((A+1)-(A-1)*c-b)/a0,-2*((A-1)+(A+1)*c)/a0,((A+1)+(A-1)*c-b)/a0);}
        else{float a0=(A+1)-(A-1)*c+b;set(A*((A+1)+(A-1)*c+b)/a0,-2*A*((A-1)+(A+1)*c)/a0,A*((A+1)+(A-1)*c-b)/a0,2*((A-1)-(A+1)*c)/a0,((A+1)-(A-1)*c-b)/a0);}
    }
    float b0=1,b1=0,b2=0,a1=0,a2=0,z1=0,z2=0;
};

class DelayLine{
public:
    void set(size_t n){buf.assign(std::max<size_t>(n+1,2),0);pos=0;}
    float run(float x){float y=buf[pos];buf[pos]=x;pos=(pos+1)%buf.size();return y;}
private: std::vector<float> buf{0,0}; size_t pos=0;
};

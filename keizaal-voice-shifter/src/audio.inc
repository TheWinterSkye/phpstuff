class AudioEngine{
public:
    AudioEngine(){
        ma_context_config c=ma_context_config_init();
        ready_=ma_context_init(nullptr,0,&c,&ctx_)==MA_SUCCESS;
        vep_.engine=this;vep_.ring=&vring_;vep_.monitor=false;
        mep_.engine=this;mep_.ring=&mring_;mep_.monitor=true;
    }
    ~AudioEngine(){stop();if(ready_)ma_context_uninit(&ctx_);}

    bool refresh(){
        if(!ready_||running())return false;
        ma_device_info *p=nullptr,*c=nullptr;ma_uint32 pn=0,cn=0;
        ma_result r=ma_context_get_devices(&ctx_,&p,&pn,&c,&cn);
        if(r!=MA_SUCCESS){error_=L"Could not read the Windows audio-device list.";return false;}
        play_.clear();cap_.clear();
        for(ma_uint32 i=0;i<pn;i++)play_.push_back({p[i].id,widen(p[i].name),p[i].isDefault!=MA_FALSE});
        for(ma_uint32 i=0;i<cn;i++)cap_.push_back({c[i].id,widen(c[i].name),c[i].isDefault!=MA_FALSE});
        return true;
    }
    const auto& captures()const{return cap_;}
    const auto& playbacks()const{return play_;}
    const std::wstring& error()const{return error_;}
    void settings(const VoiceSettings& s){std::scoped_lock l(mtx_);settings_=s;}
    bool running()const{return run_.load();}
    float inPeak()const{return inPeak_.load();}
    float outPeak()const{return outPeak_.load();}
    uint64_t underruns()const{return underruns_.load();}
    uint64_t overflows()const{return overflows_.load();}
    int latencyFrames()const{return latency_.load();}

    bool start(size_t ci,size_t vi,std::optional<size_t> mi,int quality){
        stop();
        if(ci>=cap_.size()||vi>=play_.size()){error_=L"Select a valid input and virtual output.";return false;}
        if(mi&&*mi>=play_.size()){error_=L"The monitor selection is invalid.";return false;}
        if(mi&&memcmp(&play_[vi].id,&play_[*mi].id,sizeof(ma_device_id))==0){error_=L"Monitor and virtual output must be different devices.";return false;}
        iring_.reset();vring_.reset();mring_.reset();inPeak_=0;outPeak_=0;underruns_=0;overflows_=0;quality_=std::clamp(quality,0,2);
        vep_.primed=false;mep_.primed=false;

        ma_device_config vc=ma_device_config_init(ma_device_type_playback);
        vc.playback.pDeviceID=&play_[vi].id;vc.playback.format=ma_format_f32;vc.playback.channels=2;vc.sampleRate=SAMPLE_RATE;
        vc.dataCallback=playbackCb;vc.pUserData=&vep_;vc.performanceProfile=ma_performance_profile_low_latency;vc.periodSizeInFrames=256;vc.periods=3;
        ma_result r=ma_device_init(&ctx_,&vc,&vdev_);if(r!=MA_SUCCESS){error_=L"Could not open the selected VB-CABLE playback endpoint.";cleanup();return false;}vready_=true;

        if(mi){
            ma_device_config mc=ma_device_config_init(ma_device_type_playback);
            mc.playback.pDeviceID=&play_[*mi].id;mc.playback.format=ma_format_f32;mc.playback.channels=2;mc.sampleRate=SAMPLE_RATE;
            mc.dataCallback=playbackCb;mc.pUserData=&mep_;mc.performanceProfile=ma_performance_profile_low_latency;mc.periodSizeInFrames=256;mc.periods=3;
            r=ma_device_init(&ctx_,&mc,&mdev_);if(r!=MA_SUCCESS){error_=L"Could not open the selected headphone monitor.";cleanup();return false;}mready_=true;
        }

        ma_device_config cc=ma_device_config_init(ma_device_type_capture);
        cc.capture.pDeviceID=&cap_[ci].id;cc.capture.format=ma_format_f32;cc.capture.channels=1;cc.sampleRate=SAMPLE_RATE;
        cc.dataCallback=captureCb;cc.pUserData=this;cc.performanceProfile=ma_performance_profile_low_latency;cc.periodSizeInFrames=256;cc.periods=3;
        r=ma_device_init(&ctx_,&cc,&cdev_);if(r!=MA_SUCCESS){error_=L"Could not open the selected input device.";cleanup();return false;}cready_=true;

        run_=true;worker_=std::thread(&AudioEngine::processLoop,this);
        if(ma_device_start(&vdev_)!=MA_SUCCESS||(mready_&&ma_device_start(&mdev_)!=MA_SUCCESS)||ma_device_start(&cdev_)!=MA_SUCCESS){error_=L"One of the audio devices could not start.";stop();return false;}
        return true;
    }

    void stop(){
        bool was=run_.exchange(false);
        if(cready_)ma_device_stop(&cdev_);if(vready_)ma_device_stop(&vdev_);if(mready_)ma_device_stop(&mdev_);
        if(worker_.joinable())worker_.join();
        cleanup();(void)was;
    }

private:
    struct Endpoint{AudioEngine* engine=nullptr;SpscRing* ring=nullptr;bool monitor=false;std::atomic<bool> primed{false};std::array<float,8192> temp{};};
    static void captureCb(ma_device* d,void*,const void* in,ma_uint32 n){
        auto* s=static_cast<AudioEngine*>(d->pUserData);if(!s||!in||!s->run_.load(std::memory_order_relaxed))return;
        if(s->iring_.push(static_cast<const float*>(in),n)<n)s->overflows_++;
    }
    static void playbackCb(ma_device* d,void* out,const void*,ma_uint32 n){
        auto* e=static_cast<Endpoint*>(d->pUserData);auto* p=static_cast<float*>(out);if(!e||!p)return;
        if(n>e->temp.size()){std::fill(p,p+n*2,0);e->engine->underruns_++;return;}
        if(!e->primed.load(std::memory_order_relaxed)&&e->ring->available()<1024){std::fill(p,p+n*2,0);return;}e->primed=true;
        if(e->ring->available()>(1u<<18))e->ring->discard(256);
        size_t got=e->ring->pop(e->temp.data(),n);if(got<n){std::fill(e->temp.begin()+got,e->temp.begin()+n,0);e->primed=false;e->engine->underruns_++;}
        VoiceSettings s;{std::scoped_lock l(e->engine->mtx_);s=e->engine->settings_;}
        bool audible=!e->monitor||s.monitor;
        for(ma_uint32 i=0;i<n;i++){float x=audible?e->temp[i]:0;p[i*2]=x;p[i*2+1]=x;}
    }

    void processLoop(){
        signalsmith::stretch::SignalsmithStretch<float> sh;
        if(quality_==0)sh.configure(1,1536,192,true);else if(quality_==1)sh.configure(1,3072,384,true);else sh.presetDefault(1,(float)SAMPLE_RATE,true);
        sh.reset();int lat=sh.inputLatency()+sh.outputLatency();latency_=lat+1024;
        DelayLine dry;dry.set(std::max(lat,1));Biquad hp,warm,pres,air;
        float gateEnv=0,gateGain=1,compEnv=0,deLow=0,deEnv=0,voiced=1,hfRatio=0;
        std::array<float,PROCESS_FRAMES> in{},clean{},shift{},out{};float* ip[1]={clean.data()};float* op[1]={shift.data()};
        std::array<float,1024> hist{};size_t hpos=0;int estimateCounter=0;
        while(run_.load(std::memory_order_relaxed)){
            if(iring_.available()<PROCESS_FRAMES){std::this_thread::sleep_for(std::chrono::milliseconds(1));continue;}
            iring_.pop(in.data(),PROCESS_FRAMES);VoiceSettings s;{std::scoped_lock l(mtx_);s=settings_;}
            hp.hp((float)SAMPLE_RATE,s.highpass);warm.lowShelf((float)SAMPLE_RATE,180,s.warmth);pres.peak((float)SAMPLE_RATE,2800,.8f,s.presence);air.highShelf((float)SAMPLE_RATE,7500,s.air);
            float ig=dbamp(s.inputGain),gth=dbamp(s.gate),gA=exp(-1.0f/(.004f*SAMPLE_RATE)),gR=exp(-1.0f/(.09f*SAMPLE_RATE));
            float cA=exp(-1.0f/(.006f*SAMPLE_RATE)),cR=exp(-1.0f/(.1f*SAMPLE_RATE)),deC=exp(-2*std::numbers::pi_v<float>*4800/SAMPLE_RATE);
            float peak=0,energy=0,diff=0,prev=0;
            for(int i=0;i<PROCESS_FRAMES;i++){
                float x=in[i]*ig;peak=std::max(peak,std::abs(x));float m=std::abs(x),gc=m>gateEnv?gA:gR;gateEnv=gc*gateEnv+(1-gc)*m;
                float gt=(!s.gateEnabled||gateEnv>=gth)?1:0;gateGain+=(gt-gateGain)*(gt>gateGain?.2f:.006f);x*=gateGain;
                x=air.run(pres.run(warm.run(hp.run(x))));
                deLow=(1-deC)*x+deC*deLow;float hi=x-deLow,hm=std::abs(hi);deEnv+=(hm-deEnv)*(hm>deEnv?.30f:.015f);float hg=1;
                if(deEnv>.055f)hg-=clampf((deEnv-.055f)/.2f,0,1)*s.deess*.75f;x=deLow+hi*hg;
                float cm=std::abs(x),cc=cm>compEnv?cA:cR;compEnv=cc*compEnv+(1-cc)*cm;float th=-14-s.comp*12,ratio=1+s.comp*7,edb=ampdb(compEnv),gr=0;
                if(edb>th){float cd=th+(edb-th)/ratio;gr=cd-edb;}x*=dbamp(gr+s.comp*2);
                clean[i]=x;hist[hpos]=x;hpos=(hpos+1)%hist.size();energy+=x*x;float dd=x-prev;diff+=dd*dd;prev=x;
            }
            inPeak_=peak;
            if(++estimateCounter>=4){estimateCounter=0;std::array<float,1024> ordered{};for(size_t i=0;i<ordered.size();i++)ordered[i]=hist[(hpos+i)%hist.size()];
                double en=0;for(float x:ordered)en+=double(x)*x;float best=0;if(en>1e-7){for(int lag=96;lag<=560;lag+=4){double co=0,l=0,r=0;for(size_t i=lag;i<ordered.size();i++){float a=ordered[i],b=ordered[i-lag];co+=double(a)*b;l+=double(a)*a;r+=double(b)*b;}best=std::max(best,float(co/(sqrt(l*r)+1e-12)));}}
                voiced+=(clampf(best,0,1)-voiced)*.35f;float rr=diff/std::max(energy*4,1e-9f);hfRatio+=(clampf(rr,0,1)-hfRatio)*.35f;
            }
            float totalPitch=s.pitch+s.fine/100.0f;sh.setTransposeSemitones(totalPitch,s.tonality/SAMPLE_RATE);sh.setFormantSemitones(s.formant,s.formantComp);sh.setFormantBase(s.formantBase/SAMPLE_RATE);sh.process(ip,PROCESS_FRAMES,op,PROCESS_FRAMES);
            float unvoiced=clampf((.60f-voiced)/.60f+hfRatio*.55f,0,1),wet=s.bypass?0:s.mix;wet*=1-s.consonant*unvoiced*.88f;wet=clampf(wet,.05f,1);if(s.bypass)wet=0;
            float og=dbamp(s.outputGain),opeak=0;for(int i=0;i<PROCESS_FRAMES;i++){float y=shift[i]*wet+dry.run(clean[i])*(1-wet);y*=og;y=tanh(y*1.35f)/tanh(1.35f);y=clampf(y,-.99f,.99f);out[i]=y;opeak=std::max(opeak,std::abs(y));}outPeak_=opeak;
            if(vring_.push(out.data(),out.size())<out.size())overflows_++;
            if(mring_.push(out.data(),out.size())<out.size()){mring_.discard(out.size());mring_.push(out.data(),out.size());}
        }
    }
    void cleanup(){if(cready_){ma_device_uninit(&cdev_);cready_=false;}if(vready_){ma_device_uninit(&vdev_);vready_=false;}if(mready_){ma_device_uninit(&mdev_);mready_=false;}}

    ma_context ctx_{};bool ready_=false;ma_device cdev_{},vdev_{},mdev_{};bool cready_=false,vready_=false,mready_=false;
    std::vector<DeviceEntry> cap_,play_;std::wstring error_;SpscRing iring_,vring_,mring_;Endpoint vep_,mep_;
    std::atomic<bool> run_{false};std::thread worker_;int quality_=1;std::mutex mtx_;VoiceSettings settings_;
    std::atomic<float> inPeak_{0},outPeak_{0};std::atomic<uint64_t> underruns_{0},overflows_{0};std::atomic<int> latency_{0};
};

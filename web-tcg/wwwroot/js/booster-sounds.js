(function () {
    'use strict';
    const files = { C:'reveal-common.wav', R:'reveal-rare.wav', LEADER:'reveal-rare.wav', SR:'reveal-super-rare.wav',
        UR:'reveal-ultra-rare.wav', ALT:'reveal-alternate-art.wav', FULL_ART:'reveal-full-art.wav', SECRET:'reveal-full-art.wav' };
    window.worldsBoosterSounds = {
        create(baseUrl) {
            let context, source, generation=0, muted=false;
            const bytes=new Map(), buffers=new Map();
            try { muted=window.localStorage.getItem('worlds-booster-muted')==='true'; } catch { /* Optional preference. */ }
            function download(file) {
                if (!bytes.has(file)) bytes.set(file,fetch(`${baseUrl}${file}`).then(response=>{
                    if(!response.ok) throw new Error('Son indisponible');
                    return response.arrayBuffer();
                }).catch(()=>null));
                return bytes.get(file);
            }
            // These six small WAV files are fetched once, never once per card.
            [...new Set(Object.values(files))].forEach(download);
            function stop() {
                generation++;
                if(source) {try {source.stop();} catch { /* Already ended. */ } source=null;}
            }
            function unlock() {
                try {
                    const AudioContext=window.AudioContext||window.webkitAudioContext;
                    if(!AudioContext) return Promise.resolve();
                    if(!context||context.state==='closed') {context=new AudioContext();buffers.clear();}
                    return context.resume().catch(()=>{});
                } catch { return Promise.resolve(); }
            }
            async function play(rarity) {
                stop();
                const token=generation;
                if(muted) return;
                try {
                    // Called directly from the opening button to unlock browser audio.
                    await unlock();
                    if(!context) return;
                    const file=files[rarity]||files.C;
                    if(!buffers.has(file)) buffers.set(file,download(file).then(data=>data?context.decodeAudioData(data.slice(0)):null).catch(()=>null));
                    const buffer=await buffers.get(file);
                    if(!buffer||muted||token!==generation||context.state!=='running') return;
                    const gain=context.createGain();gain.gain.value=.65;gain.connect(context.destination);
                    const current=context.createBufferSource();current.buffer=buffer;current.connect(gain);
                    current.onended=()=>{current.disconnect();gain.disconnect();if(source===current)source=null;};
                    source=current;current.start();
                } catch { /* Audio failures never interrupt the booster opening. */ }
            }
            return {
                get muted() {return muted;},
                open:()=>play('C'), reveal:play, stop,
                setMuted(value) {muted=!!value;if(muted)stop();else unlock();try {window.localStorage.setItem('worlds-booster-muted',String(muted));}catch {}},
                dispose() {stop();if(context&&context.state!=='closed')context.close().catch(()=>{});}
            };
        }
    };
})();

(function () {
    'use strict';

    const clamp = (v, min, max) => Math.min(Math.max(v, min), max);
    const rand = (min, max) => min + Math.random() * (max - min);

    // Profil sonore par rareté (notes, timbre, réverbe, intensité des effets)
    const PROFILES = {
        C: { notes: [659.25], type: 'sine', vol: .22, spacing: 0, detune: 0, reverb: .10, sparkle: 0, shimmer: 0, impact: 0 },
        R: { notes: [659.25, 987.77], type: 'sine', vol: .24, spacing: .07, detune: 0, reverb: .18, sparkle: .3, shimmer: 0, impact: 0 },
        LEADER: { notes: [783.99, 1046.5, 1318.51], type: 'triangle', vol: .24, spacing: .06, detune: 4, reverb: .25, sparkle: .5, shimmer: .25, impact: .2 },
        SR: { notes: [880, 1108.73, 1318.51, 1760], type: 'triangle', vol: .22, spacing: .05, detune: 7, reverb: .32, sparkle: .7, shimmer: .4, impact: .35 },
        UR: { notes: [523.25, 659.25, 783.99, 1046.5], type: 'sawtooth', vol: .14, spacing: .045, detune: 9, reverb: .38, sparkle: .9, shimmer: .6, impact: .7 },
        ALT: { notes: [1174.66, 1396.91, 1760, 2093], type: 'triangle', vol: .20, spacing: .035, detune: 5, reverb: .42, sparkle: 1, shimmer: .8, impact: .5 },
        FULL_ART: { notes: [587.33, 740, 880, 1174.66, 1480], type: 'sawtooth', vol: .12, spacing: .04, detune: 11, reverb: .5, sparkle: 1, shimmer: 1, impact: .85 },
        SECRET: { notes: [392, 587.33, 783.99, 1174.66], type: 'sawtooth', vol: .13, spacing: .09, detune: 13, reverb: .6, sparkle: 1, shimmer: 1, impact: 1 }
    };

    window.worldsBoosterSounds = {
        create() {
            let ctx = null, master = null, reverb = null, noise = null;
            let muted = false;
            let warp = null;
            let twinkleTimer = null;
            const live = new Set();

            try { muted = window.localStorage.getItem('worlds-booster-muted') === 'true'; } catch { /* Préférence optionnelle */ }

            /* ---------- Graphe audio ---------- */

            function makeImpulse(seconds) {
                const len = Math.floor(ctx.sampleRate * seconds);
                const buf = ctx.createBuffer(2, len, ctx.sampleRate);
                for (let c = 0; c < 2; c++) {
                    const d = buf.getChannelData(c);
                    for (let i = 0; i < len; i++) d[i] = (Math.random() * 2 - 1) * Math.pow(1 - i / len, 3);
                }
                return buf;
            }

            function makeNoise() {
                const buf = ctx.createBuffer(1, ctx.sampleRate * 2, ctx.sampleRate);
                const d = buf.getChannelData(0);
                for (let i = 0; i < d.length; i++) d[i] = Math.random() * 2 - 1;
                return buf;
            }

            // Retourne null si le son est coupé ou si l'audio n'est pas encore débloqué
            function ensure() {
                if (muted) return null;
                const AC = window.AudioContext || window.webkitAudioContext;
                if (!AC) return null;

                if (!ctx || ctx.state === 'closed') {
                    ctx = new AC();
                    master = ctx.createGain();
                    master.gain.value = .9;
                    master.connect(ctx.destination);

                    reverb = ctx.createConvolver();
                    reverb.buffer = makeImpulse(2.4);
                    const wet = ctx.createGain();
                    wet.gain.value = .7;
                    reverb.connect(wet);
                    wet.connect(master);

                    noise = makeNoise();
                }

                if (ctx.state !== 'running') ctx.resume().catch(() => { });
                return ctx.state === 'running' ? ctx : null;
            }

            function now(offset = 0) { return ctx.currentTime + .01 + offset; }

            // Bus de sortie : panoramique + envoi vers la réverbe
            function bus(pan = 0, wet = .2) {
                const input = ctx.createGain();
                const panner = ctx.createStereoPanner();
                panner.pan.value = clamp(pan, -1, 1);
                input.connect(panner);
                panner.connect(master);
                if (wet > 0) {
                    const send = ctx.createGain();
                    send.gain.value = wet;
                    panner.connect(send);
                    send.connect(reverb);
                }
                return input;
            }

            function env(g, t, attack, peak, dur) {
                g.gain.setValueAtTime(.0001, t);
                g.gain.exponentialRampToValueAtTime(Math.max(peak, .001), t + attack);
                g.gain.exponentialRampToValueAtTime(.0001, t + dur);
            }

            function play(node, start, stop) {
                live.add(node);
                node.onended = () => live.delete(node);
                node.start(start);
                node.stop(stop);
            }

            /* ---------- Briques sonores ---------- */

            function tone(freq, t, dur, o = {}) {
                const osc = ctx.createOscillator();
                osc.type = o.type || 'sine';
                osc.frequency.setValueAtTime(freq, t);
                if (o.glideTo) osc.frequency.exponentialRampToValueAtTime(o.glideTo, t + dur);
                if (o.detune) osc.detune.value = o.detune;
                const g = bus(o.pan || 0, o.wet ?? .15);
                osc.connect(g);
                env(g, t, o.attack || .006, o.vol || .2, dur);
                play(osc, t, t + dur + .05);
            }

            // Bruit filtré : sert au scratch, aux mouvements de carte et aux souffles
            function sweep(t, dur, o = {}) {
                const src = ctx.createBufferSource();
                src.buffer = noise;
                src.loop = true;
                const f = ctx.createBiquadFilter();
                f.type = o.type || 'bandpass';
                f.Q.value = o.q ?? 1;
                f.frequency.setValueAtTime(o.f0 || 1000, t);
                f.frequency.exponentialRampToValueAtTime(o.f1 || 3000, t + dur);
                const g = bus(o.pan || 0, o.wet ?? .1);
                src.connect(f);
                f.connect(g);
                env(g, t, o.attack || .02, o.vol || .2, dur);
                play(src, t, t + dur + .05);
            }

            // Impact grave (boum)
            function impact(t, vol, pan = 0) {
                tone(120, t, .8, { type: 'sine', glideTo: 36, vol, attack: .004, wet: .3, pan });
                sweep(t, .3, { type: 'lowpass', f0: 1400, f1: 180, q: .6, vol: vol * .6, attack: .002, wet: 0, pan });
            }

            /* ---------- API publique ---------- */

            // Clic d'ouverture : débloque l'audio (appelé depuis le clic sur le paquet)
            function open() {
                if (!ensure()) return;
                tone(1046.5, now(), .12, { type: 'sine', vol: .08, wet: .2 });
            }

            // Tapotements synchronisés avec le tremblement du paquet (0 → 0.7 s)
            function packRumble() {
                if (!ensure()) return;
                const t0 = now();
                tone(70, t0, .7, { type: 'sine', glideTo: 45, vol: .35, wet: 0 });
                for (let k = 0; k < 14; k++) {
                    sweep(t0 + k * .05, .04, {
                        type: 'lowpass', f0: 500, f1: 160, q: .5,
                        vol: .18, attack: .002, wet: 0, pan: rand(-.1, .1)
                    });
                }
            }

            // Scratch de déchirure : rip continu + crépitements
            function tear(dur = .9) {
                if (!ensure()) return;
                const t0 = now();
                sweep(t0, dur, { type: 'bandpass', f0: 800, f1: 3400, q: 2.5, vol: .3, attack: .04, wet: .05 });
                sweep(t0, dur * .6, { type: 'highpass', f0: 2500, f1: 6000, q: .5, vol: .1, attack: .02, wet: 0 });
                for (let k = 0; k < 42; k++) {
                    sweep(t0 + Math.random() * dur, rand(.008, .02), {
                        type: 'highpass', f0: 3000, f1: 7000, q: .6,
                        vol: rand(.05, .2), attack: .001, wet: 0, pan: rand(-.4, .4)
                    });
                }
            }

            // Pop de flash à la déchirure
            function pop() {
                if (!ensure()) return;
                const t0 = now();
                tone(260, t0, .22, { type: 'sine', glideTo: 70, vol: .45, wet: .2 });
                sweep(t0, .09, { type: 'highpass', f0: 2000, f1: 4500, q: .5, vol: .25, attack: .001, wet: .1 });
            }

            // Cartes qui montent hors du paquet : souffle + glissement ascendant
            function cardRise(pan = 0, index = 0) {
                if (!ensure()) return;
                const t0 = now();
                sweep(t0, .5, { type: 'highpass', f0: 700, f1: 4200, q: .6, vol: .2, attack: .09, pan, wet: .12 });
                tone(300 + index * 35, t0, .45, { type: 'triangle', glideTo: 620, vol: .06, pan, wet: .1, attack: .08 });
            }

            // Explosion en étoiles : une grosse première, les suivantes plus légères
            function starBurst(pan = 0, big = false) {
                if (!ensure()) return;
                const t0 = now();
                tone(1400, t0, .3, { type: 'sine', glideTo: 2600, vol: big ? .18 : .09, pan, wet: .4 });
                tone(2093, t0 + .02, .22, { type: 'sine', vol: .05, pan, wet: .4 });
                sweep(t0, .18, { type: 'highpass', f0: 3000, f1: 6000, q: .5, vol: big ? .18 : .08, pan, wet: .2 });
                if (big) impact(t0, .35, pan);
            }

            // Changement de couleur d'une étoile : note tirée du profil de rareté
            function starTint(rarity, pan = 0) {
                if (!ensure()) return;
                const cfg = PROFILES[rarity] || PROFILES.C;
                const t0 = now();
                tone(cfg.notes[cfg.notes.length - 1], t0, .35, { type: 'sine', vol: .12, pan, wet: .35, attack: .01 });
                tone(cfg.notes[0] * 2, t0 + .04, .25, { type: 'triangle', vol: .05, pan, wet: .3 });
            }

            // Scintillement continu : plus la rareté est haute, plus il est fréquent et brillant
            function twinkleStart(stars) {
                if (!ensure() || twinkleTimer) return;
                twinkleTimer = setInterval(() => {
                    if (!ctx || ctx.state !== 'running') return;
                    stars.forEach(s => {
                        const cfg = PROFILES[s.rarity] || PROFILES.C;
                        if (Math.random() > .04 + cfg.sparkle * .16) return;
                        const note = cfg.notes[Math.floor(Math.random() * cfg.notes.length)] * 2;
                        tone(note * (Math.random() < .5 ? 1 : 1.5), now(), rand(.12, .22), {
                            type: 'sine',
                            vol: .025 + cfg.sparkle * .07,
                            pan: s.pan + rand(-.15, .15),
                            wet: .45
                        });
                    });
                }, 70);
            }

            function twinkleStop() {
                if (twinkleTimer) clearInterval(twinkleTimer);
                twinkleTimer = null;
            }

            // Warp : bruit de vent + drone, pilotés par setWarpSpeed()
            function warpStart() {
                if (!ensure() || warp) return;
                const t = now();

                const src = ctx.createBufferSource();
                src.buffer = noise;
                src.loop = true;
                const f = ctx.createBiquadFilter();
                f.type = 'bandpass';
                f.Q.value = 2;
                f.frequency.value = 380;
                const g = bus(0, .18);
                g.gain.setValueAtTime(.0001, t);
                g.gain.exponentialRampToValueAtTime(.06, t + .5);
                src.connect(f);
                f.connect(g);

                const osc = ctx.createOscillator();
                osc.type = 'sawtooth';
                osc.frequency.value = 90;
                const og = bus(0, .1);
                og.gain.setValueAtTime(.0001, t);
                og.gain.exponentialRampToValueAtTime(.03, t + .6);
                osc.connect(og);

                warp = { src, f, g, osc, og };
                [src, osc].forEach(node => {
                    live.add(node);
                    node.onended = () => live.delete(node);
                });
                src.start(t);
                osc.start(t);
            }

            // v entre 0 et 1 : le filtre s'ouvre, le drone monte, le volume augmente
            function setWarpSpeed(v) {
                if (!warp || !ctx) return;
                const t = ctx.currentTime;
                warp.f.frequency.setTargetAtTime(380 + v * 2600, t, .05);
                warp.f.Q.setTargetAtTime(2 + v * 4, t, .08);
                warp.g.gain.setTargetAtTime(.05 + v * .2, t, .06);
                warp.osc.frequency.setTargetAtTime(90 + v * 220, t, .08);
                warp.og.gain.setTargetAtTime(.02 + v * .03, t, .08);
            }

            function warpStop(dur = .5) {
                if (!warp || !ctx) return;
                const t = ctx.currentTime;
                const w = warp;
                warp = null;
                w.g.gain.setTargetAtTime(.0001, t, dur / 4);
                w.og.gain.setTargetAtTime(.0001, t, dur / 4);
                w.src.stop(t + dur);
                w.osc.stop(t + dur);
            }

            // Passage d'une étoile : balayage dont la vitesse dépend de la durée du segment
            function zip(pan = 0, dur = .6) {
                if (!ensure()) return;
                const t0 = now();
                const speed = 1 / dur;
                sweep(t0, dur, { type: 'bandpass', f0: 700, f1: 900 + speed * 700, q: 3, vol: .09, attack: .05, pan, wet: .25 });
                tone(420, t0, dur, { type: 'sawtooth', glideTo: 420 + speed * 260, vol: .03, pan, wet: .2, attack: .08 });
            }

            function cardFlip(pan = 0) {
                if (!ensure()) return;
                const t0 = now();
                sweep(t0, .16, { type: 'highpass', f0: 2600, f1: 900, q: .7, vol: .18, attack: .01, pan, wet: .05 });
                tone(1100, t0, .1, { type: 'triangle', glideTo: 620, vol: .05, pan, wet: .08 });
            }

            function cardSlide(pan = 0) {
                if (!ensure()) return;
                const t0 = now();
                sweep(t0, .5, { type: 'bandpass', f0: 2200, f1: 500, q: 1.2, vol: .18, attack: .06, pan, wet: .08 });
            }

            // Révélation d'une carte : accord, arpège, brillance et impact selon la rareté
            function reveal(rarity, pan = 0) {
                if (!ensure()) return;
                const cfg = PROFILES[rarity] || PROFILES.C;
                const t0 = now();
                const n = cfg.notes.length;
                const dur = .9 + cfg.reverb * 1.5;

                cfg.notes.forEach((f, i) => {
                    const t = t0 + i * cfg.spacing;
                    const vol = cfg.vol * (1 - i * .06);
                    tone(f, t, dur, { type: cfg.type, vol, pan, wet: cfg.reverb, attack: .008 });
                    if (cfg.detune) tone(f, t, dur, { type: cfg.type, vol: vol * .6, detune: cfg.detune, pan, wet: cfg.reverb });
                });

                const sparkles = Math.round(cfg.sparkle * 7);
                for (let k = 0; k < sparkles; k++) {
                    tone(cfg.notes[k % n] * 2, t0 + .12 + k * .065, .3, {
                        type: 'sine', vol: .04 + cfg.sparkle * .04,
                        pan: pan + (k % 2 ? .3 : -.3), wet: .5
                    });
                }

                if (cfg.shimmer) {
                    sweep(t0, .9 + cfg.shimmer * .8, { type: 'bandpass', f0: 1200, f1: 5500, q: 3.5, vol: .1 * cfg.shimmer, attack: .15, pan, wet: cfg.reverb });
                }

                if (cfg.impact) impact(t0, .3 * cfg.impact, pan);

                if (rarity === 'C') {
                    sweep(t0, .05, { type: 'highpass', f0: 2500, f1: 2500, q: .6, vol: .12, attack: .002, wet: 0 });
                }
            }

            // Tension avant la dernière carte
            function tensionRise(dur = .36) {
                if (!ensure()) return;
                const t0 = now();
                sweep(t0, dur, { type: 'bandpass', f0: 250, f1: 4200, q: 1.4, vol: .2, attack: dur * .7, wet: .1 });
                tone(140, t0, dur, { type: 'sawtooth', glideTo: 520, vol: .05, attack: .05, wet: .1 });
            }

            // Arrête les boucles de l'intro (scintillement, warp) sans couper les sons en cours
            function stopIntro() {
                twinkleStop();
                warpStop(.25);
            }

            // Coupe tout immédiatement
            function stop() {
                twinkleStop();
                warp = null;
                live.forEach(node => { try { node.stop(); } catch { /* Déjà terminé */ } });
                live.clear();
            }

            function setMuted(value) {
                muted = !!value;
                if (muted) stop(); else ensure();
                try { window.localStorage.setItem('worlds-booster-muted', String(muted)); } catch { /* ignore */ }
            }

            function dispose() {
                stop();
                if (ctx && ctx.state !== 'closed') ctx.close().catch(() => { });
                ctx = null;
            }

            return {
                get muted() { return muted; },
                open, stop, stopIntro, setMuted, dispose,
                packRumble, tear, pop, cardRise, starBurst, starTint,
                twinkleStart, twinkleStop, warpStart, setWarpSpeed, warpStop,
                zip, cardFlip, cardSlide, reveal, tensionRise
            };
        }
    };
})();
(function () {
    'use strict';
    const root = document.getElementById('worldsHome');
    const canvas = document.getElementById('whPlanet');
    if (!root || !canvas) return;
    const stage = document.getElementById('whStage');
    const wrap = canvas.parentElement;
    const hero = document.getElementById('whHero');
    const panels = Array.from(root.querySelectorAll('.wh-panel'));
    const pins = Array.from(root.querySelectorAll('.wh-pin'));
    const destinations = Array.from(root.querySelectorAll('[data-open-panel]'));
    const rotationButton = document.getElementById('whRotation');
    const readout = document.getElementById('whCountry');
    const motion = window.matchMedia('(prefers-reduced-motion: reduce)');
    const animator = window.gsap;
    const radians = Math.PI / 180;
    const state = { yaw: -.22, pitch: .28, scale: 1, phase: 0 };
    const fetchController = new AbortController();
    let ctx = null;
    try { ctx = canvas.getContext('2d'); } catch { /* Navigation remains available without canvas. */ }
    let width = 1, height = 1, radius = 1, dpr = 1, countries = [], countryPaths = [];
    let activePanel = null, returnFocus = null, dragging = false, paused = motion.matches;
    let lastX = 0, lastY = 0, velocity = 0, resumeAt = 0, previousTime = 0, lastFrame = 0;
    let pointer = null, dirty = true, disposed = false, animationFrame = 0;
    const animations = new Set();
    const animate = (target, vars, from) => {
        if (!animator || motion.matches) {
            if (target === state) Object.assign(state, Object.fromEntries(Object.entries(vars).filter(([key]) => key in state)));
            dirty = true;
            return;
        }
        const options = { ...vars, onUpdate: () => { dirty = true; }, onComplete() { animations.delete(tween); vars.onComplete?.(); } };
        const tween = from ? animator.fromTo(target, from, options) : animator.to(target, options);
        animations.add(tween);
    };
    const vector = ([lon, lat]) => {
        const c = Math.cos(lat * radians);
        return [c * Math.sin(lon * radians), Math.sin(lat * radians), c * Math.cos(lon * radians)];
    };
    function projectVector(v) {
        const sy = Math.sin(state.yaw), cy = Math.cos(state.yaw), sp = Math.sin(state.pitch), cp = Math.cos(state.pitch);
        const x = v[0] * cy + v[2] * sy;
        const z = v[2] * cy - v[0] * sy;
        return { x, y: v[1] * cp - z * sp, z: v[1] * sp + z * cp };
    }
    function project(lat, lon) {
        const p = projectVector(vector([lon, lat]));
        const r = radius * state.scale;
        return { x: width / 2 + p.x * r, y: height / 2 - p.y * r, z: p.z };
    }
    function resize() {
        width = Math.max(1, wrap.clientWidth); height = Math.max(1, wrap.clientHeight);
        dpr = Math.min(window.devicePixelRatio || 1, 2);
        canvas.width = Math.round(width * dpr); canvas.height = Math.round(height * dpr);
        ctx?.setTransform(dpr, 0, 0, dpr, 0, 0);
        radius = Math.min(width * .36, height * .375);
        dirty = true;
        draw();
    }
    // Clip each geographical polygon against the visible hemisphere before projecting it.
    function countryPath(country) {
        const path = new Path2D(), r = radius * state.scale;
        for (const polygon of country.polygons) for (const ring of polygon) {
            const points = ring.map(projectVector), visible = [];
            let previous = points[points.length - 1];
            for (const point of points) {
                if ((point.z >= 0) !== (previous.z >= 0)) {
                    const t = previous.z / (previous.z - point.z);
                    visible.push({ x: previous.x + (point.x - previous.x) * t, y: previous.y + (point.y - previous.y) * t });
                }
                if (point.z >= 0) visible.push(point);
                previous = point;
            }
            if (visible.length < 3) continue;
            visible.forEach((p, i) => path[i ? 'lineTo' : 'moveTo'](width / 2 + p.x * r, height / 2 - p.y * r));
            path.closePath();
        }
        return path;
    }
    function line(points, color, lineWidth = .6) {
        ctx.beginPath(); let pen = false;
        points.forEach(p => {
            if (p.z < 0) { pen = false; return; }
            ctx[pen ? 'lineTo' : 'moveTo'](p.x, p.y); pen = true;
        });
        ctx.strokeStyle = color; ctx.lineWidth = lineWidth; ctx.stroke();
    }
    function draw() {
        if (!ctx || disposed) return;
        ctx.clearRect(0, 0, width, height);
        const cx = width / 2, cy = height / 2, r = radius * state.scale;
        const glow = ctx.createRadialGradient(cx, cy, r * .85, cx, cy, r * 1.45);
        glow.addColorStop(0, '#e4ed7c20'); glow.addColorStop(.45, '#bfc6550b'); glow.addColorStop(1, '#bfc65500');
        ctx.fillStyle = glow; ctx.fillRect(0, 0, width, height);
        // Calibration ring and its fine radial divisions.
        ctx.strokeStyle = '#d6de7b28'; ctx.lineWidth = .6;
        ctx.beginPath(); ctx.arc(cx, cy, r * 1.14, 0, Math.PI * 2); ctx.stroke();
        for (let i = 0; i < 120; i++) {
            const a = i / 120 * Math.PI * 2, inner = i % 5 === 0 ? 1.155 : 1.17;
            ctx.beginPath(); ctx.moveTo(cx + Math.cos(a) * r * inner, cy + Math.sin(a) * r * inner);
            ctx.lineTo(cx + Math.cos(a) * r * 1.185, cy + Math.sin(a) * r * 1.185); ctx.stroke();
        }
        const body = ctx.createRadialGradient(cx - r * .35, cy - r * .4, 0, cx, cy, r);
        body.addColorStop(0, '#263522'); body.addColorStop(.65, '#101d16'); body.addColorStop(1, '#070e0c');
        ctx.fillStyle = body; ctx.beginPath(); ctx.arc(cx, cy, r, 0, Math.PI * 2); ctx.fill();
        ctx.save(); ctx.clip();
        for (let lat = -60; lat <= 60; lat += 20) {
            const points = []; for (let lon = -180; lon <= 180; lon += 4) points.push(project(lat, lon));
            line(points, '#deed9a17');
        }
        for (let lon = -180; lon < 180; lon += 20) {
            const points = []; for (let lat = -90; lat <= 90; lat += 4) points.push(project(lat, lon));
            line(points, '#deed9a17');
        }
        countryPaths = countries.map(country => ({ country, path: countryPath(country) }));
        let hovered = null;
        if (pointer && !dragging) for (const item of countryPaths) {
            if (ctx.isPointInPath(item.path, pointer.x * dpr, pointer.y * dpr, 'evenodd')) { hovered = item; break; }
        }
        for (const item of countryPaths) {
            ctx.fillStyle = item === hovered ? '#fce76b83' : '#d5d57325';
            ctx.fill(item.path, 'evenodd');
            ctx.strokeStyle = item === hovered ? '#fff2a6' : '#dce68b85';
            ctx.lineWidth = item === hovered ? 1.2 : .65; ctx.stroke(item.path);
        }
        // A soft scan travels through the land, without hiding its frontiers.
        if (!motion.matches) {
            const scanY = cy - r + ((state.phase * .12) % 1) * r * 2;
            const scan = ctx.createLinearGradient(0, scanY - 18, 0, scanY + 18);
            scan.addColorStop(0, '#eaffaa00'); scan.addColorStop(.5, '#eaffaa16'); scan.addColorStop(1, '#eaffaa00');
            ctx.fillStyle = scan; ctx.fillRect(cx - r, scanY - 18, r * 2, 36);
        }
        ctx.restore();
        ctx.strokeStyle = '#f1ef9c99'; ctx.lineWidth = 1; ctx.shadowColor = '#e9ee84'; ctx.shadowBlur = 12;
        ctx.beginPath(); ctx.arc(cx, cy, r, 0, Math.PI * 2); ctx.stroke(); ctx.shadowBlur = 0;
        // Linked capitals give the hologram a subtle network of light.
        const cities = [[48.85,2.35],[38.9,-77],[35.68,139.69],[-23.55,-46.63],[30.04,31.24]];
        const origin = project(...cities[0]);
        for (const city of cities) {
            const p = project(...city); if (p.z <= .08) continue;
            if (origin.z > .08 && city !== cities[0]) {
                ctx.beginPath(); ctx.moveTo(origin.x,origin.y);
                ctx.quadraticCurveTo((origin.x+p.x)/2,(origin.y+p.y)/2-r*.22,p.x,p.y);
                ctx.strokeStyle = '#f4d96c40'; ctx.lineWidth = .7; ctx.stroke();
            }
            ctx.fillStyle = '#fff1a1'; ctx.beginPath(); ctx.arc(p.x,p.y,1.7,0,Math.PI*2); ctx.fill();
            const pulse = motion.matches ? .4 : (state.phase * .4) % 1;
            ctx.strokeStyle = `rgba(255,224,118,${(1-pulse)*.45})`;
            ctx.beginPath(); ctx.arc(p.x,p.y,3+pulse*9,0,Math.PI*2); ctx.stroke();
        }
        if (readout) readout.textContent = hovered?.country.name || (countries.length ? 'Explorez les pays' : 'Planète Terre');
        pins.forEach(pin => {
            const p = project(+pin.dataset.lat,+pin.dataset.lon), visible = p.z > .14;
            pin.style.transform = `translate(${p.x-10.5}px,${p.y-pin.offsetHeight/2}px)`;
            pin.style.opacity = visible ? Math.min(1,p.z*3) : 0;
            pin.style.visibility = visible ? 'visible' : 'hidden';
            pin.tabIndex = visible ? 0 : -1;
            pin.setAttribute('aria-hidden',String(!visible));
        });
        dirty = false;
    }
    function tick(time) {
        if (disposed) return;
        const now = performance.now();
        const delta = Math.min((now - previousTime)/1000 || 0,.05); previousTime = now;
        if (document.hidden) return;
        if (!paused && !motion.matches && !dragging && !pointer && !activePanel && now > resumeAt) { state.yaw += delta * .035; dirty = true; }
        if (!paused && !motion.matches) { state.phase += delta; dirty = true; }
        if (dirty && now-lastFrame > 32) { draw(); lastFrame = now; }
    }
    function syncControls() {
        rotationButton.setAttribute('aria-pressed',String(paused));
        rotationButton.setAttribute('aria-label',paused?'Animer le globe':'Mettre le globe en pause');
        rotationButton.textContent = paused ? '▷' : 'Ⅱ';
    }
    function closePanels(restore = true) {
        activePanel = null; stage.classList.remove('has-panel');
        hero.removeAttribute('aria-hidden'); hero.inert = false; hero.style.visibility = '';
        panels.forEach(p=>{ animator?.killTweensOf(p); p.hidden=true; });
        [...destinations,...pins].forEach(b=>{ b.classList.remove('active'); b.setAttribute('aria-expanded','false'); });
        resumeAt = performance.now()+2000;
        if (restore && returnFocus?.isConnected) returnFocus.focus({preventScroll:window.innerWidth>720});
        dirty=true;
    }
    function openPanel(id,trigger) {
        const panel = panels.find(p=>p.dataset.panelId===id); if(!panel)return;
        if(activePanel===id){closePanels();return;}
        closePanels(false); activePanel=id; returnFocus=trigger;
        stage.classList.add('has-panel');
        hero.inert=true; hero.setAttribute('aria-hidden','true'); hero.style.visibility='hidden';
        panel.hidden=false;
        [...destinations,...pins].forEach(b=>{const active=(b.dataset.openPanel||b.dataset.panel)===id;b.classList.toggle('active',active);b.setAttribute('aria-expanded',String(active));});
        const pin=pins.find(p=>p.dataset.panel===id);
        if(pin){
            const target=-Number(pin.dataset.lon)*radians;
            const diff=Math.atan2(Math.sin(target-state.yaw),Math.cos(target-state.yaw));
            animate(state,{yaw:state.yaw+diff,pitch:Number(pin.dataset.lat)*radians*.55,duration:1.3,ease:'power3.inOut',overwrite:true});
        }
        animate(panel,{opacity:1,duration:.55,ease:'power3.out',overwrite:true},{opacity:0});
        const heading=panel.querySelector('h2'); heading.tabIndex=-1; heading.focus({preventScroll:window.innerWidth>720});
        panel.querySelectorAll('[data-width]').forEach(bar=>animate(bar,{width:bar.dataset.width+'%',duration:1,ease:'power3.out'},{width:'0%'}));
        dirty=true;
    }
    panels.forEach(panel=>{
        panel.id='whPanel-'+panel.dataset.panelId;
        panel.setAttribute('role','region');panel.setAttribute('aria-labelledby',panel.id+'-title');
        panel.querySelector('h2').id=panel.id+'-title';
        panel.querySelector('.wh-close')?.addEventListener('click',()=>closePanels());
    });
    [...destinations,...pins].forEach(button=>{
        const id=button.dataset.openPanel||button.dataset.panel;
        button.setAttribute('aria-expanded','false');button.setAttribute('aria-controls','whPanel-'+id);
        button.addEventListener('click',()=>openPanel(id,button));
    });
    const onKey=e=>{if(e.key==='Escape'&&activePanel)closePanels();};
    document.addEventListener('keydown',onKey);
    rotationButton.addEventListener('click',()=>{paused=!paused;syncControls();dirty=true;});
    document.getElementById('whReset').addEventListener('click',()=>{
        animator?.killTweensOf(state);animate(state,{yaw:-.22,pitch:.28,duration:1.2,ease:'power3.inOut'});resumeAt=performance.now()+2500;
    });
    canvas.addEventListener('pointerdown',e=>{
        if(e.button!==0)return;dragging=true;lastX=e.clientX;lastY=e.clientY;velocity=0;pointer=null;
        canvas.setPointerCapture(e.pointerId);canvas.classList.add('dragging');animator?.killTweensOf(state);
    });
    canvas.addEventListener('pointermove',e=>{
        const box=canvas.getBoundingClientRect(); pointer={x:e.clientX-box.left,y:e.clientY-box.top};dirty=true;
        if(!dragging)return;
        velocity=(e.clientX-lastX)*.005;state.yaw+=velocity;
        state.pitch=Math.max(-1.1,Math.min(1.1,state.pitch+(e.clientY-lastY)*.004));lastX=e.clientX;lastY=e.clientY;
    });
    function release(){
        if(!dragging)return;dragging=false;canvas.classList.remove('dragging');resumeAt=performance.now()+2500;
        if(!motion.matches)animate(state,{yaw:state.yaw+velocity*12,duration:1.2,ease:'power3.out',overwrite:true});
        dirty=true;
    }
    canvas.addEventListener('pointerup',release);canvas.addEventListener('pointercancel',release);canvas.addEventListener('lostpointercapture',release);
    canvas.addEventListener('pointerleave',()=>{pointer=null;dirty=true;});
    root.querySelectorAll('.wh-destination').forEach(tile=>{
        tile.addEventListener('pointermove',e=>{if(motion.matches)return;const b=tile.getBoundingClientRect();tile.style.setProperty('--mx',(e.clientX-b.left)+'px');tile.style.setProperty('--my',(e.clientY-b.top)+'px');});
    });
    const onMotion=()=>{
        if(motion.matches){
            animations.forEach(t=>t.progress(1));
            animator?.killTweensOf(state);
        }
        paused=motion.matches;syncControls();dirty=true;
    };motion.addEventListener('change',onMotion);
    const observer=typeof ResizeObserver==='function'?new ResizeObserver(resize):null;
    observer?.observe(wrap);window.addEventListener('resize',resize);
    if(ctx)fetch(canvas.dataset.countriesUrl,{signal:fetchController.signal}).then(r=>{if(!r.ok)throw new Error('Atlas indisponible');return r.json();}).then(data=>{
        if(disposed)return;countries=data.features.map(c=>({name:c.name,polygons:c.polygons.map(p=>p.map(r=>r.map(vector)))}));dirty=true;
    }).catch(()=>{if(readout&&!disposed)readout.textContent='Planète Terre';});
    syncControls();resize();
    if(animator&&!motion.matches){
        animate(root.querySelector('.wh-title'),{opacity:1,y:0,duration:.8,ease:'power3.out'},{opacity:0,y:-12});
        animate(hero,{opacity:1,duration:1,ease:'power3.out'},{opacity:0});
        animate(hero.querySelector('h1'),{opacity:1,y:0,duration:1,ease:'power3.out'},{opacity:0,y:20});
        animate(state,{scale:1,duration:1.6,ease:'power3.out'},{scale:.7});
        animate(root.querySelectorAll('.wh-destination'),{opacity:1,y:0,duration:.7,stagger:.08,delay:.2,ease:'power3.out'},{opacity:0,y:20});
        root.querySelectorAll('[data-count]').forEach(el=>{
            const target=Number(el.dataset.count)||0,decimals=Number(el.dataset.decimals)||0,suffix=el.dataset.suffix||'',counter={v:0};
            const tween=animator.to(counter,{v:target,duration:1.5,delay:.3,ease:'power2.out',onUpdate:()=>{el.textContent=counter.v.toLocaleString('fr-FR',{minimumFractionDigits:decimals,maximumFractionDigits:decimals})+suffix;}});animations.add(tween);
        });
    }
    if(animator)animator.ticker.add(tick);else{
        const frame=()=>{tick();if(!disposed)animationFrame=requestAnimationFrame(frame);};animationFrame=requestAnimationFrame(frame);
    }
    window.addEventListener('pagehide',e=>{
        if(e.persisted)return;disposed=true;fetchController.abort();observer?.disconnect();
        animator?.ticker.remove(tick);cancelAnimationFrame(animationFrame);animations.forEach(t=>t.kill());
        document.removeEventListener('keydown',onKey);motion.removeEventListener('change',onMotion);window.removeEventListener('resize',resize);
    });
})();

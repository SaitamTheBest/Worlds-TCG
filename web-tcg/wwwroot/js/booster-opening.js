document.addEventListener("DOMContentLoaded", () => {
    if (typeof gsap === "undefined") {
        console.error("GSAP n'est pas chargé.");
        return;
    }

    /* ============================================================
       DOM
       ============================================================ */

    const root = document.querySelector(".booster-opening");
    const stage = document.querySelector(".booster-opening-stage");
    const slots = [...document.querySelectorAll(".booster-card-slot")];
    const summary = document.getElementById("booster-summary");
    const summaryCount = document.getElementById("summary-count");
    const progress = document.getElementById("revealed-count");
    const instruction = document.getElementById("booster-instruction-text");
    const skipButton = document.getElementById("skip-opening");
    const actions = document.getElementById("booster-actions");
    const soundButton = document.getElementById("booster-sound-toggle");

    const pack = document.getElementById("booster-pack");
    const packWrap = document.querySelector(".booster-pack-wrap");
    const packGlow = document.querySelector(".booster-pack-glow");
    const packBody = document.querySelector(".booster-pack-body");
    const packTop = document.querySelector(".booster-pack-top");
    const packHint = document.querySelector(".booster-pack-hint");

    const fxLayer = document.querySelector(".booster-fx");
    const particleContainer = document.querySelector(".booster-particles");
    const flash = document.querySelector(".booster-rarity-flash");
    const glow = document.querySelector(".booster-final-glow");
    const rays = document.querySelector(".booster-rarity-rays");
    const rings = [...document.querySelectorAll(".booster-rarity-ring")];
    const rarityName = document.querySelector(".booster-rarity-name");
    const rarityPretitle = document.querySelector(".booster-rarity-pretitle");
    const lastWarning = document.querySelector(".booster-last-warning");
    const mysteryCard = document.getElementById("booster-mystery-card");

    if (!root || !stage || !pack || !summary || !slots.length) {
        console.error("Impossible d'initialiser l'ouverture du booster.");
        return;
    }

    const total = slots.length;

    /* ============================================================
       ÉTAT
       ============================================================ */

    // idle -> intro -> collect -> done
    let phase = "idle";
    let currentIndex = 0;
    let busy = false;
    let awaitingLast = false;
    let stars = [];
    let introTl = null;
    let revealTl = null;
    let fxTl = null;
    let idleTweens = [];
    const starTweens = [];
    const reducedMotion = window.matchMedia?.("(prefers-reduced-motion: reduce)")?.matches ?? false;
    const packArtwork = pack.querySelector(".booster-pack-artwork");
    packArtwork?.addEventListener("error", () => pack.classList.remove("has-artwork"));
    if (packArtwork?.complete && !packArtwork.naturalWidth) pack.classList.remove("has-artwork");

    /* ============================================================
       EXPIRATION
       ============================================================ */

    function redirectIfOpeningExpired() {
        const openedAt = Date.parse(root.dataset.openedAt || "");
        const seconds = Number(root.dataset.expiresAfterSeconds || "60");
        const expiredUrl = root.dataset.expiredUrl || "/Booster";

        if (!Number.isFinite(openedAt) || !Number.isFinite(seconds)) return false;
        if (Date.now() < openedAt + seconds * 1000) return false;

        window.location.replace(expiredUrl);
        return true;
    }

    if (redirectIfOpeningExpired()) return;
    window.addEventListener("pageshow", redirectIfOpeningExpired);
    document.addEventListener("visibilitychange", () => {
        if (document.visibilityState === "visible") redirectIfOpeningExpired();
    });

    /* ============================================================
       SONS
       ============================================================ */

    const sounds = window.worldsBoosterSounds?.create(root.dataset.soundBaseUrl || "/sons/");
    const revealedSounds = new Set();

    function updateSoundButton() {
        if (!soundButton) return;
        soundButton.textContent = sounds?.muted ? "Son désactivé" : "Son activé";
        soundButton.setAttribute("aria-pressed", String(!sounds?.muted));
        soundButton.setAttribute("aria-label", sounds?.muted ? "Activer les sons" : "Couper les sons");
    }

    updateSoundButton();
    soundButton?.addEventListener("click", () => {
        sounds?.setMuted(!sounds.muted);
        updateSoundButton();
    });
    window.addEventListener("pagehide", () => sounds?.dispose());
    document.addEventListener("visibilitychange", () => {
        if (document.visibilityState === "hidden") sounds?.stop();
    });

    function playRevealSound(slot) {
        if (phase === "done" || revealedSounds.has(slot)) return;
        revealedSounds.add(slot);
        sounds?.reveal(getAnimationRarity(slot));
    }

    /* ============================================================
       RARETÉS
       ============================================================ */

    const rarityConfig = {
        C: { label: "COMMUNE", colors: ["#c4c9d4", "#ffffff"], twinkle: .25, power: .45, particles: 6, shake: 0, flash: .15 },
        R: { label: "RARE", colors: ["#49c5ff", "#dff7ff"], twinkle: .45, power: .7, particles: 12, shake: 0, flash: .22 },
        LEADER: { label: "LEADER", colors: ["#f4c430", "#fff1a6", "#ffffff"], twinkle: .6, power: .85, particles: 18, shake: 2, flash: .3 },
        SR: { label: "SUPER RARE", colors: ["#845dff", "#d18cff", "#ffffff"], twinkle: .75, power: .95, particles: 24, shake: 3, flash: .35 },
        UR: { label: "ULTRA RARE", colors: ["#ffcc38", "#fff0a0", "#ff8128"], twinkle: .9, power: 1.15, particles: 36, shake: 5, flash: .5 },
        ALT: { label: "ALT ART", colors: ["#17e9ff", "#c05dff", "#ff5d9d", "#ffffff"], twinkle: 1, power: 1.3, particles: 46, shake: 6, flash: .6 },
        FULL_ART: { label: "FULL ART", colors: ["#ff4f91", "#ffdf4f", "#55eaff", "#a55cff", "#ffffff"], twinkle: 1, power: 1.5, particles: 60, shake: 8, flash: .75 },
        SECRET: { label: "SECRET · GOLD", colors: ["#fff2b0", "#f4c430", "#dba629", "#ffffff"], twinkle: 1, power: 1.7, particles: 80, shake: 9, flash: .85 }
    };

    function normalizeRarity(value) {
        const r = String(value || "").toUpperCase().replace(/[_\- ]/g, "");
        if (r === "SECRET" || r === "GOLD") return "SECRET";
        if (r === "UR" || r === "ULTRARARE") return "UR";
        if (r === "SR" || r === "SUPERRARE") return "SR";
        if (r === "R" || r === "RARE") return "R";
        if (r === "L" || r === "LEADER") return "LEADER";
        return "C";
    }

    function normalizeArtType(value) {
        const a = String(value || "").toUpperCase().replace(/[_\- ]/g, "");
        if (a === "FULLART") return "FULL_ART";
        if (a === "ALTERNATEART" || a === "ALTART" || a === "ALT") return "ALT";
        return "NORMAL";
    }

    function getAnimationRarity(slot) {
        const art = normalizeArtType(slot?.dataset?.artType);
        if (art === "FULL_ART") return "FULL_ART";
        if (art === "ALT") return "ALT";
        return normalizeRarity(slot?.dataset?.rarity);
    }

    /* ============================================================
       HELPERS
       ============================================================ */

    const clamp = (v, min, max) => Math.min(Math.max(v, min), max);

    function getVisual(slot) {
        return slot?.querySelector(".booster-card-visual");
    }

    function getBaseCardSize(slot) {
        const visual = getVisual(slot);
        const card = slot?.querySelector(".tcg-card");
        const width = card?.offsetWidth || visual?.offsetWidth || 260;
        const height = card?.offsetHeight || visual?.offsetHeight || Math.round(width * 1.4);
        return { width, height };
    }

    function setInstruction(text) {
        if (instruction) instruction.textContent = text;
    }

    function updateCounters() {
        const revealed = phase === "done" ? total : phase === "collect" ? currentIndex + 1 : 0;
        const archived = phase === "done" ? total - 1 : phase === "collect" ? currentIndex : 0;
        if (progress) progress.textContent = Math.min(revealed, total);
        if (summaryCount) summaryCount.textContent = `${clamp(archived, 0, total - 1)}/${total}`;
    }

    function screenShake(strength = 4) {
        gsap.timeline()
            .to(stage, { x: strength, duration: .035 })
            .to(stage, { x: -strength, duration: .04, repeat: 4, yoyo: true })
            .to(stage, { x: 0, duration: .04 });
    }

    /* ============================================================
       PARTICULES
       ============================================================ */

    function spawnParticles(count, colors, power = 1) {
        if (!particleContainer) return;

        for (let i = 0; i < count; i++) {
            const p = document.createElement("span");
            const r = Math.random();
            p.className = "booster-particle" + (r > .82 ? " is-line" : r > .6 ? " is-diamond" : "");

            const color = colors[Math.floor(Math.random() * colors.length)];
            p.style.background = color;
            p.style.color = color;

            if (!p.classList.contains("is-line")) {
                const size = 3 + Math.random() * 7;
                p.style.width = `${size}px`;
                p.style.height = `${size}px`;
            }

            particleContainer.appendChild(p);

            const angle = Math.random() * Math.PI * 2;
            const distance = (120 + Math.random() * 280) * power;
            const x = Math.cos(angle) * distance;
            const y = Math.sin(angle) * distance;
            const rot = -180 + Math.random() * 360;

            gsap.timeline({ delay: Math.random() * .14, onComplete: () => p.remove() })
                .fromTo(p,
                    { x: 0, y: 0, scale: 0, opacity: 0, rotation: 0 },
                    { x: x * .25, y: y * .25, scale: .8 + Math.random(), opacity: 1, rotation: rot * .25, duration: .18, ease: "power2.out" })
                .to(p,
                    { x, y, scale: 0, opacity: 0, rotation: rot, duration: .7 + Math.random() * .45, ease: "power2.out" });
        }
    }

    /* ============================================================
       RESET & THÈME DE RARETÉ
       ============================================================ */

    function resetRarityEffects() {
        const fx = [glow, rays, flash, rarityName, rarityPretitle, ...rings].filter(Boolean);
        gsap.killTweensOf(fx);

        gsap.set([glow, rays, ...rings].filter(Boolean), {
            xPercent: -50, yPercent: -50, scale: .6, rotation: 0, opacity: 0
        });
        gsap.set([flash, rarityName, rarityPretitle].filter(Boolean), {
            opacity: 0, scale: 1
        });

        if (particleContainer) particleContainer.innerHTML = "";
    }

    function setRarityTheme(cfg) {
        const main = cfg.colors[0];

        if (rarityName) {
            rarityName.textContent = cfg.label;
            rarityName.style.color = main;
            rarityName.style.textShadow = `0 0 10px ${main}, 0 0 28px ${main}`;
        }

        if (glow) glow.style.background = `radial-gradient(circle, ${main} 0%, transparent 70%)`;
        if (rays) rays.style.background = `repeating-conic-gradient(from 0deg, ${main}55 0deg, transparent 3deg, transparent 13deg)`;
        if (flash) flash.style.background = `radial-gradient(circle, #fff 0%, ${main} 30%, transparent 65%)`;

        rings.forEach((ring, i) => {
            const color = cfg.colors[i % cfg.colors.length];
            ring.style.borderColor = color;
            ring.style.boxShadow = `0 0 20px ${color}, inset 0 0 18px ${color}`;
        });
    }

    /* ============================================================
       POSITIONNEMENT DANS LE RÉCAPITULATIF
       ============================================================ */

    function getArchiveTransform(index) {
        const visual = getVisual(slots[index]);
        if (!visual) return { x: 0, y: 0, scale: .25 };

        const stageRect = stage.getBoundingClientRect();
        const sumRect = summary.getBoundingClientRect();
        const { width: cw, height: ch } = getBaseCardSize(slots[index]);
        const baseX = stageRect.width / 2;
        const baseY = stageRect.height / 2;
        const count = Math.max(total - 1, 1);

        // Mobile / tablette : miniatures en ligne
        if (window.innerWidth <= 900) {
            const tw = clamp(sumRect.width / 7, 46, 62);
            const scale = tw / cw;
            const th = ch * scale;
            const startX = sumRect.left - stageRect.left + 18;
            const usable = sumRect.width - 36;
            let step = tw * .8;
            if (count > 1 && tw + step * (count - 1) > usable) step = (usable - tw) / (count - 1);

            const tx = startX + tw / 2 + index * step;
            const ty = sumRect.top - stageRect.top + sumRect.height - th / 2 - 12;
            return { x: tx - baseX, y: ty - baseY, scale };
        }

        // Desktop : miniatures en colonne
        const tw = clamp(sumRect.width - 58, 64, 86);
        const scale = tw / cw;
        const th = ch * scale;
        const startY = sumRect.top - stageRect.top + 76;
        const avail = sumRect.height - 120;
        let step = th * .72;
        if (count > 1 && th + step * (count - 1) > avail) step = Math.max(28, (avail - th) / (count - 1));

        return {
            x: sumRect.left - stageRect.left + sumRect.width / 2 - baseX,
            y: startY + th / 2 + index * step - baseY,
            scale
        };
    }

    /* ============================================================
       AFFICHAGE / ARCHIVAGE / CARTE MYSTÈRE
       ============================================================ */

    function showCard(index, instant = false) {
        const slot = slots[index];
        const visual = getVisual(slot);
        if (!visual) return;

        slot.classList.remove("is-archived");
        slot.classList.remove("is-face-down");
        slot.classList.add("is-current");
        slot.style.zIndex = "35";
        slot.style.setProperty("--booster-new-scale", "1");

        gsap.killTweensOf(visual);
        gsap.set(visual, {
            x: 0, y: 0, scale: 1, rotation: 0, rotateX: 0, rotateY: 0,
            opacity: 1, visibility: "visible", filter: "none"
        });

        if (instant) return;

        playRevealSound(slot);
        gsap.fromTo(visual,
            { opacity: 0, y: 30, scale: .9, rotateY: 80 },
            { opacity: 1, y: 0, scale: 1, rotateY: 0, duration: .5, ease: "power3.out" });
    }

    function archiveCard(index, instant = false, onComplete = null) {
        const slot = slots[index];
        slot?.classList.remove("is-face-down");
        const visual = getVisual(slot);
        if (!visual) {
            onComplete?.();
            return;
        }

        const target = getArchiveTransform(index);
        const props = {
            x: target.x, y: target.y, scale: target.scale,
            rotation: 0, rotateX: 0, rotateY: 0,
            opacity: 1, visibility: "visible", filter: "none"
        };

        slot.classList.remove("is-current");
        slot.classList.add("is-archived");
        slot.style.zIndex = String(20 + index);

        const done = () => {
            slot.style.setProperty("--booster-new-scale", String(1 / Math.max(.01, target.scale)));
            onComplete?.();
        };

        if (instant) {
            gsap.killTweensOf(visual);
            gsap.set(visual, props);
            done();
            return;
        }

        gsap.timeline({ onComplete: done })
            .to(visual, { y: -10, scale: .96, duration: .12, ease: "power2.out" })
            .to(visual, { ...props, duration: .6, ease: "expo.inOut" });
    }

    function showMysteryCard(index) {
        const slot = slots[index];
        const visual = getVisual(slot);
        if (!visual || !mysteryCard) return;

        slot.classList.remove("is-archived");
        slot.classList.add("is-current");
        slot.style.zIndex = "35";

        gsap.killTweensOf(visual);
        gsap.set(visual, { x: 0, y: 0, scale: 1, opacity: 0, visibility: "hidden", rotation: 0, filter: "none" });

        // On garde les dimensions de la vraie carte
        const rect = visual.getBoundingClientRect();
        mysteryCard.style.width = `${rect.width || visual.offsetWidth || 260}px`;
        mysteryCard.style.height = `${rect.height || visual.offsetHeight || 380}px`;

        mysteryCard.classList.add("is-visible");
        gsap.killTweensOf(mysteryCard);
        gsap.fromTo(mysteryCard,
            { opacity: 0, scale: .86, rotateY: -12, xPercent: -50, yPercent: -50, visibility: "visible" },
            { opacity: 1, scale: 1, rotateY: 0, duration: .5, ease: "back.out(1.4)" });
    }

    function hideMysteryCard() {
        if (!mysteryCard) return;
        mysteryCard.classList.remove("is-visible");
        gsap.killTweensOf(mysteryCard);
        gsap.set(mysteryCard, {
            opacity: 0, visibility: "hidden", xPercent: -50, yPercent: -50,
            scale: .9, rotateY: 0
        });
    }

    function prepareLastCard() {
        showMysteryCard(currentIndex);
        awaitingLast = true;
        setInstruction("CLIQUEZ POUR RÉVÉLER LA CARTE");

        if (lastWarning) {
            gsap.fromTo(lastWarning, { opacity: 0, y: -8 }, { opacity: .85, y: 0, duration: .35 });
        }
    }

    /* ============================================================
       ANIMATION D'ATTENTE
       ============================================================ */

    function startIdle() {
        if (reducedMotion) return;
        idleTweens = [
            gsap.to(packWrap, { y: -14, duration: 1.9, yoyo: true, repeat: -1, ease: "sine.inOut" }),
            gsap.to(packGlow, { opacity: 1, scale: 1.12, duration: 1.9, yoyo: true, repeat: -1, ease: "sine.inOut" }),
            gsap.to(pack, { rotation: 1.5, duration: 2.4, yoyo: true, repeat: -1, ease: "sine.inOut" }),
            gsap.to(packHint, { opacity: .35, duration: .9, yoyo: true, repeat: -1, ease: "sine.inOut" })
        ];

        pack.addEventListener("pointerenter", () => {
            if (phase === "idle") gsap.to(pack, { scale: 1.04, duration: .25 });
        });
        pack.addEventListener("pointerleave", () => {
            if (phase === "idle") gsap.to(pack, { scale: 1, duration: .25 });
        });
    }

    /* ============================================================
       ÉTOILES
       ============================================================ */

    // Position de chaque carte au moment où elle se change en étoile
    function burstTarget(i) {
        const rect = stage.getBoundingClientRect();
        const radius = clamp(Math.min(rect.width, rect.height) * .34, 120, 240);
        const angle = -Math.PI / 2 + (i / total) * Math.PI * 2;
        return { x: Math.cos(angle) * radius, y: Math.sin(angle) * radius * .85 };
    }

    function createStars(targets) {
        fxLayer.innerHTML = "";
        return targets.map((t, i) => {
            const star = document.createElement("span");
            star.className = "booster-star";
            const cfg = rarityConfig[getAnimationRarity(slots[i])] ?? rarityConfig.C;
            star.dataset.rarity = getAnimationRarity(slots[i]);
            star.style.setProperty("--star-size", `${32 + cfg.power * 16}px`);
            star.style.color = "#ffeaa0";
            star.innerHTML = '<span class="booster-star-halo"></span><span class="booster-star-tail"></span>' +
                '<span class="booster-star-core"><svg viewBox="0 0 100 100" aria-hidden="true"><path d="M50 3 63 33 96 37 72 60 79 94 50 77 21 94 28 60 4 37 37 33Z" fill="currentColor" stroke="#fff7d6" stroke-width="2" stroke-linejoin="round"/><path d="M50 16 54 41 76 45 55 51 50 72 44 52 23 45 44 40Z" fill="#fff" opacity=".65"/></svg><span class="booster-star-glint"></span></span>';
            fxLayer.appendChild(star);
            gsap.set(star, { x: t.x, y: t.y, scale: 0, opacity: 1 });
            return star;
        });
    }

    function startTwinkle(star, rarity) {
        // Plus la carte est rare, plus l'étoile scintille fort
        const amp = rarityConfig[rarity]?.twinkle ?? .25;
        starTweens.push(gsap.to(star.querySelector(".booster-star-core"), {
            opacity: .85,
            scale: 1.08 + amp * .3,
            rotation: 8,
            duration: .22 + Math.random() * .18,
            delay: Math.random() * .15,
            repeat: -1,
            yoyo: true,
            ease: "sine.inOut"
        }));
        starTweens.push(gsap.to(star.querySelector(".booster-star-glint"), {
            scale: 1.5 + amp, opacity: .2, rotation: 60, duration: .18 + (1 - amp) * .3,
            repeat: -1, yoyo: true, ease: "sine.inOut"
        }));
    }

    function emitStarTrail(star) {
        // Émission bornée : même à 144 Hz, pas plus d'une étincelle / 90 ms / étoile.
        const now = Date.now();
        if (now - (star.lastSparkAt || 0) < 90) return;
        star.lastSparkAt = now;
        const spark = document.createElement("span");
        spark.className = "booster-star-spark";
        spark.style.color = star.style.color;
        fxLayer.appendChild(spark);
        const x = Number(gsap.getProperty(star, "x"));
        const y = Number(gsap.getProperty(star, "y"));
        gsap.fromTo(spark,
            { x, y, opacity: .9, scale: .6 + Math.random() * .8, rotation: Math.random() * 90 },
            { x: x - 25 - Math.random() * 35, y: y + (Math.random() - .5) * 45, opacity: 0, scale: 0,
                duration: .35 + Math.random() * .15, ease: "power2.out", onComplete: () => spark.remove() });
    }

    /* ============================================================
       INTRO : DÉCHIRURE → CARTES → ÉTOILES → WARP
       ============================================================ */

    function buildIntro() {
        const rect = stage.getBoundingClientRect();
        const visuals = slots.map(getVisual);
        const fanSpacing = Math.min(52, (rect.width - Math.min(278, rect.width * .52)) / Math.max(total + 1, 2));
        const fanY = -Math.min(rect.height * .12, 65);
        const targets = slots.map((_, i) => burstTarget(i));
        const starPoints = slots.map(slot => {
            const rarity = getAnimationRarity(slot);
            return { rarity, cfg: rarityConfig[rarity] ?? rarityConfig.C };
        });

        stars = createStars(targets);

        const tl = gsap.timeline({ onComplete: enterCollection });

        // 1. Le paquet tremble
        tl.to(pack, { x: 6, rotation: 2.5, duration: .05, yoyo: true, repeat: 13, ease: "none" }, 0)
            .set(pack, { x: 0, rotation: 0 }, .7);

        // 2. Déchirure
        tl.call(() => spawnParticles(22, ["#f4c430", "#fff1a6", "#ffffff"], .6), null, .7)
            .to(packTop, { y: -170, x: -70, rotation: -24, opacity: 0, duration: .7, ease: "power3.in" }, .7)
            .fromTo(flash, { opacity: 0 }, { opacity: .85, duration: .1 }, .7)
            .to(flash, { opacity: 0, duration: .5 }, .8)
            .to([packBody, packGlow, packArtwork].filter(Boolean), { opacity: 0, scale: 1.08, duration: .4, ease: "power2.in" }, 1);

        // 3. Les cartes sortent du paquet vers le haut, en éventail
        visuals.forEach((visual, i) => {
            slots[i].classList.add("is-face-down");
            const fan = i - (total - 1) / 2;
            tl.fromTo(visual,
                { opacity: 0, x: 0, y: 90, scale: .55, rotation: 0 },
                {
                    opacity: 1, visibility: "visible",
                    x: fan * fanSpacing, y: fanY, scale: 1, rotation: fan * 4,
                    duration: .8, ease: "back.out(1.25)"
                },
                1.05 + i * .1);
        });

        // 4. Explosion en étoile : chaque carte devient une étoile
        const burstAt = 2.7;
        visuals.forEach((visual, i) => {
            const t = targets[i];
            tl.to(visual,
                { x: t.x, y: t.y, rotation: (i % 2 ? 1 : -1) * (25 + i * 6), scale: .5, duration: .85, ease: "expo.out" },
                burstAt + i * .03)
                .to(visual,
                    { opacity: 0, scale: .1, duration: .25, ease: "power2.in" },
                    burstAt + .65 + i * .03)
                .fromTo(stars[i],
                    { scale: 0, opacity: 1 },
                    { scale: 1, duration: .35, ease: "back.out(2.5)" },
                    burstAt + .6 + i * .03);
        });

        // 5. Chaque étoile prend la palette de sa carte, puis traverse la scène.
        // Le noyau scintille indépendamment du mouvement : aucune tween ne se bat pour sa position.
        tl.call(() => stars.forEach((star, i) => startTwinkle(star, starPoints[i].rarity)), null, 3.9);
        stars.forEach((star, i) => {
            const cfg = starPoints[i].cfg;
            const t = 4.3 + i * .09;
            const spreadY = Math.min(rect.height * .25, 170);
            const randomY = () => (Math.random() * 2 - 1) * spreadY;
            tl.to(star,
                { color: cfg.colors[0], duration: .4 }, 3.65 + i * .06)
                .to(star.querySelector(".booster-star-tail"), { opacity: .85, scaleX: 1, duration: .3 }, t)
                .to(star, { x: -rect.width * (.24 + Math.random() * .1), y: randomY(), rotation: -18 + Math.random() * 36,
                    duration: .65, ease: "sine.inOut", onUpdate: () => emitStarTrail(star) }, t)
                .to(star, { x: rect.width * (.13 + Math.random() * .1), y: randomY(), rotation: -24 + Math.random() * 48,
                    color: cfg.colors[1] || cfg.colors[0], duration: .75, ease: "sine.inOut", onUpdate: () => emitStarTrail(star) }, t + .65)
                .to(star, { x: rect.width * (.02 + Math.random() * .1), y: randomY(), rotation: -15 + Math.random() * 30,
                    color: cfg.colors[0], duration: .55, ease: "sine.inOut", onUpdate: () => emitStarTrail(star) }, t + 1.4)
                .to(star.querySelector(".booster-star-tail"), { scaleX: 2.2, opacity: 1, duration: .35 }, t + 1.9)
                .to(star,
                    { x: rect.width / 2 + 220, y: randomY() * .6, rotation: 0, scale: 1.3,
                        duration: .75, ease: "power3.in", onUpdate: () => emitStarTrail(star) }, t + 1.95)
                .to(star, { opacity: 0, duration: .18 }, t + 2.55);
        });
        tl.to(fxLayer, { opacity: 0, duration: .25 }, 7.5);

        return tl;
    }

    function cleanupIntro() {
        starTweens.splice(0).forEach(tween => tween.kill());
        gsap.killTweensOf(fxLayer.querySelectorAll("*"));
        idleTweens.forEach(tween => tween.kill());
        gsap.killTweensOf([...stars, pack, packTop, packBody, packGlow, flash, stage]);
        stars = [];
        fxLayer.innerHTML = "";
        gsap.set(flash, { opacity: 0 });
        gsap.set(stage, { x: 0 });
        packWrap.style.display = "none";
    }

    /* ============================================================
       PASSAGE AU RÉCAPITULATIF
       ============================================================ */

    function enterCollection() {
        cleanupIntro();
        phase = "collect";
        busy = false;
        currentIndex = 0;

        slots.forEach(slot => {
            gsap.set(getVisual(slot), { opacity: 0, visibility: "hidden" });
        });

        if (total === 1) {
            prepareLastCard();
        } else {
            showCard(0);
            setInstruction("CLIQUEZ SUR LA CARTE POUR CONTINUER");
        }

        updateCounters();
    }

    function startOpening() {
        if (phase !== "idle" || redirectIfOpeningExpired()) return;

        phase = "intro";
        pack.disabled = true;
        idleTweens.forEach(t => t.kill());
        gsap.killTweensOf([packWrap, pack, packGlow, packHint]);
        gsap.set(pack, { rotation: 0, scale: 1 });
        gsap.to(packHint, { opacity: 0, duration: .2 });
        setInstruction("OUVERTURE...");

        sounds?.open();
        if (reducedMotion) { enterCollection(); return; }
        introTl = buildIntro();
    }

    /* ============================================================
       RÉVÉLATION DE LA DERNIÈRE CARTE
       ============================================================ */

    function playRarityFx(visual, cfg) {
        const main = cfg.colors[0];
        const p = cfg.power;
        const sheen = visual.querySelector(".booster-card-sheen");

        fxTl?.kill();
        fxTl = gsap.timeline({ onComplete: showActions })
            .to(glow, { opacity: .3 + p * .2, scale: .9 + p * .25, duration: .4, ease: "power2.out" }, 0)
            .to(rarityPretitle, { opacity: .6, duration: .2 }, 0)
            .to(rarityName, { opacity: 1, scale: 1.06, duration: .3, ease: "back.out(1.6)" }, .05)
            .to(rings, { opacity: .3 + p * .3, scale: 1 + p * .35, rotation: 120, duration: .8, stagger: .1, ease: "expo.out" }, .05)
            .to(rays, { opacity: .08 + p * .12, scale: 1.1, rotation: 90, duration: .9, ease: "power2.out" }, .05)
            .to(visual, { filter: `drop-shadow(0 0 ${14 + p * 18}px ${main})`, duration: .5 }, .1)
            .call(() => {
                if (cfg.shake) screenShake(cfg.shake);
                spawnParticles(cfg.particles, cfg.colors, p);
            }, null, .1)
            .to(flash, { opacity: cfg.flash, duration: .07 }, .1)
            .to(flash, { opacity: 0, duration: .4 }, .17)
            .to(visual, { y: -10, scale: 1.04 + p * .02, duration: .22, ease: "power2.out" }, .1)
            .to(visual, { y: 0, scale: 1, duration: .5, ease: "elastic.out(1, .6)" }, .32);

        // Reflet holographique pour les cartes les plus rares
        if (sheen && p >= 1.1) {
            fxTl.fromTo(sheen,
                { opacity: .9, xPercent: -120 },
                { opacity: 0, xPercent: 120, duration: 1, ease: "power2.inOut" },
                .45);
        }
    }

    function revealLast() {
        if (busy || phase !== "collect" || !awaitingLast) return;

        busy = true;
        awaitingLast = false;

        const slot = slots[currentIndex];
        const visual = getVisual(slot);
        const rarity = getAnimationRarity(slot);
        const cfg = rarityConfig[rarity] ?? rarityConfig.C;

        if (reducedMotion) {
            hideMysteryCard();
            showCard(currentIndex);
            setRarityTheme(cfg);
            gsap.set(rarityName, { opacity: 1 });
            showActions();
            return;
        }

        setInstruction("RÉVÉLATION...");
        if (lastWarning) gsap.to(lastWarning, { opacity: 0, y: -8, duration: .2 });

        const revealRealCard = () => {
            if (phase !== "collect") return;
            slot.classList.remove("is-face-down");
            hideMysteryCard();

            slot.classList.remove("is-archived");
            slot.classList.add("is-current");
            slot.style.zIndex = "50";

            gsap.killTweensOf(visual);
            gsap.fromTo(visual,
                { opacity: 0, scale: .85, rotateY: -12, visibility: "visible", filter: "drop-shadow(0 0 0 rgba(0,0,0,0))" },
                {
                    opacity: 1, scale: 1, rotateY: 0, filter: "drop-shadow(0 0 0 rgba(0,0,0,0))",
                    duration: .45, ease: "back.out(1.45)",
                    onComplete: () => {
                        playRevealSound(slot);
                        setInstruction(cfg.label);
                        setRarityTheme(cfg);
                        playRarityFx(visual, cfg);
                    }
                });
        };

        revealTl = gsap.timeline({ onComplete: revealRealCard })
            .to(mysteryCard, { scale: .94, duration: .12, ease: "power2.in" })
            .to(mysteryCard, { scale: 1.08, duration: .12, ease: "power2.out" })
            .to(mysteryCard, { opacity: 0, scale: 1.18, duration: .22, ease: "power3.in" });
    }

    /* ============================================================
       CLIC SUR UNE CARTE
       ============================================================ */

    function handleSlotClick(index) {
        if (phase !== "collect" || busy || index !== currentIndex) return;

        if (awaitingLast) {
            revealLast();
            return;
        }

        busy = true;

        archiveCard(currentIndex, false, () => {
            if (phase !== "collect") return;

            currentIndex++;
            updateCounters();

            if (currentIndex === total - 1) {
                prepareLastCard();
                busy = false;
                return;
            }

            showCard(currentIndex);
            setInstruction("CLIQUEZ SUR LA CARTE POUR CONTINUER");
            busy = false;
        });
    }

    /* ============================================================
       FIN
       ============================================================ */

    function showActions() {
        phase = "done";
        busy = false;
        awaitingLast = false;

        updateCounters();
        setInstruction("BOOSTER RÉVÉLÉ");

        if (skipButton) {
            gsap.to(skipButton, {
                opacity: 0, y: 8, duration: .22,
                onComplete: () => { skipButton.style.display = "none"; }
            });
        }

        if (actions) {
            actions.classList.add("is-visible");
            gsap.fromTo(actions, { opacity: 0, y: 10 }, { opacity: 1, y: 0, duration: .35, ease: "power2.out" });
        }

        window.lucide?.createIcons();
    }

    /* Passer l'animation : tout est affiché instantanément */
    function finishInstantly() {
        if (phase === "done") return;
        sounds?.stop();

        introTl?.kill();
        revealTl?.kill();
        fxTl?.kill();
        slots.forEach(slot => gsap.killTweensOf(getVisual(slot)));

        cleanupIntro();
        hideMysteryCard();
        resetRarityEffects();

        phase = "collect";
        busy = true;

        for (let i = 0; i < total - 1; i++) {
            archiveCard(i, true);
        }

        currentIndex = total - 1;
        awaitingLast = false;
        showCard(currentIndex, true);

        showActions();
    }

    /* ============================================================
       ÉVÉNEMENTS
       ============================================================ */

    pack.addEventListener("click", startOpening);

    slots.forEach((slot, index) => {
        slot.addEventListener("click", event => {
            event.preventDefault();
            handleSlotClick(index);
        });
    });

    skipButton?.addEventListener("click", event => {
        event.preventDefault();
        finishInstantly();
    });

    let resizeTimer = null;
    window.addEventListener("resize", () => {
        clearTimeout(resizeTimer);
        resizeTimer = setTimeout(() => {
            slots.forEach((slot, index) => {
                if (!slot.classList.contains("is-archived")) return;
                const visual = getVisual(slot);
                if (!visual) return;

                const target = getArchiveTransform(index);
                gsap.killTweensOf(visual);
                gsap.set(visual, {
                    x: target.x, y: target.y, scale: target.scale,
                    rotation: 0, rotateX: 0, rotateY: 0, filter: "none"
                });
            });
        }, 100);
    });

    /* ============================================================
       INITIALISATION
       ============================================================ */

    slots.forEach(slot => {
        slot.classList.remove("is-current", "is-archived");
        slot.classList.add("is-face-down");
        slot.style.zIndex = "";

        const visual = getVisual(slot);
        if (!visual) return;

        if (!visual.querySelector(".booster-card-sheen")) {
            const sheen = document.createElement("span");
            sheen.className = "booster-card-sheen";
            visual.appendChild(sheen);
        }

        // Cartes cachées dans le paquet
        gsap.set(visual, { opacity: 0, visibility: "hidden", x: 0, y: 90, scale: .55, rotation: 0 });
    });

    hideMysteryCard();
    resetRarityEffects();
    startIdle();
    setInstruction("CLIQUEZ SUR LE BOOSTER");
    updateCounters();
    window.lucide?.createIcons();
});

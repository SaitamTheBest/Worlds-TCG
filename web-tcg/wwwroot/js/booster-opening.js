document.addEventListener("DOMContentLoaded", () => {
    if (typeof gsap === "undefined") {
        console.error("GSAP n'est pas chargé.");
        return;
    }

    /* ============================================================
       DOM
       ============================================================ */

    const slots = [...document.querySelectorAll(".booster-card-slot")];
    const stage = document.querySelector(".booster-opening-stage");
    const summary = document.querySelector("#booster-summary");
    const progress = document.querySelector("#revealed-count");
    const summaryCount = document.querySelector("#summary-count");
    const skipButton = document.querySelector("#skip-opening");
    const actions = document.querySelector("#booster-actions");
    const instruction = document.querySelector("#booster-instruction-text");
    const instructionContainer = document.querySelector(".booster-opening-instruction");

    const lastWarning = document.querySelector(".booster-last-warning");
    const finalGlow = document.querySelector(".booster-final-glow");
    const rarityAura = document.querySelector(".booster-rarity-aura");
    const rarityRings = [...document.querySelectorAll(".booster-rarity-ring")];
    const rarityRays = document.querySelector(".booster-rarity-rays");
    const rarityFlash = document.querySelector(".booster-rarity-flash");
    const rarityName = document.querySelector(".booster-rarity-name");
    const rarityPretitle = document.querySelector(".booster-rarity-pretitle");
    const particleContainer = document.querySelector(".booster-particles");
    const mysteryCard = document.querySelector("#booster-mystery-card");
    const openingRoot = document.querySelector(".booster-opening");

    function redirectIfOpeningExpired() {
        if (!openingRoot) return false;
        const openedAt = Date.parse(openingRoot.dataset.openedAt || "");
        const expiresAfterSeconds = Number(openingRoot.dataset.expiresAfterSeconds || "60");
        const expiredUrl = openingRoot.dataset.expiredUrl || "/Booster";
        if (!Number.isFinite(openedAt) || !Number.isFinite(expiresAfterSeconds)) return false;
        if (Date.now() < openedAt + expiresAfterSeconds * 1000) return false;
        window.location.replace(expiredUrl);
        return true;
    }

    if (redirectIfOpeningExpired()) return;
    window.addEventListener("pageshow", redirectIfOpeningExpired);
    document.addEventListener("visibilitychange", () => {
        if (document.visibilityState === "visible") redirectIfOpeningExpired();
    });

    if (!slots.length || !stage || !summary) {
        console.error("Impossible d'initialiser l'ouverture du booster.", {
            slots: slots.length,
            stage: !!stage,
            summary: !!summary
        });

        return;
    }

    /* ============================================================
       ÉTAT
       ============================================================ */

    const total = slots.length;

    let currentIndex = 0;
    let isAnimating = false;
    let openingFinished = false;
    let lastCardWaitingForReveal = false;

    /* ============================================================
       CONFIGURATION DES RARETÉS
       ============================================================ */

    const rarityConfig = {
        C: {
            label: "COMMUNE",
            colors: ["#b8b8b8", "#ffffff"],
            particles: 5,
            power: .45
        },

        R: {
            label: "RARE",
            colors: ["#49c5ff", "#dff7ff"],
            particles: 12,
            power: .75
        },

        LEADER: {
            label: "LEADER",
            colors: ["#f4c430", "#fff1a6", "#ffffff"],
            particles: 18,
            power: .85
        },

        SR: {
            label: "SUPER RARE",
            colors: ["#845dff", "#d18cff", "#ffffff"],
            particles: 22,
            power: .95
        },

        UR: {
            label: "ULTRA RARE",
            colors: ["#ffcc38", "#fff0a0", "#ff8128"],
            particles: 34,
            power: 1.15
        },

        ALT: {
            label: "ALT ART",
            colors: ["#17e9ff", "#c05dff", "#ff5d9d", "#ffffff"],
            particles: 46,
            power: 1.35
        },

        FULL_ART: {
            label: "FULL ART",
            colors: ["#ff4f91", "#ffdf4f", "#55eaff", "#a55cff", "#ffffff"],
            particles: 65,
            power: 1.55
        }
    };

    /* ============================================================
       HELPERS
       ============================================================ */

    function getVisual(slot) {
        return slot?.querySelector(".booster-card-visual");
    }

    function clamp(value, min, max) {
        return Math.min(Math.max(value, min), max);
    }

    function getBaseCardSize(slot) {
        const visual = getVisual(slot);
        const card = slot?.querySelector(".tcg-card");
        const width = card?.offsetWidth || visual?.offsetWidth || 260;
        const height = card?.offsetHeight || visual?.offsetHeight || Math.round(width * 1.4);
        return { width, height };
    }

    function setInstruction(text) {
        if (instruction) {
            instruction.textContent = text;
        }
    }

    function normalizeRarity(value) {
        const rarity = String(value || "")
            .toUpperCase()
            .replaceAll("_", "")
            .replaceAll("-", "")
            .replaceAll(" ", "")
            .trim();

        if (rarity === "UR" || rarity === "ULTRARARE") {
            return "UR";
        }

        if (rarity === "SR" || rarity === "SUPERRARE") {
            return "SR";
        }

        if (rarity === "R" || rarity === "RARE") {
            return "R";
        }

        if (rarity === "L" || rarity === "LEADER") {
            return "LEADER";
        }

        return "C";
    }

    function normalizeArtType(value) {
        const artType = String(value || "")
            .toUpperCase()
            .replaceAll("_", "")
            .replaceAll("-", "")
            .replaceAll(" ", "")
            .trim();

        if (artType === "FULLART") {
            return "FULL_ART";
        }

        if (
            artType === "ALTERNATEART" ||
            artType === "ALTART" ||
            artType === "ALT"
        ) {
            return "ALT";
        }

        return "NORMAL";
    }

    function getAnimationRarity(slot) {
        const artType = normalizeArtType(
            slot?.dataset?.artType
        );

        /*
         * Le type graphique est prioritaire.
         *
         * Une FullArt Rare doit utiliser l'animation
         * FULL_ART et non l'animation R.
         */
        if (artType === "FULL_ART") {
            return "FULL_ART";
        }

        if (artType === "ALT") {
            return "ALT";
        }

        return normalizeRarity(
            slot?.dataset?.rarity
        );
    }

    function updateCounters() {
        if (progress) {
            progress.textContent =
                Math.min(currentIndex + 1, total);
        }

        if (summaryCount) {
            summaryCount.textContent =
                `${Math.min(currentIndex, total)}/${total}`;
        }
    }

    /* ============================================================
       POSITIONNEMENT DANS LE RÉCAPITULATIF
       ============================================================ */

    function getArchiveTransform(index) {
        const visual = getVisual(slots[index]);

        if (!visual) {
            return {
                x: 0,
                y: 0,
                scale: .25
            };
        }

        const stageRect =
            stage.getBoundingClientRect();

        const summaryRect =
            summary.getBoundingClientRect();

        // IMPORTANT : offsetWidth/offsetHeight ignorent les transforms GSAP.
        // getBoundingClientRect() renverrait ici la taille déjà réduite d'une
        // miniature, ce qui ferait repasser son scale vers 1 au skip/zoom.
        const { width: cardWidth, height: cardHeight } = getBaseCardSize(slots[index]);

        const baseX =
            stageRect.width / 2;

        const baseY =
            stageRect.height / 2;

        const archiveCount =
            Math.max(total - 1, 1);

        /*
         * MOBILE / TABLETTE
         * Les cartes sont rangées horizontalement.
         */
        if (window.innerWidth <= 900) {
            const thumbWidth =
                clamp(
                    summaryRect.width / 7,
                    46,
                    62
                );

            const scale =
                thumbWidth / cardWidth;

            const thumbHeight =
                cardHeight * scale;

            const startX =
                summaryRect.left -
                stageRect.left +
                18;

            const usableWidth =
                summaryRect.width - 36;

            let stepX =
                thumbWidth * .8;

            if (
                archiveCount > 1 &&
                thumbWidth +
                stepX * (archiveCount - 1) >
                usableWidth
            ) {
                stepX =
                    (usableWidth - thumbWidth) /
                    (archiveCount - 1);
            }

            const targetX =
                startX +
                thumbWidth / 2 +
                index * stepX;

            const targetY =
                summaryRect.top -
                stageRect.top +
                summaryRect.height -
                thumbHeight / 2 -
                12;

            return {
                x: targetX - baseX,
                y: targetY - baseY,
                scale
            };
        }

        /*
         * DESKTOP
         * Les cartes sont rangées verticalement à gauche.
         */
        const thumbWidth =
            clamp(
                summaryRect.width - 58,
                64,
                86
            );

        const scale =
            thumbWidth / cardWidth;

        const thumbHeight =
            cardHeight * scale;

        const startY =
            summaryRect.top -
            stageRect.top +
            76;

        const availableHeight =
            summaryRect.height - 120;

        let stepY =
            thumbHeight * .72;

        if (
            archiveCount > 1 &&
            thumbHeight +
            stepY * (archiveCount - 1) >
            availableHeight
        ) {
            stepY =
                Math.max(
                    28,
                    (availableHeight - thumbHeight) /
                    (archiveCount - 1)
                );
        }

        const targetX =
            summaryRect.left -
            stageRect.left +
            summaryRect.width / 2;

        const targetY =
            startY +
            thumbHeight / 2 +
            index * stepY;

        return {
            x: targetX - baseX,
            y: targetY - baseY,
            scale
        };
    }

    /* ============================================================
       AFFICHAGE D'UNE CARTE
       ============================================================ */

    function showCard(index, instant = false) {
        const slot = slots[index];

        if (!slot) {
            return;
        }

        const visual =
            getVisual(slot);

        if (!visual) {
            console.error(
                `Aucun .booster-card-visual dans la carte ${index + 1}.`
            );

            return;
        }

        slot.classList.remove(
            "is-archived"
        );

        slot.classList.add(
            "is-current"
        );

        slot.style.zIndex = "35";

        gsap.killTweensOf(
            visual
        );

        gsap.set(
            visual,
            {
                x: 0,
                y: 0,
                scale: 1,
                rotation: 0,
                rotateX: 0,
                rotateY: 0,
                opacity: 1,
                visibility: "visible",
                filter: "none"
            }
        );

        if (instant) {
            return;
        }

        gsap.fromTo(
            visual,
            {
                opacity: 0,
                y: 25,
                scale: .92,
                rotateY: -8
            },
            {
                opacity: 1,
                y: 0,
                scale: 1,
                rotateY: 0,
                duration: .45,
                ease: "power3.out"
            }
        );
    }

    /* ============================================================
       ARCHIVAGE
       ============================================================ */

    function archiveCard(
        index,
        instant = false,
        onComplete = null
    ) {
        const slot =
            slots[index];

        if (!slot) {
            onComplete?.();
            return;
        }

        const visual =
            getVisual(slot);

        if (!visual) {
            onComplete?.();
            return;
        }

        const target =
            getArchiveTransform(index);

        slot.classList.remove(
            "is-current"
        );

        slot.classList.add(
            "is-archived"
        );

        slot.style.zIndex =
            String(20 + index);

        const properties = {
            x: target.x,
            y: target.y,
            scale: target.scale,
            rotation: 0,
            rotateX: 0,
            rotateY: 0,
            opacity: 1,
            visibility: "visible",
            filter: "none",
            duration: instant ? 0 : .62,
            ease: "expo.inOut"
        };

        if (instant) {
            gsap.set(
                visual,
                properties
            );

            onComplete?.();

            return;
        }

        gsap.timeline({
            onComplete
        })
            .to(
                visual,
                {
                    y: -8,
                    scale: .96,
                    duration: .12,
                    ease: "power2.out"
                }
            )
            .to(
                visual,
                properties
            );
    }

    /* ============================================================
       CARTE MYSTÈRE
       ============================================================ */

    function showMysteryCard(index) {
        const slot =
            slots[index];

        if (!slot) {
            return;
        }

        const visual =
            getVisual(slot);

        if (!visual) {
            return;
        }

        /*
         * Le slot doit rester affiché pour continuer
         * à recevoir les clics.
         */
        slot.classList.remove(
            "is-archived"
        );

        slot.classList.add(
            "is-current"
        );

        slot.style.zIndex = "35";

        /*
         * On rend momentanément la vraie carte visible
         * pour obtenir ses dimensions.
         */
        gsap.set(
            visual,
            {
                x: 0,
                y: 0,
                scale: 1,
                opacity: 1,
                visibility: "visible",
                rotation: 0,
                rotateX: 0,
                rotateY: 0,
                filter: "none"
            }
        );

        const rect =
            visual.getBoundingClientRect();

        const width =
            rect.width ||
            visual.offsetWidth ||
            300;

        const height =
            rect.height ||
            visual.offsetHeight ||
            420;

        /*
         * Puis on masque la vraie carte.
         */
        gsap.killTweensOf(
            visual
        );

        gsap.set(
            visual,
            {
                opacity: 0,
                visibility: "hidden",
                x: 0,
                y: 0,
                scale: 1,
                rotation: 0,
                rotateX: 0,
                rotateY: 0,
                filter: "none"
            }
        );

        /*
         * Sécurité : s'il n'y a pas de mystery card,
         * on laisse simplement la vraie carte.
         */
        if (!mysteryCard) {
            gsap.set(
                visual,
                {
                    visibility: "visible",
                    opacity: 1
                }
            );

            return;
        }

        mysteryCard.style.width =
            `${width}px`;

        mysteryCard.style.height =
            `${height}px`;

        mysteryCard.classList.add(
            "is-visible"
        );

        gsap.killTweensOf(
            mysteryCard
        );

        gsap.set(
            mysteryCard,
            {
                visibility: "visible",
                opacity: 0,
                xPercent: -50,
                yPercent: -50,
                scale: .86,
                rotateY: -12,
                filter: "none"
            }
        );

        gsap.to(
            mysteryCard,
            {
                opacity: 1,
                scale: 1,
                rotateY: 0,
                duration: .48,
                ease: "back.out(1.4)"
            }
        );
    }

    /* ============================================================
       PARTICULES
       ============================================================ */

    function spawnParticles(
        count,
        colors,
        power = 1
    ) {
        if (!particleContainer) {
            return;
        }

        for (
            let i = 0;
            i < count;
            i++
        ) {
            const particle =
                document.createElement(
                    "span"
                );

            particle.className =
                "booster-particle";

            const randomShape =
                Math.random();

            if (randomShape > .82) {
                particle.classList.add(
                    "is-line"
                );
            }
            else if (randomShape > .6) {
                particle.classList.add(
                    "is-diamond"
                );
            }

            const color =
                colors[
                Math.floor(
                    Math.random() *
                    colors.length
                )
                ];

            particle.style.background =
                color;

            particle.style.color =
                color;

            const size =
                3 +
                Math.random() * 7;

            if (
                !particle.classList.contains(
                    "is-line"
                )
            ) {
                particle.style.width =
                    `${size}px`;

                particle.style.height =
                    `${size}px`;
            }

            particleContainer.appendChild(
                particle
            );

            const angle =
                Math.random() *
                Math.PI *
                2;

            const distance =
                (
                    120 +
                    Math.random() * 280
                ) *
                power;

            const x =
                Math.cos(angle) *
                distance;

            const y =
                Math.sin(angle) *
                distance;

            const rotation =
                -180 +
                Math.random() * 360;

            gsap.timeline({
                delay:
                    Math.random() *
                    .14,

                onComplete:
                    () =>
                        particle.remove()
            })
                .fromTo(
                    particle,
                    {
                        x: 0,
                        y: 0,
                        scale: 0,
                        opacity: 0,
                        rotation: 0
                    },
                    {
                        x: x * .25,
                        y: y * .25,
                        scale:
                            .8 +
                            Math.random(),
                        opacity: 1,
                        rotation:
                            rotation * .25,
                        duration: .18,
                        ease: "power2.out"
                    }
                )
                .to(
                    particle,
                    {
                        x,
                        y,
                        scale: 0,
                        opacity: 0,
                        rotation,
                        duration:
                            .7 +
                            Math.random() *
                            .45,
                        ease: "power2.out"
                    }
                );
        }
    }

    /* ============================================================
       SHAKE
       ============================================================ */

    function screenShake(
        strength = 4
    ) {
        gsap.timeline()
            .to(
                stage,
                {
                    x: strength,
                    duration: .035
                }
            )
            .to(
                stage,
                {
                    x: -strength,
                    duration: .04,
                    repeat: 4,
                    yoyo: true
                }
            )
            .to(
                stage,
                {
                    x: 0,
                    duration: .04
                }
            );
    }

    /* ============================================================
       RESET DES EFFETS
       ============================================================ */

    function resetRarityEffects() {
        const effects = [
            rarityAura,
            rarityRays,
            rarityFlash,
            rarityName,
            rarityPretitle,
            finalGlow,
            ...rarityRings
        ].filter(Boolean);

        gsap.killTweensOf(
            effects
        );

        if (effects.length) {
            gsap.set(
                effects,
                {
                    opacity: 0
                }
            );
        }

        if (rarityAura) {
            gsap.set(
                rarityAura,
                {
                    xPercent: -50,
                    yPercent: -50,
                    scale: .5,
                    rotation: 0
                }
            );
        }

        if (finalGlow) {
            gsap.set(
                finalGlow,
                {
                    xPercent: -50,
                    yPercent: -50,
                    scale: .6
                }
            );
        }

        if (rarityRays) {
            gsap.set(
                rarityRays,
                {
                    xPercent: -50,
                    yPercent: -50,
                    scale: .6,
                    rotation: 0
                }
            );
        }

        rarityRings.forEach(
            ring => {
                gsap.set(
                    ring,
                    {
                        xPercent: -50,
                        yPercent: -50,
                        scale: .5,
                        rotation: 0
                    }
                );
            }
        );

        if (particleContainer) {
            particleContainer.innerHTML =
                "";
        }
    }

    /* ============================================================
       CONFIGURATION VISUELLE DE LA RARETÉ
       ============================================================ */

    function prepareRarityEffects(
        rarity
    ) {
        const config =
            rarityConfig[rarity] ??
            rarityConfig.C;

        resetRarityEffects();

        if (rarityName) {
            rarityName.textContent =
                config.label;

            rarityName.style.color =
                config.colors[0];
        }

        if (rarityAura) {
            rarityAura.style.background =
                `conic-gradient(
                    from 0deg,
                    ${config.colors.join(", ")},
                    ${config.colors[0]}
                )`;
        }

        rarityRings.forEach(
            (ring, index) => {
                const color =
                    config.colors[
                    index %
                    config.colors.length
                    ];

                ring.style.borderColor =
                    color;

                ring.style.boxShadow =
                    `0 0 20px ${color},
                     inset 0 0 18px ${color}`;
            }
        );

        if (finalGlow) {
            finalGlow.style.background =
                `radial-gradient(
                    circle,
                    ${config.colors[0]} 0%,
                    transparent 70%
                )`;
        }

        return config;
    }

    /* ============================================================
       ANIMATION COMMUNE / RARE
       ============================================================ */

    function playRareAnimation(
        visual,
        config
    ) {
        return gsap.timeline({
            onComplete:
                showActions
        })
            .to(
                finalGlow,
                {
                    opacity: .35,
                    scale: .9,
                    duration: .35
                }
            )
            .to(
                rarityPretitle,
                {
                    opacity: .5,
                    duration: .2
                },
                "<"
            )
            .to(
                rarityName,
                {
                    opacity: 1,
                    duration: .25
                },
                "<"
            )
            .to(
                rarityRings[0],
                {
                    opacity: .45,
                    scale: 1.05,
                    duration: .45,
                    ease: "power2.out"
                },
                "<"
            )
            .call(
                () =>
                    spawnParticles(
                        config.particles,
                        config.colors,
                        config.power
                    )
            )
            .to(
                visual,
                {
                    y: -8,
                    scale: 1.035,
                    duration: .3,
                    ease: "power2.out"
                },
                "<"
            )
            .to(
                visual,
                {
                    y: 0,
                    scale: 1,
                    duration: .35
                }
            );
    }

    /* ============================================================
       LEADER
       ============================================================ */

    function playLeaderAnimation(
        visual,
        config
    ) {
        return gsap.timeline({
            onComplete:
                showActions
        })
            .to(
                finalGlow,
                {
                    opacity: .5,
                    scale: 1,
                    duration: .4
                }
            )
            .to(
                rarityPretitle,
                {
                    opacity: .6,
                    duration: .2
                },
                "<"
            )
            .to(
                rarityName,
                {
                    opacity: 1,
                    scale: 1.05,
                    duration: .25
                },
                "<"
            )
            .call(
                () => {
                    spawnParticles(
                        config.particles,
                        config.colors,
                        config.power
                    );
                }
            )
            .to(
                rarityRings[0],
                {
                    opacity: .7,
                    scale: 1.15,
                    duration: .5
                },
                "<"
            )
            .to(
                visual,
                {
                    scale: 1.04,
                    y: -7,
                    duration: .3,
                    ease: "power2.out"
                },
                "<"
            )
            .to(
                visual,
                {
                    scale: 1,
                    y: 0,
                    duration: .35
                }
            );
    }

    /* ============================================================
       SUPER RARE
       ============================================================ */

    function playSuperRareAnimation(
        visual,
        config
    ) {
        return gsap.timeline({
            onComplete:
                showActions
        })
            .to(
                visual,
                {
                    scale: .97,
                    duration: .22
                }
            )
            .to(
                rarityAura,
                {
                    opacity: .42,
                    scale: .85,
                    rotation: 120,
                    duration: .55
                },
                "<"
            )
            .to(
                rarityPretitle,
                {
                    opacity: .55,
                    duration: .2
                }
            )
            .to(
                rarityName,
                {
                    opacity: 1,
                    scale: 1.04,
                    duration: .25
                },
                "<"
            )
            .to(
                rarityRings[0],
                {
                    opacity: .7,
                    scale: 1.15,
                    duration: .5,
                    ease: "expo.out"
                },
                "<"
            )
            .call(
                () =>
                    spawnParticles(
                        config.particles,
                        config.colors,
                        config.power
                    )
            )
            .to(
                rarityFlash,
                {
                    opacity: .2,
                    duration: .06
                }
            )
            .to(
                rarityFlash,
                {
                    opacity: 0,
                    duration: .22
                }
            )
            .to(
                visual,
                {
                    scale: 1.055,
                    y: -10,
                    duration: .3,
                    ease: "back.out(1.5)"
                },
                "<"
            )
            .to(
                visual,
                {
                    scale: 1,
                    y: 0,
                    duration: .4
                }
            );
    }

    /* ============================================================
       ULTRA RARE
       ============================================================ */

    function playUltraRareAnimation(
        visual,
        config
    ) {
        return gsap.timeline({
            onComplete:
                showActions
        })
            .to(
                visual,
                {
                    scale: .95,
                    y: 10,
                    duration: .3
                }
            )
            .to(
                rarityAura,
                {
                    opacity: .6,
                    scale: 1,
                    rotation: 180,
                    duration: .65
                },
                "<"
            )
            .to(
                rarityRays,
                {
                    opacity: .22,
                    scale: 1.15,
                    rotation: 90,
                    duration: .7
                },
                "<"
            )
            .to(
                rarityPretitle,
                {
                    opacity: .6,
                    duration: .15
                }
            )
            .to(
                rarityName,
                {
                    opacity: 1,
                    scale: 1.07,
                    duration: .22
                },
                "<"
            )
            .call(
                () => {
                    screenShake(4);

                    spawnParticles(
                        config.particles,
                        config.colors,
                        config.power
                    );
                }
            )
            .to(
                rarityFlash,
                {
                    opacity: .5,
                    duration: .07
                }
            )
            .to(
                rarityFlash,
                {
                    opacity: 0,
                    duration: .3
                }
            )
            .to(
                rarityRings[0],
                {
                    opacity: .9,
                    scale: 1.35,
                    duration: .55,
                    ease: "expo.out"
                },
                "<"
            )
            .to(
                rarityRings[1],
                {
                    opacity: .32,
                    scale: 1.55,
                    rotation: 90,
                    duration: .7
                },
                "<"
            )
            .to(
                finalGlow,
                {
                    opacity: .6,
                    scale: 1.1,
                    duration: .45
                },
                "<"
            )
            .to(
                visual,
                {
                    scale: 1.08,
                    y: -12,
                    filter:
                        `brightness(1.15)
                         drop-shadow(
                            0 0 22px
                            ${config.colors[0]}
                         )`,
                    duration: .35,
                    ease: "back.out(1.6)"
                },
                "<"
            )
            .to(
                visual,
                {
                    scale: 1,
                    y: 0,
                    duration: .45
                }
            );
    }

    /* ============================================================
       ALTERNATE ART
       ============================================================ */

    function playAltAnimation(
        visual,
        config
    ) {
        return gsap.timeline({
            onComplete:
                showActions
        })
            .to(
                visual,
                {
                    scale: .9,
                    rotateY: -12,
                    duration: .35
                }
            )
            .to(
                rarityAura,
                {
                    opacity: .75,
                    scale: 1.1,
                    rotation: 360,
                    duration: 1,
                    ease: "power2.out"
                },
                "<"
            )
            .to(
                rarityRays,
                {
                    opacity: .3,
                    scale: 1.25,
                    rotation: 180,
                    duration: 1
                },
                "<"
            )
            .to(
                rarityPretitle,
                {
                    opacity: .65,
                    duration: .2
                }
            )
            .to(
                rarityName,
                {
                    opacity: 1,
                    scale: 1.1,
                    duration: .25
                },
                "<"
            )
            .call(
                () => {
                    screenShake(6);

                    spawnParticles(
                        config.particles,
                        config.colors,
                        config.power
                    );
                }
            )
            .to(
                rarityFlash,
                {
                    opacity: .7,
                    duration: .07
                }
            )
            .to(
                rarityFlash,
                {
                    opacity: 0,
                    duration: .35
                }
            )
            .to(
                rarityRings[0],
                {
                    opacity: 1,
                    scale: 1.45,
                    rotation: 120,
                    duration: .65,
                    ease: "expo.out"
                },
                "<"
            )
            .to(
                rarityRings[1],
                {
                    opacity: .55,
                    scale: 1.7,
                    rotation: -90,
                    duration: .9
                },
                "<"
            )
            .to(
                finalGlow,
                {
                    opacity: .72,
                    scale: 1.2,
                    duration: .45
                },
                "<"
            )
            .to(
                visual,
                {
                    rotateY: 11,
                    scale: 1.1,
                    duration: .28
                },
                "<"
            )
            .to(
                visual,
                {
                    rotateY: -6,
                    duration: .2
                }
            )
            .to(
                visual,
                {
                    rotateY: 0,
                    scale: 1,
                    filter:
                        `brightness(1.2)
                         saturate(1.15)
                         drop-shadow(
                            0 0 26px
                            ${config.colors[1]}
                         )`,
                    duration: .55,
                    ease: "power3.out"
                }
            )
            .call(
                () =>
                    spawnParticles(
                        14,
                        config.colors,
                        .7
                    )
            );
    }

    /* ============================================================
       FULL ART
       ============================================================ */

    function playFullArtAnimation(
        visual,
        config
    ) {
        return gsap.timeline({
            onComplete:
                showActions
        })
            .to(
                visual,
                {
                    scale: .84,
                    opacity: .42,
                    filter:
                        "brightness(.5) blur(2px)",
                    duration: .5,
                    ease: "power2.in"
                }
            )
            .to(
                rarityAura,
                {
                    opacity: .7,
                    scale: .6,
                    rotation: 180,
                    duration: .5
                },
                "<"
            )
            .to(
                rarityRays,
                {
                    opacity: .12,
                    scale: .7,
                    duration: .5
                },
                "<"
            )
            .call(
                () => {
                    screenShake(9);

                    spawnParticles(
                        config.particles,
                        config.colors,
                        config.power
                    );
                }
            )
            .to(
                rarityFlash,
                {
                    opacity: 1,
                    duration: .09
                }
            )
            .to(
                rarityPretitle,
                {
                    opacity: .8,
                    duration: .12
                },
                "<"
            )
            .to(
                rarityName,
                {
                    opacity: 1,
                    scale: 1.22,
                    duration: .18
                },
                "<"
            )
            .to(
                rarityFlash,
                {
                    opacity: 0,
                    duration: .48
                }
            )
            .to(
                rarityAura,
                {
                    opacity: .95,
                    scale: 1.35,
                    rotation: 720,
                    duration: 1,
                    ease: "power3.out"
                },
                "<"
            )
            .to(
                rarityRays,
                {
                    opacity: .4,
                    scale: 1.5,
                    rotation: 280,
                    duration: 1
                },
                "<"
            )
            .to(
                rarityRings[0],
                {
                    opacity: 1,
                    scale: 1.7,
                    rotation: 180,
                    duration: .75,
                    ease: "expo.out"
                },
                "<"
            )
            .to(
                rarityRings[1],
                {
                    opacity: .65,
                    scale: 2,
                    rotation: -180,
                    duration: 1,
                    ease: "expo.out"
                },
                "<"
            )
            .to(
                finalGlow,
                {
                    opacity: .9,
                    scale: 1.35,
                    duration: .45
                },
                "<"
            )
            .to(
                visual,
                {
                    opacity: 1,
                    scale: 1.14,
                    rotateY: -12,
                    filter:
                        `brightness(1.3)
                         saturate(1.3)
                         drop-shadow(
                            0 0 35px
                            ${config.colors[0]}
                         )`,
                    duration: .42,
                    ease: "back.out(1.8)"
                },
                "<"
            )
            .to(
                visual,
                {
                    rotateY: 12,
                    duration: .22
                }
            )
            .to(
                visual,
                {
                    rotateY: -5,
                    duration: .18
                }
            )
            .to(
                visual,
                {
                    rotateY: 0,
                    scale: 1,
                    filter:
                        `brightness(1.12)
                         saturate(1.18)
                         drop-shadow(
                            0 0 22px
                            ${config.colors[0]}
                         )`,
                    duration: .55,
                    ease: "power3.out"
                }
            )
            .call(
                () =>
                    spawnParticles(
                        26,
                        config.colors,
                        .9
                    )
            )
            .to(
                rarityRings[0],
                {
                    opacity: .25,
                    scale: 2.2,
                    duration: .7
                },
                "<"
            )
            .to(
                rarityRings[1],
                {
                    opacity: .12,
                    scale: 2.5,
                    duration: .85
                },
                "<"
            )
            .to(
                rarityAura,
                {
                    opacity: .24,
                    scale: 1.15,
                    duration: .7
                },
                "<"
            );
    }

    /* ============================================================
       RÉVÉLATION DE LA DERNIÈRE CARTE
       ============================================================ */

    function playFinalAnimation(
        slot
    ) {
        if (
            !slot ||
            isAnimating ||
            openingFinished
        ) {
            return;
        }

        const visual =
            getVisual(slot);

        if (!visual) {
            return;
        }

        const rarity =
            getAnimationRarity(
                slot
            );

        const config =
            prepareRarityEffects(
                rarity
            );

        isAnimating = true;
        lastCardWaitingForReveal = false;

        setInstruction(
            "RÉVÉLATION..."
        );

        if (lastWarning) {
            gsap.to(
                lastWarning,
                {
                    opacity: 0,
                    y: -8,
                    duration: .2
                }
            );
        }

        function startRarityAnimation() {
            setInstruction(
                config.label
            );

            switch (rarity) {
                case "LEADER":
                    playLeaderAnimation(
                        visual,
                        config
                    );
                    break;

                case "SR":
                    playSuperRareAnimation(
                        visual,
                        config
                    );
                    break;

                case "UR":
                    playUltraRareAnimation(
                        visual,
                        config
                    );
                    break;

                case "ALT":
                    playAltAnimation(
                        visual,
                        config
                    );
                    break;

                case "FULL_ART":
                    playFullArtAnimation(
                        visual,
                        config
                    );
                    break;

                case "R":
                case "C":
                default:
                    playRareAnimation(
                        visual,
                        config
                    );
                    break;
            }
        }

        function revealRealCard() {
            slot.classList.remove(
                "is-archived"
            );

            slot.classList.add(
                "is-current"
            );

            slot.style.zIndex = "50";

            gsap.killTweensOf(
                visual
            );

            gsap.set(
                visual,
                {
                    visibility: "visible",
                    opacity: 0,
                    x: 0,
                    y: 0,
                    scale: .88,
                    rotation: 0,
                    rotateX: 0,
                    rotateY: -10,
                    filter: "brightness(.7)"
                }
            );

            gsap.to(
                visual,
                {
                    opacity: 1,
                    scale: 1,
                    rotateY: 0,
                    filter: "none",
                    duration: .4,
                    ease: "back.out(1.45)",
                    onComplete:
                        startRarityAnimation
                }
            );
        }

        /*
         * Sans carte mystère, on affiche immédiatement
         * la vraie dernière carte.
         */
        if (!mysteryCard) {
            revealRealCard();
            return;
        }

        gsap.killTweensOf(
            mysteryCard
        );

        gsap.timeline({
            onComplete: () => {
                mysteryCard.classList.remove(
                    "is-visible"
                );

                gsap.set(
                    mysteryCard,
                    {
                        visibility: "hidden",
                        opacity: 0,
                        xPercent: -50,
                        yPercent: -50,
                        scale: .9,
                        rotateY: 0,
                        filter: "none"
                    }
                );

                revealRealCard();
            }
        })
            .to(
                mysteryCard,
                {
                    scale: .94,
                    duration: .12,
                    ease: "power2.in"
                }
            )
            .to(
                mysteryCard,
                {
                    scale: 1.08,
                    duration: .12,
                    ease: "power2.out"
                }
            )
            .to(
                mysteryCard,
                {
                    opacity: 0,
                    scale: 1.18,
                    duration: .2,
                    ease: "power3.in"
                }
            );
    }

    /* ============================================================
       PRÉPARATION DERNIÈRE CARTE
       ============================================================ */

    function prepareLastCard() {
        showMysteryCard(
            currentIndex
        );

        lastCardWaitingForReveal = true;

        setInstruction(
            "CLIQUEZ POUR RÉVÉLER LA CARTE"
        );

        if (lastWarning) {
            gsap.fromTo(
                lastWarning,
                {
                    opacity: 0,
                    y: -8
                },
                {
                    opacity: .85,
                    y: 0,
                    duration: .35
                }
            );
        }
    }

    /* ============================================================
       FIN
       ============================================================ */

    function showActions() {
        openingFinished = true;
        isAnimating = false;
        lastCardWaitingForReveal = false;

        if (progress) {
            progress.textContent =
                total;
        }

        if (summaryCount) {
            summaryCount.textContent =
                `${Math.max(
                    total - 1,
                    0
                )}/${total}`;
        }

        setInstruction(
            "BOOSTER RÉVÉLÉ"
        );

        if (instructionContainer) {
            gsap.to(
                instructionContainer,
                {
                    opacity: .55,
                    duration: .25
                }
            );
        }

        if (skipButton) {
            gsap.to(
                skipButton,
                {
                    opacity: 0,
                    y: 8,
                    duration: .22,

                    onComplete: () => {
                        skipButton.style.display =
                            "none";
                    }
                }
            );
        }

        if (actions) {
            actions.classList.add(
                "is-visible"
            );

            gsap.fromTo(
                actions,
                {
                    opacity: 0,
                    y: 10
                },
                {
                    opacity: 1,
                    y: 0,
                    duration: .35,
                    ease: "power2.out"
                }
            );
        }

        if (
            typeof lucide !==
            "undefined"
        ) {
            lucide.createIcons();
        }
    }

    /* ============================================================
       CARTE SUIVANTE
       ============================================================ */

    function revealNext() {
        if (
            isAnimating ||
            openingFinished
        ) {
            return;
        }

        const currentCard =
            slots[currentIndex];

        if (!currentCard) {
            return;
        }

        /*
         * Dernière carte :
         * le "?" est affiché.
         * Ce clic révèle la vraie carte.
         */
        if (
            currentIndex ===
            total - 1 &&
            lastCardWaitingForReveal
        ) {
            playFinalAnimation(
                currentCard
            );

            return;
        }

        /*
         * Sécurité si on arrive directement
         * sur la dernière carte.
         */
        if (
            currentIndex ===
            total - 1
        ) {
            prepareLastCard();
            return;
        }

        isAnimating = true;

        archiveCard(
            currentIndex,
            false,
            () => {
                currentIndex++;

                updateCounters();

                /*
                 * Quand on atteint la dernière carte,
                 * on montre le dos mystérieux plutôt que
                 * la vraie carte.
                 */
                if (
                    currentIndex ===
                    total - 1
                ) {
                    prepareLastCard();

                    isAnimating = false;

                    return;
                }

                showCard(
                    currentIndex
                );

                isAnimating = false;

                setInstruction(
                    "CLIQUEZ SUR LA CARTE POUR CONTINUER"
                );
            }
        );
    }

    /* ============================================================
       PASSER L'ANIMATION
       ============================================================ */

    function skipOpening() {
        if (openingFinished) {
            return;
        }

        isAnimating = true;

        const visuals =
            slots
                .map(
                    slot =>
                        getVisual(slot)
                )
                .filter(Boolean);

        gsap.killTweensOf(
            visuals
        );

        if (mysteryCard) {
            gsap.killTweensOf(
                mysteryCard
            );

            mysteryCard.classList.remove(
                "is-visible"
            );

            gsap.set(
                mysteryCard,
                {
                    visibility: "hidden",
                    opacity: 0,
                    xPercent: -50,
                    yPercent: -50,
                    scale: .9,
                    rotateY: 0
                }
            );
        }

        /*
         * Toutes les cartes sauf la dernière
         * partent dans le récapitulatif.
         */
        for (
            let i = 0;
            i < total - 1;
            i++
        ) {
            archiveCard(
                i,
                true
            );
        }

        currentIndex =
            total - 1;

        /*
         * La dernière reste affichée au centre.
         */
        showCard(
            currentIndex,
            true
        );

        updateCounters();
        resetRarityEffects();

        lastCardWaitingForReveal =
            false;

        isAnimating = false;

        showActions();
    }

    /* ============================================================
       CLICS
       ============================================================ */

    slots.forEach(
        (slot, index) => {
            slot.addEventListener(
                "click",
                event => {
                    /*
                     * On empêche éventuellement le composant
                     * Card de déclencher une autre interaction.
                     */
                    event.preventDefault();

                    if (
                        index !==
                        currentIndex
                    ) {
                        return;
                    }

                    revealNext();
                }
            );
        }
    );

    /*
     * Normalement la mystery card a pointer-events:none
     * car le slot situé derrière récupère déjà le clic.
     *
     * On garde quand même cet event si son CSS évolue.
     */
    mysteryCard?.addEventListener(
        "click",
        event => {
            event.preventDefault();

            if (
                currentIndex ===
                total - 1 &&
                lastCardWaitingForReveal &&
                !isAnimating
            ) {
                playFinalAnimation(
                    slots[currentIndex]
                );
            }
        }
    );

    skipButton?.addEventListener(
        "click",
        event => {
            event.preventDefault();
            skipOpening();
        }
    );

    /* ============================================================
       REDIMENSIONNEMENT
       ============================================================ */

    let resizeTimer = null;

    window.addEventListener(
        "resize",
        () => {
            clearTimeout(
                resizeTimer
            );

            resizeTimer =
                setTimeout(
                    () => {
                        slots.forEach(
                            (
                                slot,
                                index
                            ) => {
                                if (
                                    !slot.classList.contains(
                                        "is-archived"
                                    )
                                ) {
                                    return;
                                }

                                const visual =
                                    getVisual(
                                        slot
                                    );

                                if (!visual) {
                                    return;
                                }

                                const target =
                                    getArchiveTransform(
                                        index
                                    );

                                gsap.killTweensOf(visual);
                                gsap.set(
                                    visual,
                                    {
                                        x:
                                            target.x,
                                        y:
                                            target.y,
                                        scale:
                                            target.scale,
                                        rotation:
                                            0,
                                        rotateX:
                                            0,
                                        rotateY:
                                            0,
                                        filter:
                                            "none"
                                    }
                                );
                            }
                        );
                    },
                    100
                );
        }
    );

    /* ============================================================
       INITIALISATION
       ============================================================ */

    /*
     * Au chargement, ton CSS met :
     *
     * .booster-card-slot {
     *     display: none;
     * }
     *
     * Il faut donc OBLIGATOIREMENT mettre is-current
     * sur la première carte.
     */
    slots.forEach(
        slot => {
            slot.classList.remove(
                "is-current",
                "is-archived"
            );

            slot.style.zIndex =
                "";
        }
    );

    /*
     * Remise à zéro de la carte mystère.
     */
    if (mysteryCard) {
        mysteryCard.classList.remove(
            "is-visible"
        );

        gsap.set(
            mysteryCard,
            {
                visibility: "hidden",
                opacity: 0,
                xPercent: -50,
                yPercent: -50,
                scale: .9,
                rotateY: 0,
                filter: "none"
            }
        );
    }

    /*
     * Remise à zéro de la fin.
     */
    if (actions) {
        actions.classList.remove(
            "is-visible"
        );

        gsap.set(
            actions,
            {
                opacity: 0
            }
        );
    }

    if (skipButton) {
        skipButton.style.display =
            "";

        gsap.set(
            skipButton,
            {
                opacity: 1,
                y: 0
            }
        );
    }

    resetRarityEffects();

    /*
     * C'est cette ligne qui manquait dans ton JS cassé.
     * Sans elle toutes les cartes restent display:none.
     */
    showCard(
        0,
        true
    );

    updateCounters();

    setInstruction(
        "CLIQUEZ SUR LA CARTE POUR CONTINUER"
    );

    if (
        typeof lucide !==
        "undefined"
    ) {
        lucide.createIcons();
    }

    console.log(
        `Booster initialisé : ${total} carte(s).`
    );
});
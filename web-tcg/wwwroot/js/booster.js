document.addEventListener("DOMContentLoaded", () => {

    if (typeof gsap === "undefined") {
        return;
    }

    // =========================================================
    // HERO
    // =========================================================

    gsap.from(".booster-hero-content", {
        opacity: 0,
        y: 35,
        duration: 0.8,
        ease: "power3.out"
    });


    // =========================================================
    // BOOSTER CARDS
    // =========================================================

    const cards = document.querySelectorAll(".booster-card");

    gsap.from(cards, {
        opacity: 0,
        y: 35,
        scale: 0.97,
        duration: 0.7,
        stagger: 0.1,
        delay: 0.15,
        ease: "power3.out"
    });


    // =========================================================
    // IMAGE PARALLAX
    // =========================================================

    cards.forEach(card => {

        const image = card.querySelector(".booster-image");

        if (!image) {
            return;
        }

        card.addEventListener("mousemove", event => {

            const rect = card.getBoundingClientRect();

            const x =
                (event.clientX - rect.left) / rect.width - 0.5;

            const y =
                (event.clientY - rect.top) / rect.height - 0.5;

            gsap.to(image, {
                rotationY: x * -8,
                rotationX: y * 5,
                duration: 0.4,
                ease: "power2.out"
            });

        });


        card.addEventListener("mouseleave", () => {

            gsap.to(image, {
                rotationY: 0,
                rotationX: 0,
                duration: 0.6,
                ease: "power3.out"
            });

        });

    });


    // =========================================================
    // REDUCED MOTION
    // =========================================================

    if (
        window.matchMedia &&
        window.matchMedia("(prefers-reduced-motion: reduce)").matches
    ) {
        gsap.globalTimeline.pause();
    }

});
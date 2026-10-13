(function () {
    'use strict';
    const page = document.getElementById('gamePage');
    const popup = document.getElementById('gameCardFocus');
    const visual = document.getElementById('gameCardFocusVisual');
    const status = document.getElementById('gameCardFocusStatus');
    if (!page || !popup || !visual) return;
    let source = null, timer;
    function hide() {
        window.clearTimeout(timer);
        source = null;
        popup.hidden = true;
    }
    function show(card) {
        if (!card.isConnected || document.querySelector('dialog[open]')) return hide();
        const id = card.querySelector('[data-card-component]')?.dataset.cardComponent;
        if (!id) return hide();
        const mount = document.createElement('div');
        mount.className = 'game-card-focus-mount';
        mount.dataset.cardComponent = id;
        visual.replaceChildren(mount);
        window.worldsCardComponents?.hydrate(visual);
        status.textContent = [card.querySelector('.game-card-hp')?.textContent,
            card.querySelector('.game-card-states')?.getAttribute('aria-label')].filter(Boolean).join(' · ');
        status.hidden = !status.textContent;
        popup.hidden = false;
        const rect = card.getBoundingClientRect();
        const width = popup.offsetWidth, height = popup.offsetHeight;
        const left = rect.right + 16 + width <= window.innerWidth ? rect.right + 16 : rect.left - width - 16;
        popup.style.left = `${Math.max(8, Math.min(left, window.innerWidth - width - 8))}px`;
        popup.style.top = `${Math.max(8, Math.min(rect.top, window.innerHeight - height - 8))}px`;
    }
    function inspect(event) {
        if (event.pointerType === 'touch' || event.buttons > 0) return;
        const card = event.target.closest('.game-card[data-instance-id]');
        if (!card || card === source) return;
        hide();
        source = card;
        timer = window.setTimeout(() => { if (source === card) show(card); }, event.type === 'focusin' ? 0 : 180);
    }
    function leave(event) {
        if (source && !source.contains(event.relatedTarget)) hide();
    }
    page.addEventListener('pointerover', inspect);
    page.addEventListener('pointerout', leave);
    page.addEventListener('focusin', inspect);
    page.addEventListener('focusout', leave);
    // The preview never captures input or changes the selected card/target.
    page.addEventListener('pointerdown', hide, true);
    page.addEventListener('click', hide, true);
    page.addEventListener('dragstart', hide, true);
    document.addEventListener('keydown', event => { if (event.key === 'Escape' || event.key === 'Enter' || event.key === ' ') hide(); });
    window.addEventListener('resize', hide);
    document.addEventListener('scroll', hide, true);
    window.addEventListener('blur', hide);
    new MutationObserver(() => { if (source && !source.isConnected) hide(); })
        .observe(page, { childList: true, subtree: true });
})();

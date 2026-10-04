(function () {
    'use strict';
    if (window.worldsCardLayout) return;
    window.worldsCardLayout = true;
    const observed = new WeakSet();
    function fit(card) {
        const info = card.querySelector('.tcg-card-info'), rules = info?.querySelector('.tcg-card-abilities');
        if (!rules || !info.clientWidth || !info.clientHeight) return;
        const style = getComputedStyle(info);
        const available = info.clientHeight - parseFloat(style.paddingTop) - parseFloat(style.paddingBottom) - 1;
        const fits = scale => { card.style.setProperty('--ability-scale', scale); return rules.scrollHeight <= available; };
        if (fits(1)) return;
        let low = .05, high = 1;
        for (let i = 0; i < 12; i++) {
            const middle = (low + high) / 2;
            if (fits(middle)) low = middle; else high = middle;
        }
        fits(low);
    }
    const resize = window.ResizeObserver ? new ResizeObserver(entries => entries.forEach(entry => fit(entry.target))) : null;
    function cardsIn(root) {
        return [...(root.matches?.('.tcg-card') ? [root] : []), ...(root.querySelectorAll?.('.tcg-card') || [])];
    }
    function scan(root) {
        cardsIn(root).forEach(card => {
            if (observed.has(card)) return;
            observed.add(card); resize?.observe(card); fit(card);
        });
    }
    function start() {
        scan(document);
        new MutationObserver(records => records.forEach(record => {
            record.removedNodes.forEach(root => cardsIn(root).filter(card => !card.isConnected).forEach(card => { resize?.unobserve(card); observed.delete(card); }));
            record.addedNodes.forEach(scan);
        })).observe(document.body, { childList: true, subtree: true });
        const refresh = () => document.querySelectorAll('.tcg-card').forEach(fit);
        document.fonts?.ready.then(refresh);
        window.addEventListener('resize', refresh);
        window.addEventListener('load', refresh, { once: true });
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start, { once: true }); else start();
})();

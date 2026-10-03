// Charger une fois le HTML du ViewComponent Card, puis le réutiliser au rendu.
(function () {
    'use strict';
    const cache = new Map();
    window.worldsCardComponents = {
        hydrate(root) {
            root.querySelectorAll('[data-card-component]').forEach(element => {
                const id = Number(element.dataset.cardComponent);
                if (!Number.isInteger(id) || id <= 0) return;
                const template = document.getElementById(`card-template-${id}`);
                if (template) {
                    element.replaceChildren(template.content.cloneNode(true));
                    if (window.lucide) window.lucide.createIcons();
                    return;
                }
                if (!cache.has(id)) {
                    cache.set(id, fetch(`/Card/Render/${id}`).then(async response => {
                        if (!response.ok || response.redirected) throw new Error('Carte indisponible');
                        return response.text();
                    }).catch(error => { cache.delete(id); throw error; }));
                }
                cache.get(id).then(html => {
                    if (!element.isConnected) return;
                    element.innerHTML = html;
                    if (window.lucide) window.lucide.createIcons();
                }).catch(() => { if (element.isConnected) element.textContent = 'Carte indisponible'; });
            });
        }
    };
})();

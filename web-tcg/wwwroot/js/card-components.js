// Charger une fois le HTML du ViewComponent Card, puis le réutiliser au rendu.
(function () {
    'use strict';
    const cache = new Map();
    function cardContent(fragment) {
        // Resources belong to the document, never to a board node replaced on every action.
        fragment.querySelectorAll('link[rel="stylesheet"]').forEach(link => {
            const url = new URL(link.getAttribute('href'), document.baseURI).href;
            if (![...document.head.querySelectorAll('link[rel="stylesheet"]')].some(existing => existing.href === url)) {
                const resource = link.cloneNode(true); resource.href = url;
                document.head.appendChild(resource);
            }
        });
        fragment.querySelectorAll('script[src]').forEach(script => {
            const url = new URL(script.getAttribute('src'), document.baseURI).href;
            if (![...document.scripts].some(existing => existing.src === url)) {
                const resource = document.createElement('script'); resource.src = url; resource.defer = true;
                document.head.appendChild(resource);
            }
        });
        const card = fragment.querySelector('.tcg-card');
        if (!card) throw new Error('Carte indisponible');
        return card.cloneNode(true);
    }
    window.worldsCardComponents = {
        hydrate(root) {
            root.querySelectorAll('[data-card-component]').forEach(element => {
                const id = Number(element.dataset.cardComponent);
                if (!Number.isInteger(id) || id <= 0) return;
                const template = document.getElementById(`card-template-${id}`);
                if (template) {
                    element.replaceChildren(cardContent(template.content));
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
                    const parsed = document.createElement("template"); parsed.innerHTML = html;
                    element.replaceChildren(cardContent(parsed.content));
                    if (window.lucide) window.lucide.createIcons();
                }).catch(() => { if (element.isConnected) element.textContent = 'Carte indisponible'; });
            });
        }
    };
})();

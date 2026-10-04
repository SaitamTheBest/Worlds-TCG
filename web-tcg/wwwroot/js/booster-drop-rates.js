(() => {
    'use strict';
    const dialog = document.getElementById('boosterDropDialog');
    const content = document.getElementById('boosterDropContent');
    if (!dialog || !content) return;
    let request;
    let trigger;
    const message = (text, error = false) => {
        const paragraph = document.createElement('p');
        paragraph.className = error ? 'booster-drop-warning' : 'booster-drop-explanation';
        paragraph.setAttribute('role', error ? 'alert' : 'status');
        paragraph.textContent = text;
        content.replaceChildren(paragraph);
    };
    document.querySelectorAll('[data-booster-drop-url]').forEach(button => {
        button.addEventListener('click', async () => {
            request?.abort();
            const current = new AbortController();
            request = current;
            trigger = button;
            message('Chargement des cartes et des probabilités…');
            content.setAttribute('aria-busy', 'true');
            content.scrollTop = 0;
            dialog.showModal();
            document.documentElement.classList.add('booster-drop-open');
            try {
                const response = await fetch(button.dataset.boosterDropUrl, { signal: current.signal });
                if (!response.ok || response.redirected) throw new Error('Chargement impossible');
                const html = await response.text();
                if (current.signal.aborted) return;
                content.innerHTML = html;
                window.lucide?.createIcons();
            } catch (error) {
                if (error.name !== 'AbortError') message('Impossible de charger les taux de drop. Fermez cette fenêtre puis réessayez.', true);
            } finally {
                if (request === current) content.setAttribute('aria-busy', 'false');
            }
        });
    });
    dialog.querySelector('.booster-drop-close').addEventListener('click', () => dialog.close());
    dialog.addEventListener('click', event => {
        if (event.target !== dialog) return;
        const bounds = dialog.getBoundingClientRect();
        if (event.clientX < bounds.left || event.clientX > bounds.right || event.clientY < bounds.top || event.clientY > bounds.bottom) dialog.close();
    });
    dialog.addEventListener('close', () => {
        request?.abort();
        document.documentElement.classList.remove('booster-drop-open');
        trigger?.focus();
    });
})();

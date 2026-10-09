(() => {
    'use strict';
    const target = document.querySelector('[data-market-price-target]');
    if (!target) return;
    const radios = document.querySelectorAll('input[name="Input.CardId"]');
    let request;
    async function update() {
        const selected = [...radios].find(radio => radio.checked);
        if (!selected) return;
        request?.abort();
        const current = new AbortController(); request = current;
        target.textContent = 'Chargement du prix du marché…';
        try {
            const response = await fetch(`/Market/PriceHistory/${Number(selected.value)}`, { signal: current.signal });
            if (!response.ok || response.redirected) throw new Error('Prix indisponible');
            const html = await response.text();
            if (request !== current) return;
            target.innerHTML = html;
            window.lucide?.createIcons();
        } catch (error) {
            if (current.signal.aborted) return;
            target.textContent = 'Le prix du marché est momentanément indisponible. Vous pouvez toujours choisir votre prix de départ.';
        }
    }
    radios.forEach(radio => radio.addEventListener('change', update));
    update();
})();

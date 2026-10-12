document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('[data-inventory-discard]').forEach(form => {
        form.addEventListener('submit', event => {
            const input = form.querySelector('[name="quantity"]');
            const quantity = Number(input.value), owned = Number(form.dataset.owned);
            if (!Number.isInteger(quantity) || quantity < 1 || quantity > owned) {
                event.preventDefault(); input.reportValidity(); return;
            }
            const lastCopy = quantity === owned ? ' Vous défaussez tous vos exemplaires ; un deck utilisant cette carte peut devenir incomplet.' : '';
            if (window.worldsConfirmation.confirmForm(event,
                `Défausser ${quantity} exemplaire(s) de « ${form.dataset.cardName} » pour ${quantity} ¤ de monnaie marché ?${lastCopy}`,
                { title: 'Défausser des cartes', confirmText: 'Défausser' }))
                form.querySelector('button[type="submit"]').disabled = true;
        });
    });
});
window.addEventListener('pageshow', () => {
    document.querySelectorAll('[data-inventory-discard] button[type="submit"]').forEach(button => { button.disabled = false; });
});

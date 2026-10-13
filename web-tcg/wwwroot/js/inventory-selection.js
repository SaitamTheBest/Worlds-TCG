document.addEventListener('DOMContentLoaded', () => {
    const form = document.getElementById('inventoryBulkDiscard');
    if (!form) return;
    const grid = document.getElementById('inventorySelectionGrid');
    const toggle = document.getElementById('inventorySelectionToggle');
    const all = document.getElementById('inventorySelectionAll');
    const count = document.getElementById('inventorySelectionCount');
    const discard = document.getElementById('inventorySelectionDiscard');
    const inputs = document.getElementById('inventorySelectionInputs');
    const choices = [...grid.querySelectorAll('[data-inventory-select]')];
    let active = false;
    const selected = () => choices.filter(choice => choice.checked);
    function update() {
        const cards = selected(), quantity = cards.reduce((sum,card) => sum + Number(card.dataset.quantity),0);
        count.textContent = `${cards.length} carte(s) · ${quantity} exemplaire(s) · ${quantity} ¤`;
        discard.disabled = !cards.length;
        all.textContent = cards.length === choices.length ? 'Tout désélectionner' : 'Tout sélectionner sur cette page';
        choices.forEach(choice => choice.closest('.inventory-card-item').classList.toggle('is-selected',active && choice.checked));
    }
    toggle.addEventListener('click', () => {
        active = !active;
        toggle.setAttribute('aria-pressed',String(active));
        toggle.textContent = active ? 'Annuler la sélection' : 'Sélectionner des cartes';
        grid.classList.toggle('is-selecting',active);
        [all,count,discard].forEach(element => element.hidden = !active);
        choices.forEach(choice => { choice.closest('label').hidden = !active; if (!active) choice.checked = false; });
        update();
    });
    choices.forEach(choice => choice.addEventListener('change',update));
    grid.addEventListener('click',event => {
        const link = event.target.closest('.inventory-card-clickable');
        if (!active || !link) return;
        event.preventDefault();
        const choice = link.closest('.inventory-card-item').querySelector('[data-inventory-select]');
        choice.checked = !choice.checked;
        update();
    });
    all.addEventListener('click',() => {
        const checked = selected().length !== choices.length;
        choices.forEach(choice => choice.checked = checked);
        update();
    });
    form.addEventListener('submit',event => {
        const cards = selected();
        if (!active || !cards.length) { event.preventDefault(); return; }
        const quantity = cards.reduce((sum,card) => sum + Number(card.dataset.quantity),0);
        inputs.replaceChildren();
        cards.forEach((card,index) => {
            for (const [field,value] of [['Id',card.value],['Quantity',card.dataset.quantity]]) {
                const input = document.createElement('input');
                input.type = 'hidden'; input.name = `items[${index}].${field}`; input.value = value;
                inputs.appendChild(input);
            }
        });
        const names = cards.map(card => `${card.dataset.name} ×${card.dataset.quantity}`).join('\n');
        if (window.worldsConfirmation.confirmForm(event,
            `Défausser TOUS les exemplaires des ${cards.length} cartes sélectionnées ?\n\n${names}\n\n${quantity} exemplaire(s) au total, pour ${quantity} ¤ de monnaie marché. Cette suppression est définitive et peut rendre vos decks incomplets.`,
            {title:'Défausser la sélection',confirmText:'Tout défausser'})) discard.disabled = true;
    });
    window.addEventListener('pageshow',update);
});

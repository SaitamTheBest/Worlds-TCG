(() => {
    'use strict';
    const normalize=value=>String(value||'').normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLocaleLowerCase('fr').trim();
    window.worldsTradePagination={init(root) {
        root.querySelectorAll('[data-trade-side]').forEach(side=> {
            const cards=[...side.querySelectorAll('[data-trade-card]')], search=side.querySelector('[data-trade-search]'), extension=side.querySelector('[data-trade-extension]');
            const previous=side.querySelector('[data-trade-previous]'), next=side.querySelector('[data-trade-next]');
            const size=6;let page=1;
            [...new Set(cards.map(card=>card.dataset.extension))].sort((a,b)=>a.localeCompare(b,'fr',{numeric:true}))
                .forEach(code=>extension.add(new Option(code,code)));
            function render() {
                const term=normalize(search.value);
                const filtered=cards.filter(card=>(!extension.value||card.dataset.extension===extension.value)&&(!term||normalize(card.dataset.search).includes(term)));
                const pages=Math.max(1,Math.ceil(filtered.length/size));page=Math.min(page,pages);
                const visible=new Set(filtered.slice((page-1)*size,page*size));
                cards.forEach(card=> {card.hidden=!visible.has(card);if(!card.hidden&&!card.querySelector('.tcg-card')) window.worldsCardComponents?.hydrate(card);});
                side.querySelector('[data-trade-page]').textContent=`${page} / ${pages} · ${filtered.length} carte(s)`;
                side.querySelector('[data-trade-empty]').hidden=filtered.length>0;
                previous.disabled=page<=1;next.disabled=page>=pages;
                const count=cards.reduce((sum,card)=>sum+Math.max(0,Number(card.querySelector('[data-trade-quantity]').value)||0),0);
                side.querySelector('[data-selected-count]').textContent=`${count} exemplaire(s) choisi(s)`;
            }
            search.addEventListener('input',()=>{page=1;render();});extension.addEventListener('change',()=>{page=1;render();});
            previous.addEventListener('click',()=>{if(page>1){page--;render();}});next.addEventListener('click',()=>{page++;render();});
            cards.forEach(card=> {const quantity=card.querySelector('[data-trade-quantity]');quantity.addEventListener('input',render);
                quantity.addEventListener('invalid',()=>{search.value='';extension.value='';page=Math.floor(cards.indexOf(card)/size)+1;render();});});
            render();
        });
    }};
})();

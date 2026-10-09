(() => {
    'use strict';
    const grid = document.querySelector('.market-selection');
    if (!grid) return;
    const stylesheet = [...document.querySelectorAll('link[rel="stylesheet"]')]
        .find(link => new URL(link.href, document.baseURI).pathname.endsWith('/css/card.css'));
    if (stylesheet && stylesheet.parentElement !== document.head) document.head.appendChild(stylesheet);
    // Les ressources du composant sont chargées une fois dans le document,
    // sans insérer ses liens CSS et scripts au milieu du formulaire.
    window.worldsCardComponents?.hydrate(grid);
})();

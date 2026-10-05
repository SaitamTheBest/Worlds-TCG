(() => {
    const normalize = text => text.normalize("NFD").replace(/[\u0300-\u036f]/g, "").toLocaleLowerCase("fr").trim();
    for (const id of ["RegionId", "CountryId", "PoliticalPartyId"]) {
        const select = document.getElementById(id);
        if (!select) continue;
        const field = document.createElement("div"); field.className = "select-search";
        const search = document.createElement("input"); search.type = "search"; search.id = `${id}-search`;
        search.placeholder = ({RegionId:"Rechercher une région…", CountryId:"Rechercher un pays…", PoliticalPartyId:"Rechercher un parti…"})[id];
        search.setAttribute("aria-label", search.placeholder); search.setAttribute("aria-controls", id); search.autocomplete = "off";
        const status = document.createElement("small"); status.setAttribute("role", "status");
        field.append(search, status); select.parentElement.before(field);
        function filter() {
            const term = normalize(search.value); let found = 0;
            for (const option of select.options) {
                option.dataset.searchExcluded = !!option.value && !normalize(option.textContent).includes(term) ? "true" : "false";
                const unavailable = option.dataset.countryExcluded === "true";
                option.hidden = unavailable || option.dataset.searchExcluded === "true";
                option.disabled = unavailable || (option.dataset.searchExcluded === "true" && !option.selected);
                if (option.value && !option.hidden) found++;
            }
            status.textContent = term ? `${found} résultat(s)` : "";
        }
        search.addEventListener("input", filter);
        search.addEventListener("keydown", event => {
            if (event.key === "Escape") { search.value = ""; filter(); }
            if (event.key === "ArrowDown" || event.key === "Enter") { event.preventDefault(); select.focus(); }
        });
        select.addEventListener("optionsupdated", filter);
        select.addEventListener("change", filter);
        filter();
    }
})();

(() => {
    const party = document.getElementById("PoliticalPartyId"), country = document.getElementById("CountryId");
    if (!party || !country) return;
    function filter() {
        for (const option of party.options) {
            option.dataset.countryExcluded = (!!country.value && !!option.value && option.dataset.countryId !== country.value) ? "true" : "false";
            option.hidden = option.dataset.countryExcluded === "true" || option.dataset.searchExcluded === "true";
            option.disabled = option.dataset.countryExcluded === "true" || (option.dataset.searchExcluded === "true" && !option.selected);
        }
        if (party.selectedOptions[0]?.dataset.countryExcluded === "true") party.value = "";
        party.dispatchEvent(new Event("optionsupdated"));
    }
    party.addEventListener("change", () => {
        const id = party.selectedOptions[0]?.dataset.countryId;
        if (id && country.value !== id) { country.value = id; country.dispatchEvent(new Event("change", { bubbles:true })); }
    });
    country.addEventListener("change", filter);
    filter();
})();

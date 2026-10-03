document.addEventListener("DOMContentLoaded", () => {
    const showcase = document.getElementById("profileShowcase");
    const inputs = document.getElementById("profileShowcaseInputs");
    const counter = document.getElementById("profileShowcaseCounter");
    const cards = [...document.querySelectorAll(".profile-owned-card")];

    if (!showcase || !inputs)
        return;

    const max = Number(showcase.dataset.max || 3);

    let selected = (showcase.dataset.selected || "")
        .split(",")
        .map(Number)
        .filter(Number.isFinite)
        .filter(id => id > 0);

    selected = [...new Set(selected)].slice(0, max);

    function getCard(id) {
        return cards.find(
            card => Number(card.dataset.cardId) === id
        );
    }

    function render() {
        const slots =
            [...showcase.querySelectorAll(".profile-showcase-slot")];

        inputs.innerHTML = "";

        slots.forEach((slot, index) => {
            const id = selected[index];

            if (!id) {
                slot.innerHTML = `
                    <div class="profile-showcase-empty">
                        <i data-lucide="plus"></i>
                        <span>Carte ${index + 1}</span>
                    </div>
                `;

                return;
            }

            const card = getCard(id);

            if (!card)
                return;

            const name = card.dataset.cardName || "";
            const image = card.dataset.cardImage || "";
            const art = card.dataset.cardArt || "";

            slot.innerHTML = `
                <button type="button"
                        class="profile-showcase-remove"
                        data-remove-id="${id}"
                        title="Retirer">
                    <i data-lucide="x"></i>
                </button>

                <div class="profile-showcase-card-art">
                    ${
                        image
                            ? `<img src="${escapeHtmlAttribute(image)}"
                                    alt="${escapeHtmlAttribute(name)}">`
                            : `<div class="profile-card-no-image">
                                   <i data-lucide="image-off"></i>
                               </div>`
                    }
                </div>

                <div class="profile-showcase-card-info">
                    <strong>${escapeHtml(name)}</strong>
                    <span>${escapeHtml(art)}</span>
                </div>
            `;

            const input =
                document.createElement("input");

            input.type = "hidden";
            input.name = "CardIds";
            input.value = id;

            inputs.appendChild(input);
        });

        cards.forEach(card => {
            const id =
                Number(card.dataset.cardId);

            card.classList.toggle(
                "is-selected",
                selected.includes(id)
            );
        });

        if (counter) {
            counter.textContent =
                `${selected.length} / ${max} sélectionnées`;
        }

        if (typeof lucide !== "undefined")
            lucide.createIcons();
    }

    cards.forEach(card => {
        card.addEventListener("click", () => {
            const id =
                Number(card.dataset.cardId);

            if (selected.includes(id)) {
                selected =
                    selected.filter(
                        currentId => currentId !== id
                    );

                render();
                return;
            }

            if (selected.length >= max)
                return;

            selected.push(id);

            render();
        });
    });

    showcase.addEventListener("click", event => {
        const removeButton =
            event.target.closest("[data-remove-id]");

        if (!removeButton)
            return;

        const id =
            Number(removeButton.dataset.removeId);

        selected =
            selected.filter(
                currentId => currentId !== id
            );

        render();
    });

    const avatarInput =
        document.getElementById("profileAvatarInput");

    avatarInput?.addEventListener("change", () => {
        const label =
            avatarInput
                .closest(".profile-file-button")
                ?.querySelector("span");

        if (!label)
            return;

        label.textContent =
            avatarInput.files?.[0]?.name ||
            "Choisir une image";
    });

    function escapeHtml(value) {
        const div =
            document.createElement("div");

        div.textContent = value;

        return div.innerHTML;
    }

    function escapeHtmlAttribute(value) {
        return escapeHtml(value)
            .replaceAll('"', "&quot;");
    }

    render();
});
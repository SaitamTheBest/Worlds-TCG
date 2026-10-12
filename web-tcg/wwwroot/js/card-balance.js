document.addEventListener("DOMContentLoaded", () => {
    const widget =
        document.getElementById("cardBalanceWidget");

    const balanceElement =
        document.getElementById("userCardBalance");

    if (!widget || !balanceElement)
        return;

    function formatBalance(value) {
        const numericValue =
            Number(value);

        if (!Number.isFinite(numericValue))
            return "0";

        return numericValue.toLocaleString(
            "fr-FR",
            {
                maximumFractionDigits: 0
            }
        );
    }

    function updateBalance(value) {
        const numericValue =
            Number(value);

        if (!Number.isFinite(numericValue))
            return;

        widget.dataset.cardBalance =
            numericValue.toString();

        balanceElement.textContent =
            formatBalance(numericValue);
    }

    /*
     * Fonction globale disponible si une autre partie
     * du frontend souhaite mettre la valeur à jour.
     */
    window.updateCardBalance =
        updateBalance;

    if (typeof signalR === "undefined") {
        console.error(
            "SignalR n'est pas chargé pour CardBalance."
        );

        return;
    }

    const connection =
        new signalR.HubConnectionBuilder()
            .withUrl("/hubs/card-balance")
            .withAutomaticReconnect()
            .build();

    connection.on(
        "CardBalanceUpdated",
        data => {
            const balance =
                typeof data === "number"
                    ? data
                    : data?.balance;

            updateBalance(balance);
        }
    );

    async function synchronizeBalance() {
        try {
            const balance =
                await connection.invoke(
                    "GetCurrentBalance"
                );

            updateBalance(balance);
        }
        catch (error) {
            console.error(
                "Impossible de synchroniser CardBalance.",
                error
            );
        }
    }

    connection.onreconnected(
        async () => {
            await synchronizeBalance();
        }
    );

    async function startConnection() {
        try {
            await connection.start();

            await synchronizeBalance();
        }
        catch (error) {
            console.error(
                "Connexion SignalR CardBalance impossible.",
                error
            );

            setTimeout(
                startConnection,
                5000
            );
        }
    }

    startConnection();
});

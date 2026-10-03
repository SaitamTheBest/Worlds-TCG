document.addEventListener("DOMContentLoaded", () => {
    const widget = document.querySelector(".tcg-balance-widget");

    if (!widget)
        return;

    const countdown =
        widget.querySelector(".tcg-balance-countdown");

    if (!countdown)
        return;

    let nextReward = widget.dataset.nextReward
        ? new Date(widget.dataset.nextReward)
        : null;

    function formatTime(milliseconds) {
        if (milliseconds <= 0)
            return "00:00";

        const totalSeconds =
            Math.ceil(milliseconds / 1000);

        const minutes =
            Math.floor(totalSeconds / 60);

        const seconds =
            totalSeconds % 60;

        return `${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")}`;
    }

    function updateCountdown() {
        if (!nextReward || Number.isNaN(nextReward.getTime())) {
            countdown.textContent = "--:--";
            return;
        }

        const remaining =
            nextReward.getTime() - Date.now();

        if (remaining <= 0) {
            countdown.textContent = "00:00";
            return;
        }

        countdown.textContent =
            formatTime(remaining);
    }

    window.updateBalanceNextReward = function (nextRewardAt) {
        nextReward = nextRewardAt
            ? new Date(nextRewardAt)
            : null;

        widget.dataset.nextReward =
            nextRewardAt ?? "";

        updateCountdown();
    };

    updateCountdown();

    setInterval(updateCountdown, 1000);
});
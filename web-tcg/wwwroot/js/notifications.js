document.addEventListener("DOMContentLoaded", () => {
    const root = document.getElementById("tcgNotification");

    if (!root)
        return;

    const trigger = document.getElementById("tcgNotificationTrigger");
    const popup = document.getElementById("tcgNotificationPopup");
    const badge = document.getElementById("tcgNotificationBadge");
    const list = document.getElementById("tcgNotificationList");
    const readAllButton = document.getElementById("tcgNotificationReadAll");
    const headerCount = document.getElementById("tcgNotificationHeaderCount");
    const toastContainer = document.getElementById("tcgNotificationToastContainer");

    let unreadCount =
        Number(root.dataset.unreadCount || 0);

    function getAntiForgeryToken() {
        return root
            .querySelector('input[name="__RequestVerificationToken"]')
            ?.value || "";
    }

    function setPopupOpen(open) {
        root.classList.toggle("is-open", open);

        trigger.setAttribute(
            "aria-expanded",
            open ? "true" : "false"
        );

        popup.setAttribute(
            "aria-hidden",
            open ? "false" : "true"
        );
    }

    function updateUnreadCount(value) {
        unreadCount = Math.max(0, value);

        root.dataset.unreadCount =
            unreadCount.toString();

        if (unreadCount <= 0) {
            badge.classList.add("hidden");
            badge.textContent = "";
            readAllButton.disabled = true;
        }
        else {
            badge.classList.remove("hidden");
            badge.textContent =
                unreadCount > 99
                    ? "99+"
                    : unreadCount.toString();

            readAllButton.disabled = false;
        }

        headerCount.textContent =
            `${unreadCount} non lue${unreadCount > 1 ? "s" : ""}`;
    }

    function formatDate(value) {
        const date = new Date(value);

        if (Number.isNaN(date.getTime()))
            return "";

        const now = new Date();

        const sameDay =
            now.toDateString() ===
            date.toDateString();

        if (sameDay) {
            return date.toLocaleTimeString(
                "fr-FR",
                {
                    hour: "2-digit",
                    minute: "2-digit"
                }
            );
        }

        return date.toLocaleDateString(
            "fr-FR",
            {
                day: "2-digit",
                month: "2-digit",
                hour: "2-digit",
                minute: "2-digit"
            }
        );
    }

    function refreshDates() {
        document
            .querySelectorAll("[data-notification-date]")
            .forEach(element => {
                element.textContent =
                    formatDate(
                        element.dataset.notificationDate
                    );
            });
    }

    function getIcon(type) {
        switch (
            String(type || "")
                .toLowerCase()
        ) {
            case "success":
                return "circle-check";

            case "warning":
                return "triangle-alert";

            case "error":
                return "circle-alert";

            default:
                return "info";
        }
    }

    function createNotificationElement(notification) {
        const button =
            document.createElement("button");

        button.type = "button";

        button.className =
            "tcg-notification-item is-unread";

        button.dataset.notificationId =
            notification.id;

        button.dataset.notificationUrl =
            notification.actionUrl || "";

        const icon =
            document.createElement("span");

        icon.className =
            "tcg-notification-item-icon";

        icon.dataset.type =
            notification.type || "info";

        icon.innerHTML =
            `<i data-lucide="${getIcon(notification.type)}"></i>`;

        const content =
            document.createElement("span");

        content.className =
            "tcg-notification-item-content";

        const title =
            document.createElement("strong");

        title.textContent =
            notification.title;

        const message =
            document.createElement("span");

        message.textContent =
            notification.message;

        const time =
            document.createElement("time");

        time.dataset.notificationDate =
            notification.createdAt;

        time.textContent =
            formatDate(notification.createdAt);

        content.append(
            title,
            message,
            time
        );

        const unreadDot =
            document.createElement("span");

        unreadDot.className =
            "tcg-notification-unread-dot";

        button.append(
            icon,
            content,
            unreadDot
        );

        return button;
    }

    function removeEmptyState() {
        document
            .getElementById("tcgNotificationEmpty")
            ?.remove();
    }

    function addNotification(notification) {
        removeEmptyState();

        const element =
            createNotificationElement(
                notification
            );

        list.prepend(element);

        while (
            list.querySelectorAll(
                ".tcg-notification-item"
            ).length > 10
        ) {
            list.lastElementChild?.remove();
        }

        if (typeof lucide !== "undefined") {
            lucide.createIcons();
        }
    }

    async function markAsRead(element) {
        if (!element.classList.contains("is-unread"))
            return;

        const id =
            element.dataset.notificationId;

        const response =
            await fetch(
                `/api/notifications/${id}/read`,
                {
                    method: "POST",
                    headers: {
                        "RequestVerificationToken":
                            getAntiForgeryToken()
                    }
                }
            );

        if (!response.ok)
            return;

        element.classList.remove("is-unread");
        element.classList.add("is-read");

        element
            .querySelector(
                ".tcg-notification-unread-dot"
            )
            ?.remove();

        updateUnreadCount(
            unreadCount - 1
        );
    }

    function showToast(notification) {
        if (!toastContainer)
            return;

        const toast =
            document.createElement("div");

        toast.className =
            "tcg-notification-toast";

        const icon =
            document.createElement("span");

        icon.className =
            "tcg-notification-toast-icon";

        icon.innerHTML =
            `<i data-lucide="${getIcon(notification.type)}"></i>`;

        const content =
            document.createElement("span");

        content.className =
            "tcg-notification-toast-content";

        const title =
            document.createElement("strong");

        title.textContent =
            notification.title;

        const message =
            document.createElement("span");

        message.textContent =
            notification.message;

        content.append(
            title,
            message
        );

        toast.append(
            icon,
            content
        );

        toastContainer.prepend(toast);

        if (typeof lucide !== "undefined")
            lucide.createIcons();

        requestAnimationFrame(() => {
            toast.classList.add(
                "is-visible"
            );
        });

        setTimeout(() => {
            toast.classList.remove(
                "is-visible"
            );

            setTimeout(
                () => toast.remove(),
                250
            );
        }, 4500);
    }

    trigger.addEventListener(
        "click",
        event => {
            event.stopPropagation();

            setPopupOpen(
                !root.classList.contains(
                    "is-open"
                )
            );
        }
    );

    document.addEventListener(
        "click",
        event => {
            if (!root.contains(event.target))
                setPopupOpen(false);
        }
    );

    document.addEventListener(
        "keydown",
        event => {
            if (event.key === "Escape")
                setPopupOpen(false);
        }
    );

    list.addEventListener(
        "click",
        async event => {
            const item =
                event.target.closest(
                    ".tcg-notification-item"
                );

            if (!item)
                return;

            try {
                await markAsRead(item);
            }
            catch (error) {
                console.error(
                    "Impossible de lire la notification.",
                    error
                );
            }

            const url =
                item.dataset.notificationUrl;

            if (url)
                window.location.href = url;
        }
    );

    readAllButton.addEventListener(
        "click",
        async () => {
            if (unreadCount <= 0)
                return;

            try {
                const response =
                    await fetch(
                        "/api/notifications/read-all",
                        {
                            method: "POST",
                            headers: {
                                "RequestVerificationToken":
                                    getAntiForgeryToken()
                            }
                        }
                    );

                if (!response.ok)
                    return;

                list
                    .querySelectorAll(
                        ".tcg-notification-item"
                    )
                    .forEach(item => {
                        item.classList.remove(
                            "is-unread"
                        );

                        item.classList.add(
                            "is-read"
                        );

                        item
                            .querySelector(
                                ".tcg-notification-unread-dot"
                            )
                            ?.remove();
                    });

                updateUnreadCount(0);
            }
            catch (error) {
                console.error(
                    "Impossible de marquer les notifications comme lues.",
                    error
                );
            }
        }
    );

    refreshDates();

    if (
        typeof signalR !== "undefined"
    ) {
        const connection =
            new signalR.HubConnectionBuilder()
                .withUrl(
                    "/hubs/notifications"
                )
                .withAutomaticReconnect()
                .build();

        connection.on(
            "NotificationCreated",
            notification => {
                addNotification(
                    notification
                );

                updateUnreadCount(
                    unreadCount + 1
                );

                showToast(
                    notification
                );
            }
        );

        async function startConnection() {
            try {
                await connection.start();
            }
            catch (error) {
                console.error(
                    "Connexion SignalR notifications impossible.",
                    error
                );

                setTimeout(
                    startConnection,
                    5000
                );
            }
        }

        startConnection();
    }
});
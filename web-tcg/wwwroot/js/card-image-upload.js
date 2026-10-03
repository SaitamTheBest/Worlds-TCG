(() => {
    "use strict";

    console.info("[CardImageUpload] Script chargé.");

    const MAX_FILE_SIZE = 8 * 1024 * 1024;

    const ALLOWED_TYPES = new Set([
        "image/png",
        "image/jpeg",
        "image/webp"
    ]);

    const ALLOWED_EXTENSIONS = [
        ".png",
        ".jpg",
        ".jpeg",
        ".webp"
    ];

    let activeUploads = 0;
    let activeUploader = null;

    /* =========================================================
       HELPERS
       ========================================================= */

    function getUploader(element) {
        return element
            ?.closest?.(
                "[data-card-image-uploader]"
            ) || null;
    }

    function getForm(uploader) {
        return uploader?.closest("form") ||
            document.getElementById("createCardForm");
    }

    function getUploadUrl(uploader) {
        const form = getForm(uploader);

        return form?.dataset?.cardImageUploadUrl ||
            "/Admin/CardImages/Upload";
    }

    function getAntiForgeryToken(uploader) {
        const form = getForm(uploader);

        return form
            ?.querySelector(
                'input[name="__RequestVerificationToken"]'
            )
            ?.value || null;
    }

    /*
     * IMPORTANT :
     * Dans Create.cshtml, .card-image-path est placé
     * juste AVANT .card-image-uploader.
     *
     * Il n'est donc pas enfant de l'uploader.
     */
    function getPathInput(uploader) {
        if (!uploader)
            return null;

        /*
         * Compatible si un jour l'input est déplacé
         * directement dans l'uploader.
         */
        const insideUploader =
            uploader.querySelector(
                ".card-image-path"
            );

        if (insideUploader)
            return insideUploader;

        /*
         * Structure actuelle :
         *
         * .create-field
         *   input.card-image-path
         *   .card-image-uploader
         */
        const field =
            uploader.closest(
                ".create-field"
            );

        const fieldInput =
            field?.querySelector(
                ".card-image-path"
            );

        if (fieldInput)
            return fieldInput;

        /*
         * Fallback pour une variante.
         */
        const variant =
            uploader.closest(
                ".variant-item"
            );

        const variantInput =
            variant?.querySelector(
                'input.card-image-path[name$=".ImageUrl"]'
            );

        if (variantInput)
            return variantInput;

        /*
         * Fallback pour l'image principale.
         */
        const baseArt =
            uploader.closest(
                ".create-base-art"
            );

        return baseArt?.querySelector(
            "#MainImageUrl"
        ) || null;
    }

    function getFileInput(uploader) {
        return uploader?.querySelector(
            ".card-image-file"
        ) || null;
    }

    function getDropzone(uploader) {
        return uploader?.querySelector(
            "[data-card-image-dropzone]"
        ) || null;
    }

    function isInternalImagePath(path) {
        return /^\/uploads\/cards\/[a-f0-9]{32}\.(png|jpg|webp)$/i
            .test(path || "");
    }

    /* =========================================================
       STATUS
       ========================================================= */

    function setStatus(
        uploader,
        type,
        message,
        icon) {
        const status =
            uploader?.querySelector(
                "[data-card-image-status]"
            );

        if (!status)
            return;

        status.innerHTML = "";

        const wrapper =
            document.createElement("span");

        if (type) {
            wrapper.classList.add(
                `is-${type}`
            );
        }

        const iconElement =
            document.createElement("i");

        iconElement.setAttribute(
            "data-lucide",
            icon
        );

        const textElement =
            document.createElement("span");

        textElement.textContent =
            message;

        wrapper.append(
            iconElement,
            textElement
        );

        status.appendChild(
            wrapper
        );

        if (typeof lucide !== "undefined") {
            lucide.createIcons();
        }
    }

    /* =========================================================
       PREVIEW
       ========================================================= */

    function getPreviewElements(uploader) {
        return {
            container:
                uploader?.querySelector(
                    ".card-image-preview"
                ) || null,

            image:
                uploader?.querySelector(
                    ".card-image-preview-img"
                ) || null
        };
    }

    function clearPreview(uploader) {
        const {
            container,
            image
        } = getPreviewElements(uploader);

        if (!container || !image)
            return;

        container.classList.add(
            "is-empty"
        );

        image.hidden = true;
        image.style.display = "none";
        image.removeAttribute("src");
    }

    function setPreviewFromPath(
        uploader,
        path) {
        const {
            container,
            image
        } = getPreviewElements(uploader);

        if (!container || !image)
            return;

        if (!path) {
            clearPreview(uploader);
            return;
        }

        container.classList.remove(
            "is-empty"
        );

        image.hidden = false;
        image.style.display = "block";

        image.src =
            `${path}${path.includes("?") ? "&" : "?"}v=${Date.now()}`;
    }

    function setTemporaryPreview(
        uploader,
        file) {
        const {
            container,
            image
        } = getPreviewElements(uploader);

        if (!container || !image)
            return;

        const objectUrl =
            URL.createObjectURL(file);

        container.classList.remove(
            "is-empty"
        );

        image.hidden = false;
        image.style.display = "block";
        image.src = objectUrl;

        image.onload = () => {
            URL.revokeObjectURL(
                objectUrl
            );
        };
    }

    /* =========================================================
       VALIDATION
       ========================================================= */

    function hasAllowedExtension(file) {
        const name =
            String(file?.name || "")
                .toLowerCase();

        return ALLOWED_EXTENSIONS.some(
            extension =>
                name.endsWith(extension)
        );
    }

    function validateFile(file) {
        if (!file) {
            return "Aucun fichier sélectionné.";
        }

        if (file.size <= 0) {
            return "Le fichier sélectionné est vide.";
        }

        if (file.size > MAX_FILE_SIZE) {
            return "L'image ne doit pas dépasser 8 Mo.";
        }

        if (
            file.type &&
            !ALLOWED_TYPES.has(file.type) &&
            !hasAllowedExtension(file)
        ) {
            return "Formats acceptés : PNG, JPG et WEBP.";
        }

        if (
            !file.type &&
            !hasAllowedExtension(file)
        ) {
            return "Formats acceptés : PNG, JPG et WEBP.";
        }

        return null;
    }

    /* =========================================================
       DATA TRANSFER
       ========================================================= */

    function extractImageFile(dataTransfer) {
        if (!dataTransfer)
            return null;

        if (dataTransfer.items) {
            for (
                const item
                of dataTransfer.items
            ) {
                if (
                    item.kind === "file" &&
                    item.type.startsWith("image/")
                ) {
                    const file =
                        item.getAsFile();

                    if (file)
                        return file;
                }
            }
        }

        if (dataTransfer.files) {
            for (
                const file
                of dataTransfer.files
            ) {
                if (
                    file &&
                    (
                        file.type.startsWith("image/") ||
                        hasAllowedExtension(file)
                    )
                ) {
                    return file;
                }
            }
        }

        return null;
    }

    /* =========================================================
       UPLOAD
       ========================================================= */

    async function uploadFile(
        uploader,
        file) {
        if (!uploader || !file)
            return false;

        activeUploader =
            uploader;

        const validationError =
            validateFile(file);

        if (validationError) {
            setStatus(
                uploader,
                "error",
                validationError,
                "circle-alert"
            );

            return false;
        }

        const pathInput =
            getPathInput(uploader);

        const dropzone =
            getDropzone(uploader);

        const uploadUrl =
            getUploadUrl(uploader);

        const token =
            getAntiForgeryToken(uploader);

        if (!pathInput) {
            console.error(
                "[CardImageUpload] .card-image-path introuvable.",
                uploader
            );

            setStatus(
                uploader,
                "error",
                "Champ ImageUrl introuvable.",
                "circle-alert"
            );

            return false;
        }

        console.log(
            "[CardImageUpload] Champ trouvé :",
            pathInput.name
        );

        if (!token) {
            console.error(
                "[CardImageUpload] Anti-forgery token introuvable."
            );

            setStatus(
                uploader,
                "error",
                "Jeton de sécurité introuvable.",
                "circle-alert"
            );

            return false;
        }

        setTemporaryPreview(
            uploader,
            file
        );

        const body =
            new FormData();

        body.append(
            "file",
            file,
            file.name || "card-image"
        );

        body.append(
            "__RequestVerificationToken",
            token
        );

        activeUploads++;

        uploader.classList.add(
            "is-uploading"
        );

        if (dropzone) {
            dropzone.disabled = true;
        }

        setStatus(
            uploader,
            "loading",
            "Enregistrement de l'illustration...",
            "loader-circle"
        );

        console.log(
            "[CardImageUpload] Envoi :",
            {
                url: uploadUrl,
                field: pathInput.name,
                name: file.name,
                type: file.type,
                size: file.size
            }
        );

        try {
            const response =
                await fetch(
                    uploadUrl,
                    {
                        method: "POST",
                        body,
                        credentials: "same-origin"
                    }
                );

            const rawResponse =
                await response.text();

            let data = null;

            if (rawResponse) {
                try {
                    data =
                        JSON.parse(
                            rawResponse
                        );
                }
                catch {
                    console.error(
                        "[CardImageUpload] Réponse non JSON :",
                        rawResponse
                    );
                }
            }

            if (!response.ok) {
                throw new Error(
                    data?.message ||
                    `Erreur HTTP ${response.status}`
                );
            }

            if (
                !data?.success ||
                !data?.path
            ) {
                throw new Error(
                    data?.message ||
                    "Le serveur n'a pas retourné le chemin de l'image."
                );
            }

            /*
             * On affecte le chemin renvoyé par le serveur
             * au champ réellement envoyé à ASP.NET.
             */
            pathInput.value =
                data.path;

            pathInput.dispatchEvent(
                new Event(
                    "change",
                    {
                        bubbles: true
                    }
                )
            );

            setPreviewFromPath(
                uploader,
                data.path
            );

            setStatus(
                uploader,
                "success",
                "Illustration enregistrée dans Worlds TCG.",
                "circle-check"
            );

            console.log(
                "[CardImageUpload] Upload terminé :",
                {
                    field: pathInput.name,
                    value: pathInput.value
                }
            );

            return true;
        }
        catch (error) {
            console.error(
                "[CardImageUpload] Échec :",
                error
            );

            /*
             * On ne détruit pas une ancienne illustration
             * déjà enregistrée si on est en modification.
             */
            if (
                !isInternalImagePath(
                    pathInput.value
                )
            ) {
                pathInput.value = "";
                clearPreview(uploader);
            }

            setStatus(
                uploader,
                "error",
                error?.message ||
                "Impossible d'enregistrer l'image.",
                "circle-alert"
            );

            return false;
        }
        finally {
            activeUploads--;

            uploader.classList.remove(
                "is-uploading"
            );

            if (dropzone) {
                dropzone.disabled = false;
            }
        }
    }

    /* =========================================================
       CLICK
       ========================================================= */

    document.addEventListener(
        "click",
        event => {
            const dropzone =
                event.target.closest?.(
                    "[data-card-image-dropzone]"
                );

            if (!dropzone)
                return;

            event.preventDefault();
            event.stopPropagation();

            const uploader =
                getUploader(dropzone);

            const fileInput =
                getFileInput(uploader);

            if (!uploader || !fileInput) {
                console.error(
                    "[CardImageUpload] Input fichier introuvable."
                );

                return;
            }

            activeUploader =
                uploader;

            console.log(
                "[CardImageUpload] Ouverture du sélecteur."
            );

            fileInput.click();
        }
    );

    /* =========================================================
       FILE INPUT
       ========================================================= */

    document.addEventListener(
        "change",
        async event => {
            const fileInput =
                event.target.closest?.(
                    ".card-image-file"
                );

            if (!fileInput)
                return;

            const uploader =
                getUploader(fileInput);

            const file =
                fileInput.files?.[0];

            if (uploader && file) {
                await uploadFile(
                    uploader,
                    file
                );
            }

            fileInput.value = "";
        }
    );

    /* =========================================================
       ACTIVE UPLOADER
       ========================================================= */

    document.addEventListener(
        "pointerdown",
        event => {
            const uploader =
                getUploader(
                    event.target
                );

            if (uploader) {
                activeUploader =
                    uploader;
            }
        }
    );

    document.addEventListener(
        "mouseover",
        event => {
            const uploader =
                getUploader(
                    event.target
                );

            if (uploader) {
                activeUploader =
                    uploader;
            }
        }
    );

    document.addEventListener(
        "focusin",
        event => {
            const uploader =
                getUploader(
                    event.target
                );

            if (uploader) {
                activeUploader =
                    uploader;
            }
        }
    );

    /* =========================================================
       DRAG ENTER
       ========================================================= */

    document.addEventListener(
        "dragenter",
        event => {
            const dropzone =
                event.target.closest?.(
                    "[data-card-image-dropzone]"
                );

            if (!dropzone)
                return;

            event.preventDefault();

            activeUploader =
                getUploader(dropzone);

            dropzone.classList.add(
                "is-dragging"
            );
        }
    );

    /* =========================================================
       DRAG OVER
       ========================================================= */

    document.addEventListener(
        "dragover",
        event => {
            const dropzone =
                event.target.closest?.(
                    "[data-card-image-dropzone]"
                );

            if (!dropzone)
                return;

            event.preventDefault();

            if (event.dataTransfer) {
                event.dataTransfer.dropEffect =
                    "copy";
            }

            dropzone.classList.add(
                "is-dragging"
            );
        }
    );

    /* =========================================================
       DRAG LEAVE
       ========================================================= */

    document.addEventListener(
        "dragleave",
        event => {
            const dropzone =
                event.target.closest?.(
                    "[data-card-image-dropzone]"
                );

            if (!dropzone)
                return;

            dropzone.classList.remove(
                "is-dragging"
            );
        }
    );

    /* =========================================================
       DROP
       ========================================================= */

    document.addEventListener(
        "drop",
        async event => {
            const dropzone =
                event.target.closest?.(
                    "[data-card-image-dropzone]"
                );

            if (!dropzone)
                return;

            event.preventDefault();
            event.stopPropagation();

            dropzone.classList.remove(
                "is-dragging"
            );

            const uploader =
                getUploader(dropzone);

            activeUploader =
                uploader;

            const file =
                extractImageFile(
                    event.dataTransfer
                );

            console.log(
                "[CardImageUpload] Drop :",
                file
            );

            if (!file) {
                setStatus(
                    uploader,
                    "error",
                    "Aucune image détectée dans le dépôt.",
                    "circle-alert"
                );

                return;
            }

            await uploadFile(
                uploader,
                file
            );
        }
    );

    /* =========================================================
       PASTE
       ========================================================= */

    document.addEventListener(
        "paste",
        async event => {
            const file =
                extractImageFile(
                    event.clipboardData
                );

            if (!file)
                return;

            const uploaderFromTarget =
                getUploader(
                    event.target
                );

            const uploader =
                uploaderFromTarget ||
                activeUploader ||
                document.querySelector(
                    "[data-card-image-uploader]"
                );

            if (!uploader)
                return;

            event.preventDefault();

            activeUploader =
                uploader;

            console.log(
                "[CardImageUpload] Image collée :",
                file
            );

            await uploadFile(
                uploader,
                file
            );
        }
    );

    /* =========================================================
       KEYBOARD
       ========================================================= */

    document.addEventListener(
        "keydown",
        event => {
            if (
                event.key !== "Enter" &&
                event.key !== " "
            ) {
                return;
            }

            const dropzone =
                event.target.closest?.(
                    "[data-card-image-dropzone]"
                );

            if (!dropzone)
                return;

            event.preventDefault();

            const uploader =
                getUploader(dropzone);

            const fileInput =
                getFileInput(uploader);

            activeUploader =
                uploader;

            fileInput?.click();
        }
    );

    /* =========================================================
       INITIAL STATE
       ========================================================= */

    function initializeExistingUploaders() {
        const uploaders =
            document.querySelectorAll(
                "[data-card-image-uploader]"
            );

        uploaders.forEach(
            uploader => {
                const pathInput =
                    getPathInput(uploader);

                if (!pathInput) {
                    console.error(
                        "[CardImageUpload] Aucun ImageUrl associé à l'uploader :",
                        uploader
                    );

                    return;
                }

                const path =
                    pathInput.value?.trim() || "";

                console.log(
                    "[CardImageUpload] Uploader associé à :",
                    pathInput.name
                );

                if (
                    path &&
                    isInternalImagePath(path)
                ) {
                    setPreviewFromPath(
                        uploader,
                        path
                    );

                    setStatus(
                        uploader,
                        "success",
                        "Illustration interne enregistrée.",
                        "circle-check"
                    );
                }
                else if (!path) {
                    clearPreview(
                        uploader
                    );
                }
            }
        );

        if (
            !activeUploader &&
            uploaders.length > 0
        ) {
            activeUploader =
                uploaders[0];
        }

        console.info(
            `[CardImageUpload] ${uploaders.length} zone(s) détectée(s).`
        );
    }

    /* =========================================================
       SUBMIT
       ========================================================= */

    document.addEventListener(
        "submit",
        event => {
            const form =
                event.target;

            if (
                !form.matches?.(
                    "#createCardForm"
                )
            ) {
                return;
            }

            if (activeUploads > 0) {
                event.preventDefault();

                window.alert(
                    "Une illustration est encore en cours d'enregistrement."
                );

                return;
            }

            const imageInputs = [
                ...form.querySelectorAll(
                    ".card-image-path"
                )
            ];

            const invalid =
                imageInputs.find(
                    input =>
                        !isInternalImagePath(
                            input.value
                        )
                );

            if (!invalid)
                return;

            event.preventDefault();

            const field =
                invalid.closest(
                    ".create-field"
                );

            const uploader =
                field?.querySelector(
                    "[data-card-image-uploader]"
                );

            activeUploader =
                uploader;

            uploader?.scrollIntoView({
                behavior: "smooth",
                block: "center"
            });

            if (uploader) {
                setStatus(
                    uploader,
                    "error",
                    "Une illustration interne est obligatoire.",
                    "circle-alert"
                );
            }

            window.alert(
                "Chaque carte doit posséder une illustration enregistrée dans Worlds TCG."
            );
        },
        true
    );

    /* =========================================================
       INIT
       ========================================================= */

    if (
        document.readyState ===
        "loading"
    ) {
        document.addEventListener(
            "DOMContentLoaded",
            initializeExistingUploaders,
            {
                once: true
            }
        );
    }
    else {
        initializeExistingUploaders();
    }

    window.initializeCardImageUploaders =
        initializeExistingUploaders;
})();
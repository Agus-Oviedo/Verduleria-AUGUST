window.augustu = {
    scrollToAndHighlight: function (elementId) {
        const element = document.getElementById(elementId);
        if (!element) return;
        element.scrollIntoView({ behavior: "smooth", block: "start" });
        element.classList.remove("attention-highlight");
        void element.offsetWidth;
        element.classList.add("attention-highlight");
        window.setTimeout(() => element.classList.remove("attention-highlight"), 2200);
    }
};

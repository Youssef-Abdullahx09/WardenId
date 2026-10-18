// Show/hide control for password fields (Features/Tenants/_PasswordToggle.cshtml). Progressive
// enhancement only: the button ships with the `hidden` attribute, so without JavaScript it never
// appears and the form works unchanged. Served same-origin, with no inline script.
document.querySelectorAll("[data-password-toggle]").forEach((button) => {
    const input = document.getElementById(button.getAttribute("aria-controls"));
    if (!input) {
        return;
    }

    const setVisible = (visible) => {
        input.type = visible ? "text" : "password";
        button.setAttribute("aria-pressed", String(visible));
        button.setAttribute("aria-label", visible ? "Hide password" : "Show password");
    };

    button.hidden = false;
    button.addEventListener("click", () => {
        setVisible(input.type === "password");
        input.focus();
    });

    // Always submit as a password field, so the browser never treats the value as plain text
    // (e.g. saving it to autofill form history).
    input.form?.addEventListener("submit", () => setVisible(false));
});

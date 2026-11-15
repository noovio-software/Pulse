// Filtres : saisir une date passe la période sur « Dates… » ; choisir une période prédéfinie vide les dates.
document.addEventListener("change", (event) => {
    const form = event.target.closest("form.filters");
    if (!form) return;
    const period = form.elements["periode"];
    if (event.target.type === "date" && event.target.value) {
        period.value = "dates";
    } else if (event.target === period && period.value !== "dates") {
        form.elements["du"].value = "";
        form.elements["au"].value = "";
    }
});

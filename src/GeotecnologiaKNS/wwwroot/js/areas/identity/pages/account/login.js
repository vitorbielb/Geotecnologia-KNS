function switchCheckBoxValue(checkbox) {
    var hiddenInput = checkbox.prev('input[type="hidden"]');
    if (checkbox.prop('checked')) {
        hiddenInput.val('true');
    } else {
        hiddenInput.val('false');
    }
}
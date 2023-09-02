function switchCheckBoxValue(checkbox) {
    var hiddenInput = checkbox.prev('input[type="hidden"]');
    if (checkbox.prop('checked')) {
        hiddenInput.val('enabled');
    } else {
        hiddenInput.val('disabled');
    }
}
function displayUsers(tenantId) {
    $.ajax({
        url: "./Permissions/GetUsers",
        type: "GET",
        data: { tenantId: tenantId },
        dataType: "json",
        success: function (data) {

            var dropdown = $("#usersDropdown");
            dropdown.empty();
            dropdown.removeAttr('disabled');
            dropdown.append($('<option></option>').val(null).text('Selecione...'));
            for (var i = 0; i < data.length; i++) {
                dropdown.append($('<option></option>').val(data[i].value).text(data[i].text));
            }
        }
    });
}

function displayFeatures(userId) {
    $.ajax({
        url: "./Permissions/GetClaims",
        type: "GET",
        data: { userId: userId },
        dataType: "html",
        success: function (response) {
            $("#edit-claims").html(response);
        }
    });
}
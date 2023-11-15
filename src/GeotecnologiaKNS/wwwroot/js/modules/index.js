function displayFeatures(tenantId) {
    $.ajax({
        url: "./Modules/GetClaims",
        type: "GET",
        data: { tenantId: tenantId },
        dataType: "html",
        success: function (response) {
            $("#edit-claims").html(response);
        }
    });
}
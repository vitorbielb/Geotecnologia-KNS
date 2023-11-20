function populateCities(uf) {
    $.ajax({
        url: "/Propriedades/GetCitiesByUF",
        type: "GET",
        data: { uf: uf },
        dataType: "json",

        success: function (data) {

            var dropdown = $("#citiesDropdown");
            dropdown.empty();
            dropdown.removeAttr('disabled');
            dropdown.append($('<option></option>').val(null).text('Selecione...'));
            for (var i = 0; i < data.length; i++) {
                dropdown.append($('<option></option>').val(data[i].value).text(data[i].text));
            }
        }
    });
}

$(document).ready(function () {
    $('#Latitude, #Longitude').on('change', function () {
        var latitude = $('#Latitude').val();
        var longitude = $('#Longitude').val();
        geozoneMapSet(latitude, longitude);
    });
});


function geozoneMapSet(latitude, longitude) {
    if (latitude && longitude) {
        $.ajax({
            url: "GeozoneMap?centerLat=" + latitude + "&centerLong=" + longitude,
            type: "GET",
            success: function (html) {
                $('#geozone-map').html(html);
            }
        });

    }
}